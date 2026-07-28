# Copyright (c) Microsoft. All rights reserved.
"""Copilot SDK harness for the assistant.

"""

from __future__ import annotations

import logging
import os
import uuid

from azure.identity import DefaultAzureCredential
from copilot import CopilotClient, PermissionHandler, ProviderConfig

import tools

logger = logging.getLogger("github-copilot.client")

# The Copilot SDK ships a coding-assistant system prompt by default. Replace it
# with a Teams-assistant persona so the model uses our to-do / file tools
# instead of behaving like a code agent.
_SYSTEM_MESSAGE = (
    "You are a warm, concise personal assistant inside Microsoft Teams. "
    "You help the user manage a simple to-do list and read files they have "
    "shared in the chat. When the user asks you to DO something (add a task, "
    "mark it done, read a shared file), use the available tools rather than "
    "only describing how. Prefer short, friendly replies. If you are unsure, "
    "ask a brief clarifying question."
)

_ENDPOINT = os.environ.get("FOUNDRY_PROJECT_ENDPOINT", "")
_MODEL = os.environ.get("AZURE_AI_MODEL_DEPLOYMENT_NAME", "")
_SESSION_ID = os.environ.get("FOUNDRY_AGENT_SESSION_ID") or str(uuid.uuid4())

_client: CopilotClient | None = None
_session: object | None = None


async def _get_session(conversation_id: str):
    """Create (once) the shared client + session for this container."""
    global _client, _session
    if _session is not None:
        return _session
    if not _ENDPOINT or not _MODEL:
        raise RuntimeError(
            "FOUNDRY_PROJECT_ENDPOINT and AZURE_AI_MODEL_DEPLOYMENT_NAME must be set."
        )
    if _client is None:
        _client = CopilotClient()
        await _client.start()

    token = DefaultAzureCredential().get_token("https://ai.azure.com/.default").token
    opts = dict(
        provider=ProviderConfig(
            type="azure",
            base_url=_ENDPOINT,
            wire_api="responses",
            bearer_token=token,
        ),
        model=_MODEL,
        tools=tools.build_tools(conversation_id),
        system_message={"mode": "replace", "content": _SYSTEM_MESSAGE},
        on_permission_request=PermissionHandler.approve_all,
        streaming=False,
    )
    try:
        _session = await _client.resume_session(_SESSION_ID, **opts)
        logger.info("Resumed Copilot session %s", _SESSION_ID)
    except Exception:  # pylint: disable=broad-exception-caught
        _session = await _client.create_session(session_id=_SESSION_ID, **opts)
        logger.info("Created Copilot session %s", _SESSION_ID)
    return _session


async def ask(conversation_id: str, text: str) -> str:
    """Send ``text`` to the container's session and return the reply text."""
    try:
        session = await _get_session(conversation_id)
        event = await session.send_and_wait(text, timeout=90)
    except Exception as ex:  # pylint: disable=broad-exception-caught
        logger.error("ask() failed: %s", ex, exc_info=True)
        return f"Sorry, something went wrong: {ex}"

    data = event.to_dict().get("data", {}) if event else {}
    return (data.get("content") or "").strip() or "(no response)"
