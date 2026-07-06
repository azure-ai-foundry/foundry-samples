# Copyright (c) Microsoft. All rights reserved.

"""Azure TTS plugin for LiveKit Agents with text-streaming support.

The official ``livekit-plugins-azure`` package only ships a *chunked*
(SSML POST) implementation: every utterance waits for a complete
sentence, then a single HTTP request synthesises it end-to-end. The
plugin advertises ``streaming=False``, so the agent can't start
speaking until the LLM has produced enough text. That adds noticeable
time-to-first-audio.

This module wraps the Azure Speech SDK's *TextStream* input
(``SpeechSynthesisRequest`` + websocket v2 endpoint) into a LiveKit
``tts.TTS`` subclass that advertises ``streaming=True``. LLM tokens
arriving on the ``SynthesizeStream._input_ch`` are forwarded into a
persistent ``input_stream`` as they arrive; the SDK pushes audio
chunks back over a single websocket while text is still being written.

"""

from __future__ import annotations

import asyncio
import logging
from dataclasses import dataclass
from typing import AsyncIterator, Optional

from livekit.agents import (
    APIConnectionError,
    APIConnectOptions,
    tts,
    utils,
)
from livekit.agents.types import DEFAULT_API_CONNECT_OPTIONS

try:
    from azure.cognitiveservices.speech import (
        CancellationReason,
        Connection,
        PropertyId,
        SpeechConfig,
        SpeechSynthesisOutputFormat,
        SpeechSynthesisRequest,
        SpeechSynthesisRequestInputType,
        SpeechSynthesizer,
    )
except ModuleNotFoundError as e:  # pragma: no cover
    raise ModuleNotFoundError(
        "azure-cognitiveservices-speech is required for "
        "AzureTextStreamingTTS; install it with "
        "`pip install azure-cognitiveservices-speech`."
    ) from e


logger = logging.getLogger("azure-tts-text-streaming")


_SAMPLE_RATE_TO_FORMAT: dict[int, SpeechSynthesisOutputFormat] = {
    8000: SpeechSynthesisOutputFormat.Raw8Khz16BitMonoPcm,
    16000: SpeechSynthesisOutputFormat.Raw16Khz16BitMonoPcm,
    24000: SpeechSynthesisOutputFormat.Raw24Khz16BitMonoPcm,
    48000: SpeechSynthesisOutputFormat.Raw48Khz16BitMonoPcm,
}


# ---------------------------------------------------------------------------
# Pooled synthesizer
# ---------------------------------------------------------------------------


class _PooledSynth:
    """A pre-warmed ``SpeechSynthesizer`` with a per-instance audio queue.

    The Azure Speech SDK delivers audio via event callbacks fired on a
    background thread. Each pooled synth owns its own ``asyncio.Queue``
    so the callbacks (captured by closure at ``connect`` time) can push
    chunks into the right queue without cross-talk between concurrent
    turns.

    ``connection`` keeps the preconnected websocket alive (the SDK does
    not store a back-reference).
    """

    __slots__ = ("synth", "connection", "queue", "loop", "disposed", "errored")

    def __init__(
        self,
        synth: SpeechSynthesizer,
        connection: Optional[Connection],
    ) -> None:
        self.synth: Optional[SpeechSynthesizer] = synth
        self.connection: Optional[Connection] = connection
        self.queue: asyncio.Queue = asyncio.Queue()
        self.loop: Optional[asyncio.AbstractEventLoop] = None
        self.disposed = False
        self.errored = False

    def enqueue_threadsafe(self, item) -> None:
        """Push a queue item from the SDK's background thread."""
        loop = self.loop
        if loop is not None and loop.is_running():
            loop.call_soon_threadsafe(self.queue.put_nowait, item)
        else:
            # Fallback for callbacks that fire before _run captures the loop.
            self.queue.put_nowait(item)

    def drain_queue(self) -> None:
        while not self.queue.empty():
            try:
                self.queue.get_nowait()
            except asyncio.QueueEmpty:
                break

    def dispose(self) -> None:
        if self.disposed:
            return
        self.disposed = True
        try:
            if self.connection is not None:
                self.connection.close()
        except Exception:
            pass
        self.connection = None
        # The Python SDK's SpeechSynthesizer cleans up via __del__; just
        # drop the reference.
        self.synth = None


# ---------------------------------------------------------------------------
# Options
# ---------------------------------------------------------------------------


@dataclass
class _Opts:
    api_key: str
    region: str
    voice: str
    sample_rate: int
    frame_timeout_ticks: int  # PropertyId.SpeechSynthesis_FrameTimeoutInterval (100-ns)
    rtf_timeout_threshold: float


# ---------------------------------------------------------------------------
# TTS
# ---------------------------------------------------------------------------


class TTS(tts.TTS):
    """Azure TTS using TextStream input (websocket v2) with token streaming."""

    def __init__(
        self,
        *,
        speech_key: str,
        speech_region: str,
        voice: str = "en-US-AvaMultilingualNeural",
        sample_rate: int = 24000,
        pool_size: int = 2,
        # 100-ns ticks; large default effectively disables the SDK timeout so
        # slow LLMs don't cause synthesis to be cancelled.
        frame_timeout_ticks: int = 100_000_000,
        rtf_timeout_threshold: float = 10.0,
    ) -> None:
        if sample_rate not in _SAMPLE_RATE_TO_FORMAT:
            raise ValueError(
                f"Unsupported sample_rate={sample_rate}; "
                f"choose from {sorted(_SAMPLE_RATE_TO_FORMAT)}"
            )

        super().__init__(
            capabilities=tts.TTSCapabilities(streaming=True),
            sample_rate=sample_rate,
            num_channels=1,
        )

        self._opts = _Opts(
            api_key=speech_key,
            region=speech_region,
            voice=voice,
            sample_rate=sample_rate,
            frame_timeout_ticks=frame_timeout_ticks,
            rtf_timeout_threshold=rtf_timeout_threshold,
        )
        self._pool_size = max(1, pool_size)
        self._speech_config: Optional[SpeechConfig] = None
        self._pool: Optional[asyncio.Queue[_PooledSynth]] = None
        self._init_lock = asyncio.Lock()

    @property
    def model(self) -> str:
        return self._opts.voice

    @property
    def provider(self) -> str:
        return "Azure TTS (TextStream)"

    # -- pool management ----------------------------------------------------

    def _build_speech_config(self) -> SpeechConfig:
        # Text streaming requires the v2 websocket endpoint.
        endpoint = (
            f"wss://{self._opts.region}.tts.speech.microsoft.com/"
            f"cognitiveservices/websocket/v2"
        )
        cfg = SpeechConfig(endpoint=endpoint, subscription=self._opts.api_key)
        cfg.speech_synthesis_voice_name = self._opts.voice
        cfg.set_speech_synthesis_output_format(
            _SAMPLE_RATE_TO_FORMAT[self._opts.sample_rate]
        )
        cfg.set_property(
            PropertyId.SpeechSynthesis_FrameTimeoutInterval,
            str(self._opts.frame_timeout_ticks),
        )
        cfg.set_property(
            PropertyId.SpeechSynthesis_RtfTimeoutThreshold,
            str(self._opts.rtf_timeout_threshold),
        )
        return cfg

    def _build_pooled_synth(self) -> _PooledSynth:
        assert self._speech_config is not None
        synth = SpeechSynthesizer(
            speech_config=self._speech_config, audio_config=None
        )
        wrapper = _PooledSynth(synth, connection=None)

        # Bind callbacks; ``w`` captures this wrapper so chunks land in its queue.
        synth.synthesizing.connect(
            lambda evt, w=wrapper: self._on_synthesizing(w, evt)
        )
        synth.synthesis_completed.connect(
            lambda evt, w=wrapper: self._on_completed(w, evt)
        )
        synth.synthesis_canceled.connect(
            lambda evt, w=wrapper: self._on_canceled(w, evt)
        )

        # Preconnect so the first speak_async doesn't pay the websocket
        # handshake latency. The Connection must outlive the synth.
        try:
            conn = Connection.from_speech_synthesizer(synth)
            conn.open(True)
            wrapper.connection = conn
        except Exception as e:
            logger.warning("Azure TTS preconnect failed: %s", e)
        return wrapper

    async def _ensure_pool(self) -> asyncio.Queue[_PooledSynth]:
        if self._pool is not None:
            return self._pool
        async with self._init_lock:
            if self._pool is not None:
                return self._pool
            self._speech_config = self._build_speech_config()
            pool: asyncio.Queue[_PooledSynth] = asyncio.Queue()
            for _ in range(self._pool_size):
                pool.put_nowait(self._build_pooled_synth())
            self._pool = pool
            logger.info(
                "Azure TextStream TTS ready: voice=%s pool_size=%d",
                self._opts.voice,
                self._pool_size,
            )
            return pool

    async def _acquire(self) -> _PooledSynth:
        pool = await self._ensure_pool()
        try:
            w = pool.get_nowait()
        except asyncio.QueueEmpty:
            logger.warning(
                "Azure TTS pool exhausted; building extra synth on demand"
            )
            w = self._build_pooled_synth()
        w.drain_queue()
        return w

    async def _release(self, w: _PooledSynth) -> None:
        if self._pool is None or w.disposed or w.errored or w.synth is None:
            w.dispose()
            if self._pool is not None:
                try:
                    self._pool.put_nowait(self._build_pooled_synth())
                except Exception as e:
                    logger.error("Azure TTS pool refill failed: %s", e)
            return
        w.drain_queue()
        w.loop = None
        self._pool.put_nowait(w)

    # -- SDK callbacks (run on the Speech SDK background thread) ------------

    def _on_synthesizing(self, wrapper: _PooledSynth, evt) -> None:
        if evt.result and evt.result.audio_data:
            wrapper.enqueue_threadsafe(evt.result.audio_data)

    def _on_completed(self, wrapper: _PooledSynth, evt) -> None:
        wrapper.enqueue_threadsafe(None)

    def _on_canceled(self, wrapper: _PooledSynth, evt) -> None:
        reason = evt.result.cancellation_details.reason
        if reason == CancellationReason.CancelledByUser:
            # We stopped synthesis ourselves (interruption); treat as EOS.
            wrapper.enqueue_threadsafe(None)
            return
        details = evt.result.cancellation_details
        msg = f"Azure TTS canceled: {reason}"
        if details.error_details:
            msg += f" - {details.error_details}"
        logger.error(msg)
        wrapper.errored = True
        wrapper.enqueue_threadsafe(RuntimeError(msg))

    # -- LiveKit tts.TTS interface ------------------------------------------

    def synthesize(
        self,
        text: str,
        *,
        conn_options: APIConnectOptions = DEFAULT_API_CONNECT_OPTIONS,
    ) -> tts.ChunkedStream:
        return _ChunkedStream(
            tts=self, input_text=text, conn_options=conn_options
        )

    def stream(
        self,
        *,
        conn_options: APIConnectOptions = DEFAULT_API_CONNECT_OPTIONS,
    ) -> tts.SynthesizeStream:
        return _SynthesizeStream(tts=self, conn_options=conn_options)

    async def aclose(self) -> None:
        if self._pool is None:
            return
        while not self._pool.empty():
            try:
                w = self._pool.get_nowait()
            except asyncio.QueueEmpty:
                break
            w.dispose()
        self._pool = None


# ---------------------------------------------------------------------------
# Streams
# ---------------------------------------------------------------------------


class _ChunkedStream(tts.ChunkedStream):
    """One-shot synthesis path (still goes through the streaming session)."""

    def __init__(
        self,
        *,
        tts: TTS,
        input_text: str,
        conn_options: APIConnectOptions,
    ) -> None:
        super().__init__(
            tts=tts, input_text=input_text, conn_options=conn_options
        )
        self._tts: TTS = tts

    async def _run(self, output_emitter: tts.AudioEmitter) -> None:
        request_id = utils.shortuuid()
        output_emitter.initialize(
            request_id=request_id,
            sample_rate=self._tts._opts.sample_rate,
            num_channels=1,
            mime_type="audio/pcm",
        )
        await _run_one_segment(
            tts_=self._tts,
            text_iter=_single_text_iter(self._input_text),
            emitter=output_emitter,
            segment_id=request_id,
        )


class _SynthesizeStream(tts.SynthesizeStream):
    """Streaming path: pump ``_input_ch`` tokens into a TextStream request."""

    def __init__(
        self, *, tts: TTS, conn_options: APIConnectOptions
    ) -> None:
        super().__init__(tts=tts, conn_options=conn_options)
        self._tts: TTS = tts

    async def _run(self, output_emitter: tts.AudioEmitter) -> None:
        request_id = utils.shortuuid()
        output_emitter.initialize(
            request_id=request_id,
            sample_rate=self._tts._opts.sample_rate,
            num_channels=1,
            stream=True,
            mime_type="audio/pcm",
        )

        async def token_iter() -> AsyncIterator[str]:
            first = True
            async for item in self._input_ch:
                # Azure streams audio as text arrives, so a mid-stream flush
                # has no extra effect; just keep forwarding tokens.
                if isinstance(item, self._FlushSentinel):
                    continue
                if not item:
                    continue
                if first:
                    # Required so the base ``SynthesizeStream`` auto-emits
                    # TTSMetrics: ttfb is measured from this timestamp to
                    # the first audio chunk pushed via the AudioEmitter.
                    self._mark_started()
                    first = False
                yield item

        await _run_one_segment(
            tts_=self._tts,
            text_iter=token_iter(),
            emitter=output_emitter,
            segment_id=request_id,
        )


# ---------------------------------------------------------------------------
# Shared segment driver
# ---------------------------------------------------------------------------


async def _single_text_iter(text: str) -> AsyncIterator[str]:
    if text:
        yield text


async def _run_one_segment(
    *,
    tts_: TTS,
    text_iter: AsyncIterator[str],
    emitter: tts.AudioEmitter,
    segment_id: str,
) -> None:
    """Drive one Azure TextStream session end-to-end.

    Producer side (this coroutine): pulls tokens off ``text_iter`` and
    writes them into the request's ``input_stream``.

    Consumer side (``consumer_task``): drains the per-synth audio queue
    fed by the SDK's background callbacks and forwards bytes to the
    LiveKit ``AudioEmitter``. Running it as a separate task decouples
    audio delivery from token-arrival cadence.
    """
    wrapper = await tts_._acquire()
    wrapper.loop = asyncio.get_running_loop()

    consumer_task: Optional[asyncio.Task] = None
    request: Optional[SpeechSynthesisRequest] = None
    interrupted = False
    consumer_error: list[BaseException] = []

    try:
        request = SpeechSynthesisRequest(
            input_type=SpeechSynthesisRequestInputType.TextStream
        )
        wrapper.synth.speak_async(request)

        emitter.start_segment(segment_id=segment_id)

        async def consumer() -> None:
            while True:
                item = await wrapper.queue.get()
                if item is None:
                    return
                if isinstance(item, BaseException):
                    consumer_error.append(item)
                    return
                emitter.push(item)

        consumer_task = asyncio.create_task(
            consumer(), name="azure-tts-stream-consumer"
        )

        # Producer loop -----------------------------------------------------
        try:
            async for text in text_iter:
                # ``input_stream.write`` is a fast non-blocking enqueue
                # into the SDK's internal buffer.
                request.input_stream.write(text)
        except asyncio.CancelledError:
            interrupted = True
            raise
        finally:
            try:
                request.input_stream.close()
            except Exception as e:
                logger.warning("Azure TTS input_stream.close failed: %s", e)

        # Wait for the consumer to drain remaining audio after EOS.
        try:
            await asyncio.wait_for(consumer_task, timeout=30.0)
        except asyncio.TimeoutError:
            logger.warning("Azure TTS consumer timed out; cancelling")
            consumer_task.cancel()
            try:
                await consumer_task
            except BaseException:
                pass
            raise APIConnectionError(
                "Azure TTS timed out waiting for audio completion"
            )

        if consumer_error:
            raise APIConnectionError(str(consumer_error[0]))

        emitter.end_segment()

    except asyncio.CancelledError:
        interrupted = True
        raise
    finally:
        if consumer_task is not None and not consumer_task.done():
            consumer_task.cancel()
            try:
                await consumer_task
            except BaseException:
                pass
        if interrupted and wrapper.synth is not None:
            # Tell Azure to abandon any in-flight audio for this turn.
            try:
                fut = wrapper.synth.stop_speaking_async()
                await asyncio.to_thread(fut.get)
            except Exception:
                pass
        wrapper.drain_queue()
        await tts_._release(wrapper)
