# Copyright (c) Microsoft. All rights reserved.

# NOTE: Intentionally duplicated with ../pipecat-ws-server/azure_fast_transcription_stt.py
# to keep each sample self-contained and deployable in isolation.
# Keep the two copies in sync when making changes.

"""Azure Fast Transcription STT service for Pipecat.

Subclasses ``SegmentedSTTService`` which buffers audio using VAD events
from the downstream context aggregator, then passes complete speech
segments to ``run_stt`` for batch transcription via the Azure Fast
Transcription REST API.

Key differences from the base ``SegmentedSTTService``:

- **No TTFB metric** — the service is non-streaming so TTFB is meaningless.
- **Processing time** measures wall-clock from VAD-end to transcript-received.
- **Non-blocking transcription** — transcription runs as a background task
  so the ``VADUserStoppedSpeakingFrame`` flows downstream immediately
  (user-stopped-speaking fires at VAD end, not after transcription).
"""

import asyncio
import io
import time
import wave
from dataclasses import dataclass
from typing import AsyncGenerator, Optional

from loguru import logger

from pipecat.frames.frames import (
    ErrorFrame,
    Frame,
    TranscriptionFrame,
    VADUserStoppedSpeakingFrame,
)
from pipecat.processors.frame_processor import FrameDirection
from pipecat.services.azure.common import language_to_azure_language
from pipecat.services.settings import STTSettings
from pipecat.services.stt_latency import AZURE_TTFS_P99
from pipecat.services.stt_service import SegmentedSTTService
from pipecat.transcriptions.language import Language
from pipecat.utils.time import time_now_iso8601
from pipecat.utils.tracing.service_decorators import traced_stt

from azure.core.credentials import AzureKeyCredential
from azure.ai.transcription.aio import TranscriptionClient
from azure.ai.transcription.models import TranscriptionContent, TranscriptionOptions


@dataclass
class AzureFastTranscriptionSTTSettings(STTSettings):
    """Settings for AzureFastTranscriptionSTTService."""

    pass


class AzureFastTranscriptionSTTService(SegmentedSTTService):
    """Segmented STT using the Azure Fast Transcription REST API.

    The parent ``SegmentedSTTService`` handles:
    - Buffering audio while the user speaks (via VAD events)
    - Packaging the buffer as WAV when speech ends
    - Calling ``run_stt()`` with the complete WAV bytes

    This subclass implements ``run_stt()`` to send the WAV to Azure
    Fast Transcription and yield the resulting ``TranscriptionFrame``.
    """

    Settings = AzureFastTranscriptionSTTSettings
    _settings: Settings

    def __init__(
        self,
        *,
        api_key: str,
        endpoint: str,
        language: Optional[Language] = Language.EN_US,
        sample_rate: Optional[int] = None,
        settings: Optional[Settings] = None,
        ttfs_p99_latency: Optional[float] = AZURE_TTFS_P99,
        **kwargs,
    ):
        """
        Args:
            api_key: Azure Speech / Foundry resource key.
            endpoint: Resource endpoint, e.g.
                ``https://<region>.api.cognitive.microsoft.com/``.
            language: BCP-47 locale for recognition (default ``en-US``).
            sample_rate: Audio sample rate in Hz. If *None*, determined
                from the pipeline start frame.
        """
        default_settings = self.Settings(
            model=None,
            language=Language.EN_US,
        )

        if language is not None and language != Language.EN_US:
            self._warn_init_param_moved_to_settings("language", "language")
            default_settings.language = language

        if settings is not None:
            default_settings.apply_update(settings)

        super().__init__(
            sample_rate=sample_rate,
            ttfs_p99_latency=ttfs_p99_latency,
            settings=default_settings,
            **kwargs,
        )

        if not api_key:
            raise ValueError("api_key is required for Azure Fast Transcription")
        if not endpoint:
            raise ValueError("endpoint is required for Azure Fast Transcription")

        self._client = TranscriptionClient(
            endpoint=endpoint,
            credential=AzureKeyCredential(api_key),
        )
        self._debug_audio_count = 0

    def can_generate_metrics(self) -> bool:
        return True

    async def start(self, frame):
        await super().start(frame)
        logger.debug(
            f"AzureFastTranscriptionSTT started: sample_rate={self.sample_rate}, "
            f"audio_passthrough={self._audio_passthrough}, "
            f"buffer_size_1s={self._audio_buffer_size_1s}"
        )

    async def stop(self, frame):
        await super().stop(frame)
        await self._close_client()

    async def cancel(self, frame):
        await super().cancel(frame)
        await self._close_client()

    async def _close_client(self):
        client = self._client
        if client is None:
            return
        self._client = None
        try:
            await client.close()
        except Exception as e:
            logger.warning(f"Error closing Azure transcription client: {e}")

    async def process_frame(self, frame, direction):
        from pipecat.frames.frames import (
            AudioRawFrame,
            UserStoppedSpeakingFrame,
            VADUserStartedSpeakingFrame,
            VADUserStoppedSpeakingFrame,
        )

        if isinstance(frame, AudioRawFrame):
            self._debug_audio_count += 1
            if self._debug_audio_count <= 3 or self._debug_audio_count % 50 == 0:
                logger.trace(
                    f"[DBG] AudioFrame #{self._debug_audio_count} dir={direction} "
                    f"len={len(frame.audio)} buf={len(self._audio_buffer)} "
                    f"speaking={self._user_speaking}"
                )
        elif isinstance(frame, VADUserStartedSpeakingFrame):
            logger.debug(f"[DBG] VAD START dir={direction}")
        elif isinstance(frame, VADUserStoppedSpeakingFrame):
            logger.debug(f"[DBG] VAD STOP dir={direction}")
            # Push UserStoppedSpeakingFrame downstream immediately at VAD end
            # so the RTVI "user-stopped-speaking" event fires without waiting
            # for transcription.  The turn stop strategy in the aggregator has
            # enable_user_speaking_frames=False to avoid a duplicate.
            await self.push_frame(
                UserStoppedSpeakingFrame(), FrameDirection.DOWNSTREAM
            )

        await super().process_frame(frame, direction)

    # ------------------------------------------------------------------
    # Disable TTFB metrics (non-streaming service, TTFB is meaningless)
    # ------------------------------------------------------------------

    async def _handle_vad_user_stopped_speaking(self, frame: VADUserStoppedSpeakingFrame):
        """Override to skip TTFB metric start — only set _user_speaking."""
        self._user_speaking = False

    async def push_frame(self, frame, direction=FrameDirection.DOWNSTREAM):
        """Override to skip TTFB metric stop on TranscriptionFrame."""
        if isinstance(frame, TranscriptionFrame):
            frame.finalized = True
        # Bypass STTService.push_frame (which does TTFB tracking) and call
        # the grandparent FrameProcessor.push_frame directly via
        # SegmentedSTTService's grandparent.
        await super(SegmentedSTTService, self).push_frame(frame, direction)

    # ------------------------------------------------------------------
    # Non-blocking transcription: run_stt as background task so
    # VADUserStoppedSpeakingFrame flows downstream immediately.
    # Processing time = VAD end → transcript received.
    # ------------------------------------------------------------------

    async def _handle_user_stopped_speaking(self, frame: VADUserStoppedSpeakingFrame):
        """Package audio, start processing timer, and kick off transcription
        as a background task so the pipeline is not blocked."""
        self._user_speaking = False

        # Start processing metrics now (VAD end).
        await self.start_processing_metrics()

        # Package buffered audio as WAV.
        content = io.BytesIO()
        wf = wave.open(content, "wb")
        wf.setsampwidth(2)
        wf.setnchannels(1)
        wf.setframerate(self.sample_rate)
        wf.writeframes(self._audio_buffer)
        wf.close()
        content.seek(0)
        self._audio_buffer.clear()

        audio_data = content.read()

        # Run transcription in background so the VAD stop frame continues
        # downstream immediately (user-stopped-speaking fires at VAD end).
        self.create_task(self._transcribe_and_push(audio_data))

    async def _transcribe_and_push(self, audio: bytes):
        """Background task: transcribe audio and push result frames."""
        async for frame in self.run_stt(audio):
            await self.push_frame(frame)

    def language_to_service_language(self, language: Language) -> Optional[str]:
        """Convert a Language enum to Azure's BCP-47 locale."""
        return language_to_azure_language(language)

    @traced_stt
    async def _handle_transcription(
        self, transcript: str, is_final: bool, language: Optional[str] = None
    ):
        """Handle a transcription result with tracing."""
        await self.stop_processing_metrics()

    async def run_stt(self, audio: bytes) -> AsyncGenerator[Frame, None]:
        """Transcribe a complete speech segment via Azure Fast Transcription.

        Uses the async ``azure.ai.transcription.aio.TranscriptionClient`` so
        the HTTP request is awaited natively on the event loop instead of
        being shoved onto a worker thread via ``asyncio.to_thread``.

        Args:
            audio: WAV-encoded audio bytes (produced by SegmentedSTTService).

        Yields:
            TranscriptionFrame on success, ErrorFrame on failure.
        """
        try:
            result = await self._transcribe_async(audio)
            text = self._extract_text(result)

            if text:
                language = self._settings.language or language_to_azure_language(Language.EN_US)
                await self._handle_transcription(text, True, language)
                logger.debug(f"Transcription: [{text}]")
                yield TranscriptionFrame(
                    text,
                    self._user_id,
                    time_now_iso8601(),
                    language,
                    result=result,
                )
            else:
                await self.stop_processing_metrics()
                logger.debug("Azure Fast Transcription returned no transcript")
        except Exception as e:
            await self.stop_processing_metrics()
            logger.error(f"Azure Fast Transcription error: {e}")
            yield ErrorFrame(error=f"Azure Fast Transcription error: {e}")

    async def _transcribe_async(self, wav_bytes: bytes):
        locale = self._settings.language or language_to_azure_language(Language.EN_US)
        audio_file = ("audio.wav", io.BytesIO(wav_bytes), "audio/wav")
        t0 = time.monotonic()
        result = await self._client.transcribe(
            TranscriptionContent(
                definition=TranscriptionOptions(locales=[locale]),
                audio=audio_file,
            )
        )
        elapsed_ms = (time.monotonic() - t0) * 1000
        logger.info(f"_transcribe_async took {elapsed_ms:.0f}ms (audio {len(wav_bytes)} bytes)")
        return result

    @staticmethod
    def _extract_text(result) -> str:
        combined_phrases = getattr(result, "combined_phrases", None) or []
        text = " ".join(
            phrase.text.strip()
            for phrase in combined_phrases
            if getattr(phrase, "text", None) and phrase.text.strip()
        ).strip()
        if text:
            return text

        phrases = getattr(result, "phrases", None) or []
        return " ".join(
            phrase.text.strip()
            for phrase in phrases
            if getattr(phrase, "text", None) and phrase.text.strip()
        ).strip()
