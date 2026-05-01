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

import json
import logging
import os

from starlette.requests import Request
from starlette.responses import JSONResponse, StreamingResponse

from azure.ai.projects.aio import AIProjectClient
from azure.identity.aio import DefaultAzureCredential

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
_openai_client = _project_client.get_openai_client()

_SYSTEM_PROMPT = "You are a helpful AI assistant. Be concise and informative."

app = InvocationAgentServerHost()

# In-memory session store — keyed by agent_session_id.
# WARNING: state is lost on restart. Use durable storage in production.
_history: list[dict[str, str]] = []

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
    _history.append({"role": "user", "content": user_message})

    async def event_generator():
        full_reply = ""
        async for event in await _openai_client.responses.create(
            model=_model,
            instructions="You are a helpful AI assistant.",
            input=list(_history),
            store=False,
            stream=True,
        ):
            if event.type == "response.output_text.delta":
                full_reply += event.delta
                yield f"data: {json.dumps({'type': 'token', 'content': event.delta})}\n\n"

        yield f"data: {json.dumps({'type': 'done', 'full_text': full_reply})}\n\n"
        _history.append({"role": "assistant", "content": full_reply})

    return StreamingResponse(
        event_generator(),
        media_type="text/event-stream",
        headers={"Cache-Control": "no-cache"},
    )

if __name__ == "__main__":
    app.run()
