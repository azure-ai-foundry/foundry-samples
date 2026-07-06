# Copyright (c) Microsoft. All rights reserved.

"""RealtimeRouter — Voice Live gpt-realtime with function calling.

Uses the Realtime API's native audio I/O + session tools for task dispatch.
Function calls arrive as response output items with type=function_call.
"""

from __future__ import annotations

import asyncio
import base64
import json
import logging
import struct
from typing import Any, Optional

from starlette.websockets import WebSocket, WebSocketState

from azure.ai.voicelive.aio import connect as voicelive_connect
from azure.ai.voicelive.models import (
    AudioEchoCancellation,
    AudioInputTranscriptionOptions,
    AudioNoiseReduction,
    AzureStandardVoice,
    FunctionCallOutputItem,
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
from azure.identity.aio import DefaultAzureCredential

from duplex_agent.base import Router

logger = logging.getLogger(__name__)
_rt_logger = logging.getLogger("duplex_agent.realtime_io")


class RealtimeRouter(Router):
    """Voice Live gpt-realtime router with function calling for task dispatch."""

    def __init__(
        self,
        endpoint: str,
        model: str,
        tools: list[dict],
        system_prompt: str,
        voice: str = "en-US-Ava:DragonHDLatestNeural",
    ):
        super().__init__(tools, system_prompt)
        self._endpoint = endpoint
        self._model = model
        self._voice = voice
        self._connection_cm = None
        self._connection = None
        self._credential: DefaultAzureCredential | None = None
        self._websocket: WebSocket | None = None
        self._response_in_progress = False
        self._user_speaking = False
        self._bot_audio_end: float = 0.0
        self._last_user_event: float = 0.0

    async def start(self, transport: WebSocket) -> None:
        self._websocket = transport
        self._credential = DefaultAzureCredential()
        self._connection_cm = voicelive_connect(
            endpoint=self._endpoint,
            credential=self._credential,
            model=self._model,
        )
        self._connection = await self._connection_cm.__aenter__()

        # Build voice config
        voice_config: Any
        if "-" in self._voice:
            voice_config = AzureStandardVoice(name=self._voice)
        else:
            voice_config = self._voice

        # Build session tools from self._tools (function schemas)
        session_tools = []
        for t in self._tools:
            tool_def = {
                "type": "function",
                "name": t["name"],
                "description": t.get("description", ""),
                "parameters": t.get("parameters", {}),
            }
            session_tools.append(tool_def)

        session = RequestSession(
            modalities=[Modality.TEXT, Modality.AUDIO],
            instructions=self._system_prompt,
            voice=voice_config,
            input_audio_format=InputAudioFormat.PCM16,
            output_audio_format=OutputAudioFormat.PCM16,
            input_audio_transcription=AudioInputTranscriptionOptions(model="azure-speech"),
            turn_detection=ServerVad(
                threshold=0.5,
                prefix_padding_ms=300,
                silence_duration_ms=500,
            ),
            input_audio_echo_cancellation=AudioEchoCancellation(),
            input_audio_noise_reduction=AudioNoiseReduction(
                type="azure_deep_noise_suppression",
            ),
            tools=session_tools,
        )
        _rt_logger.debug(
            "SESSION SETUP: model=%s voice=%s tools=%s",
            self._model, self._voice, [t["name"] for t in session_tools],
        )
        _rt_logger.debug("SYSTEM PROMPT: %s", self._system_prompt[:500])
        await self._connection.session.update(session=session)

    async def run_until_disconnect(self) -> None:
        """Run the browser<->VoiceLive bridge with function call handling."""
        forward = asyncio.create_task(self._browser_to_voicelive(), name="duplex-b2vl")
        backward = asyncio.create_task(self._voicelive_to_browser(), name="duplex-vl2b")
        done, pending = await asyncio.wait(
            {forward, backward}, return_when=asyncio.FIRST_COMPLETED
        )
        for t in pending:
            t.cancel()
        for t in pending:
            try:
                await t
            except (asyncio.CancelledError, Exception):
                pass
        for t in done:
            try:
                exc = t.exception()
            except asyncio.CancelledError:
                continue
            if exc:
                logger.error("RealtimeRouter task %s failed: %s", t.get_name(), exc)

    async def stop(self) -> None:
        if self._connection_cm:
            await self._connection_cm.__aexit__(None, None, None)
            self._connection_cm = None
            self._connection = None
        elif self._connection:
            await self._connection.close()
            self._connection = None
        if self._credential:
            close = getattr(self._credential, "close", None)
            if close:
                result = close()
                if asyncio.iscoroutine(result):
                    await result
            self._credential = None

    async def inject_message(self, text: str, role: str = "system") -> None:
        """Inject system message -> triggers LLM to speak the update."""
        if not self._connection:
            return
        _rt_logger.debug("INJECT [%s]: %s", role, text[:300])
        await self._connection.conversation.item.create(
            item=MessageItem(
                role=role,
                content=[InputTextContentPart(text=text)],
            ),
        )
        await self._connection.response.create()

    def is_idle(self) -> bool:
        now = asyncio.get_event_loop().time()
        bot_done = now > self._bot_audio_end
        return not self._response_in_progress and bot_done and not self._user_speaking

    def is_speaking(self) -> bool:
        return self._response_in_progress

    # ------------------------------------------------------------------
    # Internal: browser <-> Voice Live bridge
    # ------------------------------------------------------------------

    async def _safe_send_json(self, payload: dict) -> bool:
        ws = self._websocket
        if not ws or ws.application_state == WebSocketState.DISCONNECTED:
            return False
        try:
            await ws.send_json(payload)
            return True
        except Exception:
            return False

    async def _safe_send_bytes(self, data: bytes) -> bool:
        ws = self._websocket
        if not ws or ws.application_state == WebSocketState.DISCONNECTED:
            return False
        try:
            await ws.send_bytes(data)
            return True
        except Exception:
            return False

    async def _browser_to_voicelive(self) -> None:
        """Pump browser audio/text into Voice Live."""
        ws = self._websocket
        while True:
            msg = await ws.receive()
            if msg.get("type") == "websocket.disconnect":
                return
            data: Optional[bytes] = msg.get("bytes")
            if data:
                audio_b64 = base64.b64encode(data).decode("ascii")
                await self._connection.input_audio_buffer.append(audio=audio_b64)
                continue
            text = msg.get("text")
            if not text:
                continue
            try:
                payload = json.loads(text)
            except json.JSONDecodeError:
                continue
            if payload.get("type") == "text":
                content = payload.get("content", "")
                if content:
                    _rt_logger.debug("USER TEXT INPUT: %s", content[:300])
                    await self._connection.conversation.item.create(
                        item=UserMessageItem(
                            content=[InputTextContentPart(text=content)],
                        ),
                    )
                    await self._connection.response.create()

    async def _voicelive_to_browser(self) -> None:
        """Pump Voice Live events to browser + handle function calls."""
        greeting_sent = False
        async for event in self._connection:
            et = event.type
            _rt_logger.debug("SERVER EVENT: %s", et)

            if et == ServerEventType.SESSION_UPDATED:
                await self._safe_send_json(
                    {"type": "session_started", "session_id": event.session.id}
                )
                if not greeting_sent:
                    greeting_sent = True
                    await self.inject_message(
                        "Greet the user warmly in one short sentence. "
                        "Mention you can do research and other tasks in the "
                        "background while chatting."
                    )

            elif et == ServerEventType.INPUT_AUDIO_BUFFER_SPEECH_STARTED:
                self._user_speaking = True
                self._last_user_event = asyncio.get_event_loop().time()
                await self._safe_send_json({"type": "user_speech_started"})

            elif et == ServerEventType.INPUT_AUDIO_BUFFER_SPEECH_STOPPED:
                self._user_speaking = False
                self._last_user_event = asyncio.get_event_loop().time()
                await self._safe_send_json({"type": "user_speech_stopped"})

            elif et == ServerEventType.RESPONSE_CREATED:
                self._response_in_progress = True

            elif et == ServerEventType.RESPONSE_AUDIO_DELTA:
                pcm = event.delta or b""
                if not pcm:
                    continue
                duration = len(pcm) / 2.0 / 24000.0
                now = asyncio.get_event_loop().time()
                self._bot_audio_end = max(self._bot_audio_end, now) + duration
                header = struct.pack("<II", 24000, 1)
                await self._safe_send_bytes(header + pcm)

            elif et == ServerEventType.RESPONSE_AUDIO_TRANSCRIPT_DELTA:
                delta = getattr(event, "delta", "") or ""
                if delta:
                    await self._safe_send_json(
                        {"type": "bot_text", "delta": delta, "final": False}
                    )

            elif et == ServerEventType.RESPONSE_AUDIO_TRANSCRIPT_DONE:
                bot_transcript = getattr(event, "transcript", "") or ""
                _rt_logger.debug("BOT RESPONSE TRANSCRIPT: %s", bot_transcript[:300])
                await self._safe_send_json({
                    "type": "bot_text",
                    "delta": "",
                    "final": True,
                    "text": bot_transcript,
                })

            elif et == ServerEventType.CONVERSATION_ITEM_INPUT_AUDIO_TRANSCRIPTION_COMPLETED:
                transcript = getattr(event, "transcript", "") or ""
                _rt_logger.debug("USER SPEECH TRANSCRIPT: %s", transcript)
                await self._safe_send_json({
                    "type": "transcription",
                    "text": transcript,
                    "final": True,
                })

            elif et == ServerEventType.RESPONSE_DONE:
                self._response_in_progress = False
                await self._safe_send_json({"type": "response_done"})
                await self._handle_function_calls(event)

            elif et == ServerEventType.ERROR:
                err = getattr(event, "error", None)
                await self._safe_send_json({
                    "type": "error",
                    "message": getattr(err, "message", str(err)),
                })

    async def _handle_function_calls(self, response_done_event) -> None:
        """Process function calls from the completed response."""
        response = getattr(response_done_event, "response", None)
        if not response:
            return
        output_items = getattr(response, "output", None) or []
        for item in output_items:
            if getattr(item, "type", None) != "function_call":
                continue
            call_id = item.call_id
            name = item.name
            try:
                arguments = json.loads(item.arguments or "{}")
            except json.JSONDecodeError:
                arguments = {}

            _rt_logger.debug("FUNCTION CALL: %s(%s)", name, json.dumps(arguments)[:200])

            if self._on_tool_call:
                result = await self._on_tool_call(name, arguments)
            else:
                result = json.dumps({"error": f"No handler for tool: {name}"})

            _rt_logger.debug("FUNCTION RESULT: %s -> %s", name, result[:300])

            await self._connection.conversation.item.create(
                item=FunctionCallOutputItem(call_id=call_id, output=result),
            )
            await self._connection.response.create()
