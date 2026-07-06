# Copyright (c) Microsoft. All rights reserved.

"""Azure TTS Text Streaming service for pipecat.

Uses the Azure Speech SDK's TextStream input type (websocket v2 endpoint)
to stream LLM tokens directly into the synthesizer, producing audio with
significantly lower latency than the per-sentence SSML approach used by
the stock AzureTTSService.

References:
  - Azure SDK sample: cognitive-services-speech-sdk/samples/python/tts-text-stream/text_stream_sample.py
  - Azure Speech SDK SpeechSynthesisRequest / SpeechSynthesisRequestInputType.TextStream
  - pipecat TTSService base class (pipecat.services.tts_service)
  - pipecat AzureTTSService (pipecat.services.azure.tts)
"""

import asyncio
from typing import AsyncGenerator, Optional

from loguru import logger

from pipecat.frames.frames import (
    CancelFrame,
    EndFrame,
    ErrorFrame,
    Frame,
    InterruptionFrame,
    StartFrame,
    TTSAudioRawFrame,
    TTSStartedFrame,
    TTSStoppedFrame,
)
from pipecat.processors.frame_processor import FrameDirection
from pipecat.services.azure.tts import (
    AzureBaseTTSService,
    AzureTTSSettings,
    sample_rate_to_output_format,
)
from pipecat.services.tts_service import TextAggregationMode, TTSService
from pipecat.utils.tracing.service_decorators import traced_tts

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
except ModuleNotFoundError as e:
    logger.error(f"Exception: {e}")
    logger.error("In order to use Azure TTS Text Streaming, you need to `pip install azure-cognitiveservices-speech`.")
    raise Exception(f"Missing module: {e}")


class _PooledSynth:
    """A pooled SpeechSynthesizer with a per-instance audio queue and persistent connection.

    Each pooled synth has its own ``asyncio.Queue`` because the Azure Speech SDK
    binds event callbacks at ``synthesizing.connect(...)`` time -- the callbacks
    capture this wrapper via closure and push audio chunks into the wrapper's
    own queue. This lets multiple turns/synthesizers run concurrently without
    cross-talk on a shared queue.

    The ``connection`` reference keeps the preconnected websocket alive (it would
    otherwise be garbage-collected since SDK doesn't store the back-reference).
    """

    __slots__ = ("synth", "connection", "audio_queue", "loop", "disposed", "errored")

    def __init__(self, synth: SpeechSynthesizer, connection: Optional[Connection]):
        self.synth: Optional[SpeechSynthesizer] = synth
        self.connection: Optional[Connection] = connection
        self.audio_queue: asyncio.Queue = asyncio.Queue()
        self.loop: Optional[asyncio.AbstractEventLoop] = None
        self.disposed: bool = False
        # If the synth hit a non-user synthesis_canceled error, mark it so
        # the pool can replace it with a fresh instance on release.
        self.errored: bool = False

    def enqueue_threadsafe(self, item):
        """Thread-safe enqueue from SDK background thread into the asyncio queue."""
        loop = self.loop
        if loop is not None and loop.is_running():
            loop.call_soon_threadsafe(self.audio_queue.put_nowait, item)
        else:
            # Fallback when called before the loop is captured (e.g. preconnect).
            self.audio_queue.put_nowait(item)

    def drain_queue(self):
        """Drop any pending items so the synth is ready for the next turn."""
        while not self.audio_queue.empty():
            try:
                self.audio_queue.get_nowait()
            except asyncio.QueueEmpty:
                break

    def dispose(self):
        """Tear down the connection and drop the synth reference."""
        if self.disposed:
            return
        self.disposed = True
        try:
            if self.connection is not None:
                self.connection.close()
        except Exception:
            pass
        self.connection = None
        # The Python SDK's SpeechSynthesizer cleans up via __del__; dropping
        # the reference is sufficient.
        self.synth = None


class AzureTTSTextStreamingService(TTSService, AzureBaseTTSService):
    """Azure TTS service using the TextStream input type for token-level streaming.

    Instead of constructing SSML and synthesizing complete sentences, this
    service opens a persistent TextStream request and writes LLM tokens into
    it as they arrive.  The Azure Speech SDK streams audio chunks back via
    the websocket v2 endpoint, yielding TTSAudioRawFrame's with minimal
    latency.

    Key differences from AzureTTSService:
      - Uses ``SpeechSynthesisRequest(input_type=TextStream)`` + ``speak_async``
      - Text is written token-by-token via ``request.input_stream.write(text)``
      - A single TextStream request is kept open for the entire LLM turn
      - The stream is closed in ``flush_audio`` when the response ends
      - Uses the wss:// websocket v2 endpoint for lower latency
      - No SSML construction — voice is set via ``speech_synthesis_voice_name``
      - Configurable timeout properties to handle LLM latency spikes
    """

    Settings = AzureTTSSettings

    def __init__(
        self,
        *,
        api_key: str,
        region: str,
        voice: Optional[str] = None,
        sample_rate: Optional[int] = None,
        settings: Optional[Settings] = None,
        text_aggregation_mode: Optional[TextAggregationMode] = None,
        frame_timeout_ms: int = 100_000_000,
        rtf_timeout_threshold: float = 10.0,
        pool_size: int = 3,
        **kwargs,
    ):
        """Initialize the Azure TTS Text Streaming service.

        Args:
            api_key: Azure Cognitive Services subscription key.
            region: Azure region identifier (e.g., "eastus", "westus2").
            voice: Voice name (e.g., "en-us-Ava:DragonHDLatestNeural").
            sample_rate: Audio sample rate in Hz. If None, uses pipeline default.
            settings: Runtime-updatable settings.
            text_aggregation_mode: How to aggregate text before synthesis.
                Defaults to TOKEN mode for lowest latency with text streaming.
            frame_timeout_ms: Frame timeout in 100-ns ticks to avoid SDK
                cancellation during LLM latency spikes.
            rtf_timeout_threshold: Real-time factor timeout threshold.
            pool_size: Number of pre-warmed, preconnected ``SpeechSynthesizer``
                instances to keep in the pool. Each turn checks one out for the
                duration of the LLM response and returns it on completion. Sized
                to absorb concurrency spikes; defaults to 3.
            **kwargs: Additional arguments passed to parent TTSService.
        """
        default_settings = self.Settings(
            model=None,
            voice=voice or "en-US-Ava:DragonHDLatestNeural",
            language="en-US",
            emphasis=None,
            pitch=None,
            rate=None,
            role=None,
            style=None,
            style_degree=None,
            volume=None,
        )

        if settings is not None:
            default_settings.apply_update(settings)

        # Default to TOKEN aggregation for lowest latency
        if text_aggregation_mode is None:
            text_aggregation_mode = TextAggregationMode.TOKEN

        super().__init__(
            text_aggregation_mode=text_aggregation_mode,
            push_text_frames=True,
            # We manage TTSStartedFrame/TTSStoppedFrame ourselves from the
            # background audio pump task to prevent the base class from
            # creating duplicate audio contexts (one empty context from
            # run_tts yielding nothing, and a second context from the pump
            # pushing frames).
            push_stop_frames=False,
            push_start_frame=False,
            pause_frame_processing=False,
            sample_rate=sample_rate,
            settings=default_settings,
            **kwargs,
        )

        self._init_azure_base(api_key=api_key, region=region)

        self._frame_timeout_ms = frame_timeout_ms
        self._rtf_timeout_threshold = rtf_timeout_threshold
        self._pool_size = max(1, pool_size)

        self._speech_config: Optional[SpeechConfig] = None
        # Pool of pre-warmed, preconnected synthesizer wrappers. Acquired per
        # turn in ``run_tts`` and returned in ``flush_audio`` /
        # ``_handle_interruption``.
        self._pool: Optional[asyncio.Queue] = None

        # Persistent TextStream state across tokens within a turn.
        # Producer side (run_tts): writes LLM tokens into _current_request
        # using the leased _active_synth.
        # Consumer side (_audio_pump_task): reads audio chunks from the
        # leased synth's audio_queue (fed by SDK callbacks) and pushes
        # frames downstream.
        self._active_synth: Optional[_PooledSynth] = None
        self._current_request: Optional[SpeechSynthesisRequest] = None
        self._current_context_id: Optional[str] = None
        self._stream_closed: bool = False
        self._audio_pump_task: Optional[asyncio.Task] = None

    def can_generate_metrics(self) -> bool:
        return True

    async def start(self, frame: StartFrame):
        """Initialize the speech config and build a pool of preconnected synthesizers."""
        await super().start(frame)

        if self._speech_config:
            return

        # IMPORTANT: Must use the websocket v2 endpoint for text streaming
        endpoint = f"wss://{self._region}.tts.speech.microsoft.com/cognitiveservices/websocket/v2"

        self._speech_config = SpeechConfig(
            endpoint=endpoint,
            subscription=self._api_key,
        )

        # Set voice directly (no SSML)
        self._speech_config.speech_synthesis_voice_name = self._settings.voice

        # Set output format
        self._speech_config.set_speech_synthesis_output_format(
            sample_rate_to_output_format(self.sample_rate)
        )

        # Set generous timeout values to avoid SDK cancellation when LLM
        # response tokens arrive slowly
        self._speech_config.set_property(
            PropertyId.SpeechSynthesis_FrameTimeoutInterval,
            str(self._frame_timeout_ms),
        )
        self._speech_config.set_property(
            PropertyId.SpeechSynthesis_RtfTimeoutThreshold,
            str(self._rtf_timeout_threshold),
        )

        # Build the synthesizer pool. Each entry is created with callbacks
        # bound to its own audio queue, then preconnected so the first turn
        # doesn't pay the websocket-handshake cost.
        self._pool = asyncio.Queue()
        for _ in range(self._pool_size):
            wrapper = self._build_pooled_synth()
            await self._pool.put(wrapper)
        logger.info(
            f"{self}: Initialized synthesizer pool with {self._pool_size} "
            f"preconnected synthesizers"
        )

    def _build_pooled_synth(self) -> _PooledSynth:
        """Create a synthesizer, wire per-wrapper callbacks, and preconnect it."""
        synth = SpeechSynthesizer(
            speech_config=self._speech_config, audio_config=None
        )
        wrapper = _PooledSynth(synth, connection=None)

        # Callbacks capture ``wrapper`` so chunks land in this wrapper's queue.
        synth.synthesizing.connect(
            lambda evt, w=wrapper: self._on_synthesizing(w, evt)
        )
        synth.synthesis_completed.connect(
            lambda evt, w=wrapper: self._on_completed(w, evt)
        )
        synth.synthesis_canceled.connect(
            lambda evt, w=wrapper: self._on_canceled(w, evt)
        )

        # Preconnect: open the websocket eagerly so the first speak_async on
        # this synth doesn't pay the handshake latency. The Connection must
        # outlive the synth, so we keep it on the wrapper.
        try:
            connection = Connection.from_speech_synthesizer(synth)
            connection.open(True)
            wrapper.connection = connection
        except Exception as e:
            logger.warning(f"{self}: Preconnect failed for pooled synth: {e}")

        return wrapper

    async def stop(self, frame: EndFrame):
        await super().stop(frame)
        await self._dispose_pool()

    async def cancel(self, frame: CancelFrame):
        await super().cancel(frame)
        await self._dispose_pool()

    async def _dispose_pool(self):
        """Drain the pool and dispose every pooled synthesizer."""
        if self._pool is None:
            return
        while not self._pool.empty():
            try:
                wrapper = self._pool.get_nowait()
            except asyncio.QueueEmpty:
                break
            wrapper.dispose()
        self._pool = None

    # -- Azure SDK callbacks (called from SDK background thread) --
    # asyncio.Queue is NOT thread-safe, so we must use
    # loop.call_soon_threadsafe() (via wrapper.enqueue_threadsafe) to safely
    # enqueue from the SDK thread.

    def _on_synthesizing(self, wrapper: _PooledSynth, evt):
        """Enqueue audio chunks as they arrive from the synthesizer."""
        if evt.result and evt.result.audio_data:
            wrapper.enqueue_threadsafe(evt.result.audio_data)

    def _on_completed(self, wrapper: _PooledSynth, evt):
        """Signal end of audio stream."""
        wrapper.enqueue_threadsafe(None)

    def _on_canceled(self, wrapper: _PooledSynth, evt):
        """Handle synthesis cancellation."""
        reason = evt.result.cancellation_details.reason
        if reason == CancellationReason.CancelledByUser:
            logger.debug(f"{self}: Text stream synthesis canceled by user (interruption)")
            wrapper.enqueue_threadsafe(None)
        else:
            details = evt.result.cancellation_details
            error_msg = f"Azure TTS text stream synthesis canceled: {reason}"
            if details.error_details:
                error_msg += f" - {details.error_details}"
            logger.error(error_msg)
            # Mark the wrapper as errored so it gets replaced on release
            # rather than re-used in a broken state.
            wrapper.errored = True
            wrapper.enqueue_threadsafe(Exception(error_msg))

    # -- Frame processing --

    async def push_frame(self, frame: Frame, direction: FrameDirection = FrameDirection.DOWNSTREAM):
        await super().push_frame(frame, direction)

    async def _audio_pump(self, wrapper: _PooledSynth, context_id: str):
        """Background consumer: drain ``wrapper.audio_queue`` and push frames downstream.

        Runs for the lifetime of one LLM turn. The producer side (``run_tts``)
        writes LLM tokens into the TextStream while this task independently
        reads audio chunks produced by the Azure SDK's background thread (via
        the wrapper's ``enqueue_threadsafe``) and forwards them as
        ``TTSAudioRawFrame``s.

        Exits when:
          - A ``None`` sentinel is read (synthesis_completed / stream closed).
          - An ``Exception`` is read (synthesis_canceled with an error).
          - The task is cancelled (interruption).
        """
        pushed_any = False
        try:
            while True:
                chunk = await wrapper.audio_queue.get()
                if chunk is None:
                    break
                if isinstance(chunk, Exception):
                    logger.error(f"{self}: Synthesis error in audio pump: {chunk}")
                    await self.push_frame(ErrorFrame(error=str(chunk)))
                    return

                # Emit TTSStartedFrame + stop TTFB on the first audio chunk.
                if not pushed_any:
                    pushed_any = True
                    await self.stop_ttfb_metrics()
                    await self.push_frame(TTSStartedFrame(context_id=context_id))

                await self.push_frame(
                    TTSAudioRawFrame(
                        audio=chunk,
                        sample_rate=self.sample_rate,
                        num_channels=1,
                        context_id=context_id,
                    )
                )

            # Normal completion: emit a matching TTSStoppedFrame.
            if pushed_any:
                await self.push_frame(TTSStoppedFrame(context_id=context_id))
        except asyncio.CancelledError:
            # Interruption path: do NOT push TTSStoppedFrame; the interruption
            # handler in the framework manages downstream frame flow so that
            # in-flight audio is dropped cleanly.
            logger.debug(f"{self}: Audio pump task cancelled")
            raise

    async def flush_audio(self, context_id: Optional[str] = None):
        """Close the persistent TextStream and wait for the audio pump to drain.

        Called by the pipecat TTSService base class at the end of an LLM
        response. We close the input stream so the SDK knows no more text is
        coming, then await the background pump task which will exit on the
        synthesis_completed sentinel after pushing all remaining audio.
        Finally the leased synthesizer is returned to the pool for the next turn.
        """
        if self._current_request is None or self._stream_closed:
            return

        logger.debug(f"{self}: Closing text stream and waiting for audio pump")
        self._stream_closed = True

        try:
            self._current_request.input_stream.close()
        except Exception as e:
            logger.warning(f"{self}: Error closing text stream: {e}")

        # Wait for the consumer task to finish draining the queue.
        pump = self._audio_pump_task
        if pump is not None:
            try:
                await asyncio.wait_for(asyncio.shield(pump), timeout=30.0)
            except asyncio.TimeoutError:
                logger.warning(
                    f"{self}: Timeout waiting for audio pump to finish; cancelling"
                )
                pump.cancel()
                try:
                    await pump
                except (asyncio.CancelledError, Exception):
                    pass
            except asyncio.CancelledError:
                # Pump was cancelled by an interruption racing with flush.
                pass
            except Exception as e:
                logger.error(f"{self}: Audio pump raised during flush: {e}")

        active = self._active_synth
        self._current_request = None
        self._current_context_id = None
        self._stream_closed = False
        self._audio_pump_task = None
        self._active_synth = None

        if active is not None:
            await self._release_synth(active)

    async def _handle_interruption(self, frame: InterruptionFrame, direction: FrameDirection):
        """Handle interruption by stopping the pump, closing the stream, and cancelling synthesis."""
        await super()._handle_interruption(frame, direction)
        await self.stop_all_metrics()

        # Cancel the consumer task FIRST so it stops pushing audio frames
        # downstream while we tear the synthesizer down.
        pump = self._audio_pump_task
        self._audio_pump_task = None
        if pump is not None and not pump.done():
            pump.cancel()
            try:
                await pump
            except (asyncio.CancelledError, Exception):
                pass

        # Close the persistent text stream so the SDK doesn't keep waiting
        # for more tokens.
        if self._current_request and not self._stream_closed:
            try:
                self._current_request.input_stream.close()
            except Exception:
                pass

        active = self._active_synth
        self._active_synth = None
        self._current_request = None
        self._current_context_id = None
        self._stream_closed = False

        if active is not None and active.synth is not None:
            try:
                result_future = active.synth.stop_speaking_async()
                await asyncio.to_thread(result_future.get)
            except Exception as e:
                logger.error(f"{self}: Error stopping synthesis on interruption: {e}")

            # Drain any leftover audio (or sentinel/exception) so the synth
            # starts the next turn with a clean queue.
            active.drain_queue()
            await self._release_synth(active)

    async def _acquire_synth(self) -> _PooledSynth:
        """Lease a synthesizer from the pool, creating a fresh one if empty."""
        if self._pool is None:
            raise RuntimeError(
                "Synthesizer pool is not initialized; start() must be called first"
            )
        try:
            wrapper = self._pool.get_nowait()
        except asyncio.QueueEmpty:
            logger.warning(
                f"{self}: Pool exhausted; creating an additional synthesizer on demand"
            )
            wrapper = self._build_pooled_synth()
        # Always start from a clean queue.
        wrapper.drain_queue()
        return wrapper

    async def _release_synth(self, wrapper: _PooledSynth):
        """Return a synthesizer to the pool, replacing it if it errored."""
        if self._pool is None:
            wrapper.dispose()
            return

        # If the synth hit a non-user synthesis error, it may be in a bad
        # state -- replace it with a fresh, preconnected one.
        if wrapper.errored or wrapper.disposed or wrapper.synth is None:
            wrapper.dispose()
            try:
                replacement = self._build_pooled_synth()
                await self._pool.put(replacement)
            except Exception as e:
                logger.error(f"{self}: Failed to replace pooled synth: {e}")
            return

        wrapper.drain_queue()
        await self._pool.put(wrapper)

    @traced_tts
    async def run_tts(self, text: str, context_id: str) -> AsyncGenerator[Frame, None]:
        """Write a text token into the persistent TextStream.

        Producer side of the producer/consumer split. With TOKEN aggregation
        mode, pipecat calls this once per LLM token. We maintain a single
        ``SpeechSynthesisRequest(TextStream)`` across the entire turn, leasing
        one ``_PooledSynth`` from the pool for the duration of the turn:

        - **First token**: leases a synthesizer from the pool, creates the
          request, calls ``speak_async``, starts the background ``_audio_pump``
          task bound to the leased synth, then writes the token.
        - **Subsequent tokens**: just writes to the existing stream.
        - ``flush_audio()`` closes the stream, awaits the pump, and returns the
          synthesizer to the pool.

        This coroutine does NOT yield audio frames -- the background pump
        task pushes audio downstream independently of token-writing cadence,
        which decouples LLM token timing from audio delivery latency.

        Args:
            text: The text token to write into the stream.
            context_id: Tracking context ID for audio frames.

        Yields:
            ErrorFrame on failure. No frames are yielded on the happy path;
            audio is pushed downstream by ``_audio_pump``.
        """
        logger.trace(f"{self}: TTS token [{text}]")

        if self._pool is None:
            logger.error(f"{self}: Synthesizer pool not initialized")
            return

        try:
            # Lease a synthesizer from the pool on the first token of the
            # turn and spin up the consumer task that drains its queue.
            is_first_token = self._current_request is None
            if is_first_token:
                wrapper = await self._acquire_synth()
                # Capture the running event loop on the wrapper so its SDK
                # callbacks can do thread-safe enqueues.
                wrapper.loop = asyncio.get_running_loop()

                self._active_synth = wrapper
                self._current_request = SpeechSynthesisRequest(
                    input_type=SpeechSynthesisRequestInputType.TextStream
                )
                wrapper.synth.speak_async(self._current_request)
                self._current_context_id = context_id
                self._stream_closed = False

                await self.start_tts_usage_metrics(text)
                await self.start_ttfb_metrics()

                # Start the consumer task. It runs concurrently with future
                # run_tts() calls and pushes audio frames as they arrive.
                self._audio_pump_task = asyncio.create_task(
                    self._audio_pump(wrapper, context_id),
                    name=f"{self.__class__.__name__}._audio_pump",
                )
                logger.debug(f"{self}: Leased pooled synth and opened TextStream for turn")

            # Write the token into the input stream. The SDK's write call is
            # a fast, non-blocking enqueue into its internal buffer, so it's
            # safe to run on the event loop thread. The audio pump task reads
            # audio concurrently in the background.
            self._current_request.input_stream.write(text)

        except Exception as e:
            logger.error(f"{self}: Text stream TTS error: {e}")
            yield ErrorFrame(error=f"Azure TTS text stream error: {e}")

            # Tear down the producer state and the consumer task so the next
            # turn starts cleanly. The leased synth is marked errored so the
            # release path replaces it with a fresh instance.
            pump = self._audio_pump_task
            self._audio_pump_task = None
            if pump is not None and not pump.done():
                pump.cancel()
                try:
                    await pump
                except (asyncio.CancelledError, Exception):
                    pass
            active = self._active_synth
            self._active_synth = None
            self._current_request = None
            self._current_context_id = None
            self._stream_closed = False
            if active is not None:
                active.errored = True
                await self._release_synth(active)
