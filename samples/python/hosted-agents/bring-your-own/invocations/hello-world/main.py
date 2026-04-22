# Copyright (c) Microsoft. All rights reserved.

"""Hello World — Bring Your Own Invocations agent.

Minimal hosted agent that forwards user input to a Foundry model via the
Responses API and returns the reply through the Invocations protocol.

This sample demonstrates the simplest possible BYO integration: the protocol
SDK (``azure-ai-agentserver-invocations``) handles the HTTP contract and
session resolution, and you supply the model call using the Foundry SDK.

Unlike the Responses protocol, the Invocations protocol does **not** provide
built-in server-side conversation history. This agent maintains an in-memory
session store keyed by ``agent_session_id``. In production, replace it with
durable storage (Redis, Cosmos DB, etc.) so history survives restarts.

Required environment variables:
    FOUNDRY_PROJECT_ENDPOINT: Foundry project endpoint (auto-injected in hosted containers)
    AZURE_AI_MODEL_DEPLOYMENT_NAME: Model deployment name (declared in agent.manifest.yaml)

Usage::

    # Set environment variables
    export FOUNDRY_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
    export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4.1-mini"

    # Start the agent
    python main.py

    # Turn 1 — start a new conversation
    curl -sS -N -X POST "http://localhost:8088/invocations?agent_session_id=chat-001" \\
        -H "Content-Type: application/json" \\
        -d '{"message": "What is Microsoft Foundry?"}'

    # Turn 2 — continue the same conversation
    curl -sS -N -X POST "http://localhost:8088/invocations?agent_session_id=chat-001" \\
        -H "Content-Type: application/json" \\
        -d '{"message": "What hosted agent options does it offer?"}'
"""

import asyncio
import json
import logging
import os

from starlette.requests import Request
from starlette.responses import JSONResponse, StreamingResponse

from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential

from azure.ai.agentserver.invocations import InvocationAgentServerHost

logger = logging.getLogger(__name__)

if not os.environ.get("APPLICATIONINSIGHTS_CONNECTION_STRING"):
    logger.warning(
        "APPLICATIONINSIGHTS_CONNECTION_STRING not set — traces will not be sent to "
        "Application Insights. Set it to enable local telemetry. "
        "(This variable is auto-injected in hosted Foundry containers — do not declare it in agent.manifest.yaml.)"
    )

# Initialize Foundry project client — reads FOUNDRY_PROJECT_ENDPOINT.
# FOUNDRY_PROJECT_ENDPOINT is auto-injected in hosted Foundry containers.
# Locally, set it manually or use 'azd ai agent run' which sets it automatically.
_endpoint = os.environ.get("FOUNDRY_PROJECT_ENDPOINT")
if not _endpoint:
    raise EnvironmentError(
        "FOUNDRY_PROJECT_ENDPOINT environment variable is not set. "
        "Set it to your Foundry project endpoint, or use 'azd ai agent run' "
        "which sets it automatically."
    )

_model = os.environ.get("AZURE_AI_MODEL_DEPLOYMENT_NAME")
if not _model:
    raise EnvironmentError(
        "AZURE_AI_MODEL_DEPLOYMENT_NAME environment variable is not set. "
        "Set it to your model deployment name as declared in agent.manifest.yaml."
    )

_credential = DefaultAzureCredential()
_project_client = AIProjectClient(endpoint=_endpoint, credential=_credential)

# Use the Responses API — not chat.completions (Chat Completions API is legacy).
_responses_client = _project_client.get_openai_client().responses

_SYSTEM_PROMPT = "You are a helpful AI assistant. Be concise and informative."

app = InvocationAgentServerHost()

# In-memory session store — keyed by agent_session_id.
# WARNING: state is lost on restart. Use durable storage in production.
_sessions: dict[str, list[dict[str, str]]] = {}


async def _stream_reply(input_items: list[dict[str, str]]):
    """Call the Foundry model and yield text deltas as they arrive.

    The Responses SDK uses a synchronous streaming iterator. We bridge it to
    async by running it in a thread pool and forwarding each delta through an
    ``asyncio.Queue`` so the event loop is never blocked.
    """
    loop = asyncio.get_running_loop()
    queue: asyncio.Queue[str | None] = asyncio.Queue()

    def _produce() -> None:
        """Runs in a thread: streams from the model and enqueues each delta."""
        try:
            for event in _responses_client.create(
                model=_model,
                instructions=_SYSTEM_PROMPT,
                input=input_items,
                store=False,  # This agent owns history — no need to store at the model level
                stream=True,
            ):
                if event.type == "response.output_text.delta":
                    loop.call_soon_threadsafe(queue.put_nowait, event.delta)
        finally:
            # None signals end of stream
            loop.call_soon_threadsafe(queue.put_nowait, None)

    # Start sync streaming in a background thread; yield deltas as they arrive.
    fut = loop.run_in_executor(None, _produce)
    while (delta := await queue.get()) is not None:
        yield delta
    await fut  # re-raise any exception that escaped the thread


# ── Required handler ──────────────────────────────────────────────────────────
# @app.invoke_handler is the only handler you must implement. It receives every
# POST /invocations request. The function name below is arbitrary.
#
# Two optional handlers exist for long-running operations (LRO):
#   @app.get_invocation_handler    — handle GET /invocations/{id} status polls
#   @app.cancel_invocation_handler — handle DELETE /invocations/{id} cancellation
# For a simple streaming agent like this one, neither is needed.
#
# To serve an OpenAPI spec at GET /invocations/docs/openapi.json, pass it to
# the host constructor: InvocationAgentServerHost(openapi_spec={...})
# ─────────────────────────────────────────────────────────────────────────────
@app.invoke_handler
async def handle_invoke(request: Request):
    """Handle a streaming multi-turn chat request."""
    # Accept either a JSON object ({"message": "..."} or {"input": "..."}) or a
    # plain-text body (e.g. sent directly from the Foundry portal chat UI).
    try:
        body = await request.body()
        if not body:
            raise ValueError("empty body")
        try:
            data = json.loads(body)
        except json.JSONDecodeError:
            user_message = body.decode("utf-8", errors="replace").strip()
        else:
            if isinstance(data, dict):
                user_message = data.get("message") or data.get("input") or ""
            else:
                user_message = body.decode("utf-8", errors="replace").strip()
        if not isinstance(user_message, str) or not user_message.strip():
            raise ValueError("missing message text")
    except ValueError:
        return JSONResponse(
            status_code=400,
            content={
                "error": "invalid_request",
                "message": (
                    'Request body must be a non-empty JSON object with a "message" (or "input") '
                    'string, or a plain-text body, e.g. {"message": "What is Microsoft Foundry?"}'
                ),
            },
        )

    # The Invocations SDK resolves session and invocation identity from the
    # incoming request headers and exposes them via request.state.
    session_id = request.state.session_id
    invocation_id = request.state.invocation_id

    logger.info(
        "Processing invocation %s (session %s)", invocation_id, session_id
    )

    # Retrieve or create conversation history for this session.
    history = _sessions.setdefault(session_id, [])
    history.append({"role": "user", "content": user_message})

    # Build the Responses API input list from history.
    # History is stored as {role, content} dicts — the same format the API accepts.
    input_items = list(history)

    async def event_generator():
        full_reply = ""
        try:
            async for delta in _stream_reply(input_items):
                full_reply += delta
                yield f"data: {json.dumps({'type': 'token', 'content': delta})}\n\n"
        except Exception as exc:
            msg = f"Error calling model: {exc}"
            logger.error(msg)
            full_reply = msg
            yield f"data: {json.dumps({'type': 'token', 'content': msg})}\n\n"

        # Final event carries the complete text so the caller can use it
        # without having to reassemble the token stream.
        yield f"data: {json.dumps({'type': 'done', 'invocation_id': invocation_id, 'session_id': session_id, 'full_text': full_reply})}\n\n"

        # Persist the assistant reply to history after streaming is complete.
        if full_reply:
            history.append({"role": "assistant", "content": full_reply})

    return StreamingResponse(
        event_generator(),
        media_type="text/event-stream",
        headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"},
    )


app.run()
