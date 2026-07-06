# Copyright (c) Microsoft. All rights reserved.

"""Voice Live Hello World — Bring Your Own ``invocations_ws`` agent.

A minimal hosted agent that bridges a browser WebSocket connection to the
Azure Voice Live service. The Invocations SDK
(``azure-ai-agentserver-invocations``) handles the ``/invocations_ws``
route, OTel tracing, and keep-alive pings; the user-supplied
``@app.ws_handler`` opens a Voice Live session and shuttles audio frames
in both directions.

Wire format with the browser (matches the ``./chat_client`` decoder):

* Browser → Server (binary): raw PCM16 mic chunks, ``BROWSER_SAMPLE_RATE``
  Hz mono.  Voice Live's native rate is 24 kHz so the browser is set to
  send 24 kHz directly and the server forwards bytes verbatim.
* Browser → Server (text JSON): ``{"type": "text", "content": "..."}`` —
  forwarded to Voice Live as a user text item + ``response.create``.
* Server → Browser (binary): 8-byte little-endian header
  ``(sample_rate u32, num_channels u32)`` followed by PCM16 audio. The
  shared chat-client decoder picks up the header and creates an
  ``AudioContext`` at the matching rate.
* Server → Browser (text JSON): control events
  (``session_started``, ``user_speech_started`` / ``stopped``,
  ``transcription``, ``bot_text``, ``error``).

Endpoint resolution:

* In Foundry-hosted runs, ``FOUNDRY_PROJECT_ENDPOINT`` is auto-injected
  (e.g. ``https://<acct>.services.ai.azure.com/api/projects/<proj>``);
  this sample strips the ``/api/projects/...`` suffix to get the bare
  AI-Services account URL that the Voice Live SDK expects.
* For local runs, set ``AZURE_VOICELIVE_ENDPOINT`` (account URL or
  project URL — both work) in ``.env``.

Other environment variables:

    AZURE_VOICELIVE_MODEL         — Realtime model (declared in
                                    ``agent.manifest.yaml``).
    AZURE_VOICELIVE_VOICE         — Default ``en-US-Ava:DragonHDLatestNeural``.
    AZURE_VOICELIVE_INSTRUCTIONS  — System prompt.

Authentication is always ``DefaultAzureCredential``: locally via
``az login``; in Foundry via the hosted agent's managed identity.

Run locally::

    az login
    export AZURE_VOICELIVE_ENDPOINT=https://<account>.services.ai.azure.com/
    python main.py
"""

from __future__ import annotations

import asyncio
import base64
import json
import logging
import os
import struct
from typing import Optional
from urllib.parse import urlsplit, urlunsplit

from starlette.websockets import WebSocket, WebSocketDisconnect, WebSocketState

from azure.ai.agentserver.invocations import InvocationAgentServerHost

from azure.identity.aio import DefaultAzureCredential

from azure.ai.voicelive.aio import connect as voicelive_connect
from azure.ai.voicelive.models import (
    AudioEchoCancellation,
    AudioInputTranscriptionOptions,
    AudioNoiseReduction,
    AzureStandardVoice,
    InputAudioFormat,
    InputTextContentPart,
    MessageItem,
    Modality,
    OutputAudioFormat,
    RequestSession,
    ServerEventType,
    ServerVad,
    UserMessageItem,
)

try:
    from dotenv import load_dotenv
except ImportError:  # pragma: no cover - python-dotenv is in requirements.txt
    def load_dotenv(*_a, **_kw):  # type: ignore[misc]
        return False

# Load .env for local dev. In Foundry-hosted runs there is no .env file and
# real env vars take precedence (override=False).
load_dotenv(override=False)


logger = logging.getLogger("hello-world")
logging.basicConfig(
    level=os.environ.get("LOG_LEVEL", "INFO"),
    format="%(asctime)s %(name)s %(levelname)s: %(message)s",
)


# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------

# Voice Live's native realtime audio is PCM16 24 kHz mono. We forward bytes
# verbatim in both directions, so the browser must capture/play at 24 kHz too.
BROWSER_SAMPLE_RATE = 24_000
BROWSER_CHANNELS = 1

DEFAULT_VOICE = "en-US-Ava:DragonHDLatestNeural"
DEFAULT_INSTRUCTIONS = (
    "You are a friendly, concise voice assistant. "
    "Greet the user warmly on the first turn, then keep replies short — "
    "this is a real-time voice conversation."
)


def _resolve_endpoint() -> str:
    """Return the Voice Live AI-Services account URL.

    Prefers the Foundry-injected ``FOUNDRY_PROJECT_ENDPOINT`` (a project
    URL of the form ``https://<acct>.services.ai.azure.com/api/projects/<proj>``);
    falls back to ``AZURE_VOICELIVE_ENDPOINT`` for local runs. Either form
    is accepted — the ``/api/projects/...`` path is stripped because the
    Voice Live SDK builds its own ``/voice-live/...`` paths from the
    account root.
    """
    raw = (
        os.environ.get("FOUNDRY_PROJECT_ENDPOINT", "").strip()
        or os.environ.get("AZURE_VOICELIVE_ENDPOINT", "").strip()
    )
    if not raw:
        raise EnvironmentError(
            "Neither FOUNDRY_PROJECT_ENDPOINT (auto-injected in hosted "
            "containers) nor AZURE_VOICELIVE_ENDPOINT (set locally) is "
            "present. Set one to your AI Services / Foundry endpoint, "
            "e.g. 'https://<account>.services.ai.azure.com/'."
        )
    parts = urlsplit(raw)
    if parts.scheme not in ("http", "https") or not parts.netloc:
        raise EnvironmentError(
            f"Invalid Voice Live endpoint {raw!r}: expected an absolute URL "
            "like 'https://<account>.services.ai.azure.com/' or a Foundry "
            "project URL of the form "
            "'https://<account>.services.ai.azure.com/api/projects/<proj>'."
        )
    return urlunsplit((parts.scheme, parts.netloc, "/", "", ""))


VOICE_LIVE_ENDPOINT = _resolve_endpoint()
VOICE_LIVE_MODEL = os.environ.get("AZURE_VOICELIVE_MODEL", "gpt-realtime").strip()
VOICE_LIVE_VOICE = os.environ.get("AZURE_VOICELIVE_VOICE", DEFAULT_VOICE).strip()
VOICE_LIVE_INSTRUCTIONS = os.environ.get(
    "AZURE_VOICELIVE_INSTRUCTIONS", DEFAULT_INSTRUCTIONS
).strip() or DEFAULT_INSTRUCTIONS

# Seconds of silence (no user speech, and no bot audio still playing back)
# after which the agent proactively re-engages. Silence is measured using
# the actual queued bot audio duration, not the ``response.done`` event,
# because audio keeps playing in the browser after the server finishes
# streaming. Set to ``0`` to disable.
try:
    IDLE_ENGAGEMENT_SECONDS = float(
        os.environ.get("AZURE_VOICELIVE_IDLE_ENGAGEMENT_SECONDS", "20")
    )
except ValueError:
    IDLE_ENGAGEMENT_SECONDS = 0.0


# ---------------------------------------------------------------------------
# Browser <-> Voice Live bridge
# ---------------------------------------------------------------------------

def _audio_frame(pcm: bytes, sample_rate: int = 24_000, channels: int = 1) -> bytes:
    """Pack PCM16 bytes with the 8-byte ``(sample_rate, channels)`` header."""
    return struct.pack("<II", sample_rate, channels) + pcm


class _IdleState:
    """Track real silence for idle re-engagement.

    ``bot_audio_end`` is the wall-clock (event-loop time) at which the
    audio streamed to the browser is expected to finish playing,
    accumulated from RESPONSE_AUDIO_DELTA byte counts at 24 kHz PCM16.
    ``last_user_event`` is bumped on any user speech activity. Effective
    silence-start is the later of the two.
    """

    def __init__(self) -> None:
        now = asyncio.get_event_loop().time()
        self.last_user_event = now
        self.bot_audio_end = now
        self.response_in_progress = False

    def mark_user_active(self) -> None:
        self.last_user_event = asyncio.get_event_loop().time()

    def add_bot_audio(self, pcm_bytes: int, sample_rate: int = 24_000) -> None:
        now = asyncio.get_event_loop().time()
        # PCM16 mono => 2 bytes per sample.
        duration = pcm_bytes / 2.0 / float(sample_rate)
        self.bot_audio_end = max(self.bot_audio_end, now) + duration

    def idle_seconds(self) -> float:
        now = asyncio.get_event_loop().time()
        baseline = max(self.last_user_event, self.bot_audio_end)
        return max(0.0, now - baseline)

    def reset(self) -> None:
        self.last_user_event = asyncio.get_event_loop().time()


def _build_voice_config():
    v = VOICE_LIVE_VOICE
    if "-" in v:  # Azure voice e.g. en-US-Ava:DragonHDLatestNeural
        return AzureStandardVoice(name=v)
    return v  # OpenAI voice (alloy, echo, ...)


async def _build_session() -> RequestSession:
    return RequestSession(
        modalities=[Modality.TEXT, Modality.AUDIO],
        instructions=VOICE_LIVE_INSTRUCTIONS,
        voice=_build_voice_config(),
        input_audio_format=InputAudioFormat.PCM16,
        output_audio_format=OutputAudioFormat.PCM16,
        # Transcribe the user's mic input via Azure Speech so the browser
        # gets `transcription` events alongside the bot's audio.
        input_audio_transcription=AudioInputTranscriptionOptions(model="azure-speech"),
        turn_detection=ServerVad(
            threshold=0.5,
            prefix_padding_ms=300,
            silence_duration_ms=500,
        ),
        input_audio_echo_cancellation=AudioEchoCancellation(),
        input_audio_noise_reduction=AudioNoiseReduction(
            type="azure_deep_noise_suppression"
        ),
    )


async def _safe_send_json(ws: WebSocket, payload: dict) -> bool:
    """Best-effort JSON send; returns False if the socket is gone."""
    if ws.application_state == WebSocketState.DISCONNECTED:
        return False
    try:
        await ws.send_json(payload)
        return True
    except Exception:
        return False


async def _safe_send_bytes(ws: WebSocket, data: bytes) -> bool:
    if ws.application_state == WebSocketState.DISCONNECTED:
        return False
    try:
        await ws.send_bytes(data)
        return True
    except Exception:
        return False


async def _browser_to_voicelive(
    websocket: WebSocket,
    connection,  # azure.ai.voicelive.aio.VoiceLiveConnection
) -> None:
    """Pump browser frames into the Voice Live connection."""
    while True:
        msg = await websocket.receive()
        msg_type = msg.get("type")
        if msg_type == "websocket.disconnect":
            return
        # Binary mic chunk → input_audio_buffer.append
        data: Optional[bytes] = msg.get("bytes")
        if data:
            audio_b64 = base64.b64encode(data).decode("ascii")
            await connection.input_audio_buffer.append(audio=audio_b64)
            continue
        # Text JSON
        text = msg.get("text")
        if not text:
            continue
        try:
            payload = json.loads(text)
        except json.JSONDecodeError:
            continue
        kind = payload.get("type")
        if kind == "text":
            content = payload.get("content", "")
            if not content:
                continue
            # Append a user text item, then trigger a response.
            await connection.conversation.item.create(
                item=UserMessageItem(
                    content=[InputTextContentPart(text=content)],
                ),
            )
            await connection.response.create()
        else:
            logger.debug("Ignored browser text frame: %r", payload)


async def _voicelive_to_browser(
    websocket: WebSocket,
    connection,
    state: _IdleState,
) -> None:
    """Pump Voice Live events out to the browser."""
    greeting_sent = False
    async for event in connection:
        et = event.type

        if et == ServerEventType.SESSION_UPDATED:
            await _safe_send_json(
                websocket,
                {"type": "session_started", "session_id": event.session.id},
            )
            # Proactive welcome: once the session is ready, tell the LLM
            # to greet the user so the bot speaks first.
            # https://learn.microsoft.com/azure/ai-services/speech-service/how-to-voice-live-proactive-messages
            if not greeting_sent:
                greeting_sent = True
                try:
                    await connection.conversation.item.create(
                        item=MessageItem(
                            role="system",
                            content=[InputTextContentPart(
                                text="Greet the user warmly in one short sentence "
                                     "and invite them to ask a question.",
                            )],
                        ),
                    )
                    await connection.response.create()
                    logger.info("Sent proactive greeting request")
                except Exception:  # noqa: BLE001
                    logger.exception("Failed to send proactive greeting")

        elif et == ServerEventType.INPUT_AUDIO_BUFFER_SPEECH_STARTED:
            state.mark_user_active()
            await _safe_send_json(websocket, {"type": "user_speech_started"})

        elif et == ServerEventType.INPUT_AUDIO_BUFFER_SPEECH_STOPPED:
            state.mark_user_active()
            await _safe_send_json(websocket, {"type": "user_speech_stopped"})

        elif et == ServerEventType.RESPONSE_CREATED:
            state.response_in_progress = True

        elif et == ServerEventType.RESPONSE_AUDIO_DELTA:
            # The SDK already base64-decodes the audio delta (rest_field
            # format="base64"), so event.delta is raw PCM16 bytes at the
            # session output rate (24 kHz mono).
            pcm = event.delta or b""
            if not pcm:
                continue
            # Track real playback duration so idle detection waits until
            # the bot has actually stopped speaking in the browser.
            state.add_bot_audio(len(pcm))
            await _safe_send_bytes(websocket, _audio_frame(pcm))

        elif et == ServerEventType.RESPONSE_AUDIO_TRANSCRIPT_DELTA:
            delta = getattr(event, "delta", "") or ""
            if delta:
                await _safe_send_json(
                    websocket,
                    {"type": "bot_text", "delta": delta, "final": False},
                )

        elif et == ServerEventType.RESPONSE_AUDIO_TRANSCRIPT_DONE:
            await _safe_send_json(
                websocket,
                {
                    "type": "bot_text",
                    "delta": "",
                    "final": True,
                    "text": getattr(event, "transcript", "") or "",
                },
            )

        elif et == ServerEventType.CONVERSATION_ITEM_INPUT_AUDIO_TRANSCRIPTION_COMPLETED:
            await _safe_send_json(
                websocket,
                {
                    "type": "transcription",
                    "text": getattr(event, "transcript", "") or "",
                    "final": True,
                },
            )

        elif et == ServerEventType.RESPONSE_DONE:
            state.response_in_progress = False
            await _safe_send_json(websocket, {"type": "response_done"})

        elif et == ServerEventType.ERROR:
            err = getattr(event, "error", None)
            await _safe_send_json(
                websocket,
                {
                    "type": "error",
                    "message": getattr(err, "message", str(err)),
                    "code": getattr(err, "code", None),
                },
            )

        else:
            logger.debug("Voice Live event: %s", et)


async def _idle_engagement_watcher(
    connection,
    state: _IdleState,
    idle_seconds: float,
) -> None:
    """Periodically check for prolonged silence and re-engage the user.

    Silence is the gap since the later of the last user speech event and
    the projected end of any queued bot audio (computed from
    RESPONSE_AUDIO_DELTA byte counts, not RESPONSE_DONE \u2014 the browser
    keeps playing audio after the server finishes streaming).
    """
    if idle_seconds <= 0:
        return
    poll_interval = max(1.0, min(5.0, idle_seconds / 4.0))
    while True:
        await asyncio.sleep(poll_interval)
        if state.response_in_progress:
            continue
        if state.idle_seconds() < idle_seconds:
            continue
        try:
            await connection.conversation.item.create(
                item=MessageItem(
                    role="system",
                    content=[InputTextContentPart(
                        text=(
                            "The user has been silent for a while. Re-engage "
                            "them with one short, friendly sentence \u2014 ask "
                            "if they're still there or offer a topic to "
                            "explore."
                        ),
                    )],
                ),
            )
            await connection.response.create()
            logger.info(
                "Sent idle re-engagement (idle=%.1fs, threshold=%.1fs)",
                state.idle_seconds(), idle_seconds,
            )
            # Avoid immediately re-firing; the response itself will extend
            # bot_audio_end as deltas arrive.
            state.reset()
        except Exception:  # noqa: BLE001
            logger.exception("Failed to send idle engagement")


# ---------------------------------------------------------------------------
# Server
# ---------------------------------------------------------------------------

app = InvocationAgentServerHost()


@app.ws_handler
async def handle_ws(websocket: WebSocket) -> None:
    """Per-connection bridge to a fresh Voice Live session.

    The SDK calls ``await websocket.accept()`` for us before invoking this
    handler and cleanly closes the socket on return.
    """
    credential = DefaultAzureCredential()
    try:
        async with voicelive_connect(
            endpoint=VOICE_LIVE_ENDPOINT,
            credential=credential,
            model=VOICE_LIVE_MODEL,
        ) as connection:
            await connection.session.update(session=await _build_session())

            state = _IdleState()
            forward = asyncio.create_task(
                _browser_to_voicelive(websocket, connection),
                name="b2vl",
            )
            backward = asyncio.create_task(
                _voicelive_to_browser(websocket, connection, state),
                name="vl2b",
            )
            idle_watch = asyncio.create_task(
                _idle_engagement_watcher(
                    connection, state, IDLE_ENGAGEMENT_SECONDS,
                ),
                name="idle",
            )
            done, pending = await asyncio.wait(
                {forward, backward, idle_watch},
                return_when=asyncio.FIRST_COMPLETED,
            )
            for task in pending:
                task.cancel()
            for task in pending:
                try:
                    await task
                except (asyncio.CancelledError, Exception):  # noqa: BLE001
                    pass
            for task in done:
                exc = task.exception()
                if exc and not isinstance(exc, WebSocketDisconnect):
                    logger.error("WS bridge task %s failed: %s", task.get_name(), exc)
    except WebSocketDisconnect:
        return
    finally:
        # ``credential`` is owned per-connection; close async credentials.
        close = getattr(credential, "close", None)
        if close is not None:
            try:
                result = close()
                if asyncio.iscoroutine(result):
                    await result
            except Exception:
                logger.debug("Credential close failed", exc_info=True)


if __name__ == "__main__":
    app.run()
