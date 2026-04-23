# Copyright (c) Microsoft. All rights reserved.

"""Getting-started: GitHub Copilot SDK with the Foundry invocations protocol."""

import asyncio
import json
import logging
import os
import pathlib
import uuid

from starlette.requests import Request
from starlette.responses import JSONResponse, Response, StreamingResponse


from azure.ai.agentserver.invocations import InvocationAgentServerHost
from copilot import CopilotClient, SubprocessConfig
from copilot.session import PermissionHandler

from copilot.generated.session_events import SessionEventType

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

if not os.environ.get("APPLICATIONINSIGHTS_CONNECTION_STRING"):
    logger.warning(
        "APPLICATIONINSIGHTS_CONNECTION_STRING not set — traces will not be sent to "
        "Application Insights. Set it to enable local telemetry. "
        "(This variable is auto-injected in hosted Foundry containers — do not declare it in agent.manifest.yaml.)"
    )

if not os.environ.get("GITHUB_TOKEN"):
    raise EnvironmentError(
        "GITHUB_TOKEN environment variable is not set. "
        "Supply a GitHub fine-grained PAT with 'Copilot Requests → Read-only' permission. "
        "Create one at https://github.com/settings/personal-access-tokens/new"
    )

app = InvocationAgentServerHost()

_client: CopilotClient | None = None
_session = None
_session_id: str | None = None
_skills_dir = str(pathlib.Path(__file__).parent / "skills")


async def _ensure_session():
    """Resume a persisted session or create a new one (lazy, runs once)."""
    global _client, _session, _session_id
    if _session is not None:
        return

    _session_id = os.environ.get("FOUNDRY_AGENT_SESSION_ID")
    if not _session_id:
        _session_id = str(uuid.uuid4())
        logger.warning(
            "FOUNDRY_AGENT_SESSION_ID not set, using: %s", _session_id)

    _client = CopilotClient(
        SubprocessConfig(github_token=os.environ["GITHUB_TOKEN"]),
        auto_start=False,
    )
    await _client.start()

    working_dir = os.environ.get("HOME", "/home")

    try:
        _session = await _client.resume_session(
            _session_id,
            on_permission_request=PermissionHandler.approve_all,
            streaming=True,
            skill_directories=[_skills_dir],
            working_directory=working_dir,
        )
        logger.info("Resumed session: %s", _session_id)
    except Exception:
        _session = await _client.create_session(
            session_id=_session_id,
            on_permission_request=PermissionHandler.approve_all,
            streaming=True,
            skill_directories=[_skills_dir],
            working_directory=working_dir,
        )
        logger.info("Created session: %s", _session_id)


async def _stream_response(invocation_id: str, input_text: str):
    """Forward Copilot SDK session events as SSE."""
    await _ensure_session()
    queue: asyncio.Queue = asyncio.Queue()

    def on_event(event):
        if event.type == SessionEventType.SESSION_IDLE:
            queue.put_nowait(None)
        elif event.type == SessionEventType.SESSION_ERROR:
            queue.put_nowait(RuntimeError(
                getattr(event.data, "message", "error")))
        else:
            queue.put_nowait(event)

    unsubscribe = _session.on(on_event)
    try:
        await _session.send(input_text)
        while True:
            item = await queue.get()
            if item is None:
                break
            if isinstance(item, Exception):
                yield f"data: {json.dumps({'type': 'error', 'message': str(item)})}\n\n".encode()
                break
            yield f"data: {json.dumps(item.to_dict())}\n\n".encode()

        yield f"event: done\ndata: {json.dumps({'invocation_id': invocation_id, 'session_id': _session_id})}\n\n".encode()
    finally:
        unsubscribe()


@app.invoke_handler
async def handle_invoke(request: Request) -> Response:
    try:
        data = await request.json()
        if not isinstance(data, dict):
            raise ValueError("body is not a JSON object")
        input_text = data.get("input")
        if not isinstance(input_text, str) or not input_text.strip():
            raise ValueError('missing or empty "input" field')
    except (json.JSONDecodeError, ValueError):
        return JSONResponse(
            status_code=400,
            content={
                "error": "invalid_request",
                "message": (
                    'Request body must be a JSON object with a non-empty "input" string, '
                    'e.g. {"input": "What can you help me with?"}'
                ),
            },
        )
    return StreamingResponse(
        _stream_response(request.state.invocation_id, input_text),
        media_type="text/event-stream",
        headers={"Cache-Control": "no-cache", "Connection": "keep-alive"},
    )


if __name__ == "__main__":
    app.run()
