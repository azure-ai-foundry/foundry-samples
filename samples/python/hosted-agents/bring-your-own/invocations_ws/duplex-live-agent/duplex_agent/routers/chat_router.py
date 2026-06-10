# Copyright (c) Microsoft. All rights reserved.

"""ChatRouter — STT -> Chat Completions with tools -> TTS.

For use when:
- You need a non-realtime model (gpt-4o, etc.)
- You want to control STT/TTS independently
- You're doing text-only (no audio)
"""

from __future__ import annotations

import asyncio
import json
import logging
from typing import Any, Awaitable, Callable

from duplex_agent.base import Router

logger = logging.getLogger(__name__)


class ChatRouter(Router):
    """Chat completions router with pluggable STT/TTS."""

    def __init__(
        self,
        llm_client: Any,
        model: str,
        tools: list[dict],
        system_prompt: str,
        stt: Callable[[bytes], Awaitable[str]] | None = None,
        tts: Callable[[str], Awaitable[bytes]] | None = None,
        on_text_output: Callable[[str], Awaitable[None]] | None = None,
    ):
        super().__init__(tools, system_prompt)
        self._llm = llm_client
        self._model = model
        self._stt = stt
        self._tts = tts
        self._on_text_output = on_text_output
        self._messages: list[dict] = [{"role": "system", "content": system_prompt}]
        self._speaking = False
        self._user_speaking = False
        self._disconnect_event = asyncio.Event()
        self._transport: Any = None

    async def start(self, transport: Any) -> None:
        self._transport = transport

    async def run_until_disconnect(self) -> None:
        await self._disconnect_event.wait()

    async def stop(self) -> None:
        self._disconnect_event.set()

    async def handle_user_text(self, text: str) -> None:
        """Process a user text message (from STT or direct input)."""
        self._messages.append({"role": "user", "content": text})
        await self._generate_response()

    async def handle_user_audio(self, audio: bytes) -> None:
        """Process user audio via STT then handle as text."""
        if self._stt:
            text = await self._stt(audio)
            if text:
                await self.handle_user_text(text)

    async def inject_message(self, text: str, role: str = "system") -> None:
        self._messages.append({"role": role, "content": text})
        await self._generate_response()

    def is_idle(self) -> bool:
        return not self._speaking and not self._user_speaking

    def is_speaking(self) -> bool:
        return self._speaking

    async def _generate_response(self) -> None:
        """Call the LLM with tools, handle response + tool calls in a loop."""
        self._speaking = True
        try:
            max_rounds = 10
            for _ in range(max_rounds):
                openai_tools = (
                    [{"type": "function", "function": t} for t in self._tools]
                    if self._tools
                    else None
                )
                kwargs: dict[str, Any] = {
                    "model": self._model,
                    "messages": self._messages,
                }
                if openai_tools:
                    kwargs["tools"] = openai_tools

                response = await self._llm.chat.completions.create(**kwargs)
                choice = response.choices[0]
                message = choice.message
                self._messages.append(message.model_dump())

                if message.tool_calls:
                    for tc in message.tool_calls:
                        name = tc.function.name
                        args = json.loads(tc.function.arguments or "{}")
                        if self._on_tool_call:
                            result = await self._on_tool_call(name, args)
                        else:
                            result = json.dumps({"error": f"Unknown tool: {name}"})
                        self._messages.append({
                            "role": "tool",
                            "tool_call_id": tc.id,
                            "content": result,
                        })
                    continue

                if message.content:
                    if self._on_text_output:
                        await self._on_text_output(message.content)
                    if self._tts:
                        await self._tts(message.content)
                break
        finally:
            self._speaking = False
