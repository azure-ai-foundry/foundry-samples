# Copyright (c) Microsoft. All rights reserved.

"""Human-in-the-loop (HITL) agent using azure-ai-agentserver-invocations with Azure OpenAI.

Demonstrates an approval-gate pattern where the agent:
  1. Receives a task and generates a proposal using Azure OpenAI.
  2. Pauses execution and returns the proposal for human review.
  3. Resumes after the human approves, requests a revision, or rejects.

State machine::

    [new task] ──► AWAITING_APPROVAL ──► (approve) ──► COMPLETED
                        │
                        ├──► (revise + feedback) ──► AWAITING_APPROVAL (loop)
                        │
                        └──► (reject) ──► REJECTED

.. note::

    Session state is persisted as JSON files in the ``$HOME`` directory,
    so state survives restarts and files are accessible via the
    Session Files API when deployed to Azure.

Required environment variables:
    FOUNDRY_PROJECT_ENDPOINT: Foundry project endpoint (e.g., https://your-resource.openai.azure.com/api/projects/proj)
    AZURE_AI_MODEL_DEPLOYMENT_NAME: Model deployment name (e.g., gpt-4o)

Uses DefaultAzureCredential for authentication - works with:
- Azure CLI login (az login)
- Managed Identity in Azure
- Environment variables (AZURE_CLIENT_ID, AZURE_TENANT_ID, AZURE_CLIENT_SECRET)

Usage::

    export FOUNDRY_PROJECT_ENDPOINT="https://your-resource.openai.azure.com/api/projects/proj"
    export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4o"
    python main.py

    # Submit a task
    curl -X POST "http://localhost:8088/invocations?agent_session_id=s1" \\
        -H "Content-Type: application/json" \\
        -d '{"task": "Draft a marketing email for our new AI product launch"}'

    # Approve the proposal
    curl -X POST "http://localhost:8088/invocations?agent_session_id=s1" \\
        -H "Content-Type: application/json" \\
        -d '{"decision": "approve"}'

    # Or revise with feedback
    curl -X POST "http://localhost:8088/invocations?agent_session_id=s1" \\
        -H "Content-Type: application/json" \\
        -d '{"decision": "revise", "feedback": "Make the tone more casual"}'

    # Or reject
    curl -X POST "http://localhost:8088/invocations?agent_session_id=s1" \\
        -H "Content-Type: application/json" \\
        -d '{"decision": "reject"}'

    # Check status (e.g., after reconnecting hours later)
    curl http://localhost:8088/invocations/<invocation_id>
"""

import asyncio
import hashlib
import json
import logging
import os
import tempfile
from pathlib import Path
from typing import Any

from starlette.requests import Request
from starlette.responses import JSONResponse, Response

from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential

from azure.ai.agentserver.invocations import InvocationAgentServerHost

logger = logging.getLogger("human-in-the-loop")

if not os.environ.get("APPLICATIONINSIGHTS_CONNECTION_STRING"):
    logger.warning(
        "APPLICATIONINSIGHTS_CONNECTION_STRING not set — traces will not be sent to "
        "Application Insights. Set it to enable local telemetry. "
        "(This variable is auto-injected in hosted Foundry containers — do not declare it in agent.manifest.yaml.)"
    )

# ---------------------------------------------------------------------------
# Foundry project client — reads FOUNDRY_PROJECT_ENDPOINT.
# FOUNDRY_PROJECT_ENDPOINT is auto-injected in hosted Foundry containers.
# Locally, set it manually or use 'azd ai agent run' which sets it automatically.
# ---------------------------------------------------------------------------
FOUNDRY_PROJECT_ENDPOINT = os.environ.get("FOUNDRY_PROJECT_ENDPOINT")
if not FOUNDRY_PROJECT_ENDPOINT:
    raise EnvironmentError(
        "FOUNDRY_PROJECT_ENDPOINT environment variable is not set. "
        "Set it to your Foundry project endpoint, or use 'azd ai agent run'."
    )

AZURE_AI_MODEL_DEPLOYMENT_NAME = os.environ.get(
    "AZURE_AI_MODEL_DEPLOYMENT_NAME")
if not AZURE_AI_MODEL_DEPLOYMENT_NAME:
    raise EnvironmentError(
        "AZURE_AI_MODEL_DEPLOYMENT_NAME environment variable is not set. "
        "Set it to your model deployment name as declared in agent.manifest.yaml."
    )

_credential = DefaultAzureCredential()
_project_client = AIProjectClient(
    endpoint=FOUNDRY_PROJECT_ENDPOINT, credential=_credential)

# Use the Responses API — not chat.completions (Chat Completions API is legacy).
_openai_client = _project_client.get_openai_client()

# ---------------------------------------------------------------------------
# OpenAPI 3.0 spec -- served at GET /invocations/docs/openapi.json
# ---------------------------------------------------------------------------
OPENAPI_SPEC: dict[str, Any] = {
    "openapi": "3.0.0",
    "info": {
        "title": "Human-in-the-Loop Agent",
        "version": "1.0.0",
        "description": (
            "An agent that generates proposals via Azure OpenAI, pauses for "
            "human approval, and resumes after the human responds."
        ),
    },
    "paths": {
        "/invocations": {
            "post": {
                "summary": "Submit a new task or respond to a pending proposal",
                "parameters": [
                    {
                        "name": "agent_session_id",
                        "in": "query",
                        "required": False,
                        "schema": {"type": "string"},
                    }
                ],
                "requestBody": {
                    "required": True,
                    "content": {
                        "application/json": {
                            "schema": {
                                "type": "object",
                                "properties": {
                                    "task": {"type": "string"},
                                    "decision": {
                                        "type": "string",
                                        "enum": ["approve", "revise", "reject"],
                                    },
                                    "feedback": {"type": "string"},
                                },
                            }
                        }
                    },
                },
                "responses": {
                    "200": {
                        "description": "Agent response with current status and data.",
                    },
                },
            }
        },
        "/invocations/{invocation_id}": {
            "get": {
                "summary": "Check the status of a session",
                "parameters": [
                    {
                        "name": "invocation_id",
                        "in": "path",
                        "required": True,
                        "schema": {"type": "string"},
                    }
                ],
                "responses": {
                    "200": {"description": "Current session state."},
                    "404": {"description": "Session not found."},
                },
            }
        },
        "/invocations/{invocation_id}/cancel": {
            "post": {
                "summary": "Cancel a pending session",
                "parameters": [
                    {
                        "name": "invocation_id",
                        "in": "path",
                        "required": True,
                        "schema": {"type": "string"},
                    }
                ],
                "responses": {
                    "200": {"description": "Cancellation result."},
                    "404": {"description": "Session not found."},
                },
            }
        },
    },
}

# ---------------------------------------------------------------------------
# Session state — persisted as JSON files in $HOME.
# Writing to $HOME makes files visible via the Session Files API.
# ---------------------------------------------------------------------------
_STATE_DIR = Path(os.environ.get("HOME", os.getcwd()))

_contexts: dict[str, dict[str, Any]] = {}
_invocation_to_session: dict[str, str] = {}


def _session_file_path(session_id: str) -> Path:
    """Return the path to the JSON state file for a session."""
    safe_id = "".join(
        c if c.isalnum() or c in "-_" else "_" for c in session_id)
    # Hash suffix avoids collisions from sanitization (e.g. "a@b" vs "a#b")
    hash_suffix = hashlib.sha256(session_id.encode()).hexdigest()[:8]
    return _STATE_DIR / f"hitl_session_{safe_id}_{hash_suffix}.json"


def _save_session(session_id: str, ctx: dict[str, Any]) -> None:
    """Persist session state atomically to a JSON file in $HOME."""
    data = {"session_id": session_id, **ctx}
    target = _session_file_path(session_id)
    fd, tmp_path = tempfile.mkstemp(dir=str(_STATE_DIR), suffix=".tmp")
    try:
        with os.fdopen(fd, "w") as f:
            json.dump(data, f, indent=2)
        os.replace(tmp_path, str(target))
    except BaseException:
        try:
            os.unlink(tmp_path)
        except OSError:
            pass
        raise
    logger.debug("[%s] State saved to disk", session_id)


def _load_all_sessions() -> None:
    """Load all persisted sessions into memory on startup."""
    if not _STATE_DIR.is_dir():
        return
    for path in _STATE_DIR.glob("hitl_session_*.json"):
        try:
            data = json.loads(path.read_text())
            if not isinstance(data, dict):
                logger.warning("Skipping %s: not a JSON object", path.name)
                continue
            session_id = data.get("session_id")
            if not session_id:
                continue
            required = {"status", "original_task", "proposal"}
            if not required.issubset(data.keys()):
                logger.warning("Skipping %s: missing required keys", path.name)
                continue
            ctx = {k: v for k, v in data.items() if k != "session_id"}
            inv_ids = ctx.get("invocation_ids", [])
            if not isinstance(inv_ids, list):
                inv_ids = []
                ctx["invocation_ids"] = inv_ids
            _contexts[session_id] = ctx
            for inv_id in inv_ids:
                _invocation_to_session[inv_id] = session_id
        except Exception:
            logger.warning("Failed to load session file: %s", path.name)
    if _contexts:
        logger.info("Loaded %d session(s) from disk", len(_contexts))


_load_all_sessions()

# ---------------------------------------------------------------------------
# Server
# ---------------------------------------------------------------------------
app = InvocationAgentServerHost(openapi_spec=OPENAPI_SPEC)

# ---------------------------------------------------------------------------
# LLM helper
# ---------------------------------------------------------------------------
_SYSTEM_PROMPT = (
    "You are a professional assistant. The user will give you a task. "
    "Generate a high-quality draft proposal that the user can review "
    "and approve. Be detailed, well-structured, and ready for review.\n\n"
    "If revision feedback is provided, incorporate it into an improved "
    "version of the proposal."
)


async def _call_llm(instructions: str, input_items: list[dict[str, str]]) -> str:
    """Call the Foundry Responses API and return the response text.

    The Foundry OpenAI client is synchronous; run it in a thread so we don't
    block the event loop.
    """
    loop = asyncio.get_running_loop()
    response = await loop.run_in_executor(
        None,
        lambda: _openai_client.responses.create(
            model=AZURE_AI_MODEL_DEPLOYMENT_NAME,
            instructions=instructions,
            input=input_items,
        ),
    )
    for item in response.output:
        if item.type == "message":
            for part in item.content:
                if part.type == "output_text":
                    return part.text
    return ""


async def _generate_proposal(
    task: str,
    revision_history: list[dict[str, str]],
) -> str:
    """Generate or revise a proposal using Azure OpenAI Responses API.

    Builds input items with the original task and any prior
    revision rounds, then calls the LLM for a new proposal.
    """
    input_items: list[dict[str, str]] = [
        {"role": "user", "content": f"Task: {task}"},
    ]
    for rev in revision_history:
        input_items.append(
            {"role": "assistant", "content": rev["proposal"]})
        input_items.append(
            {"role": "user", "content": f"Revision feedback: {rev['feedback']}"})

    return await _call_llm(_SYSTEM_PROMPT, input_items)


# ---------------------------------------------------------------------------
# Handlers
# ---------------------------------------------------------------------------

@app.invoke_handler
async def handle_invoke(request: Request) -> Response:
    """Handle a new task submission or a decision on a pending proposal."""
    try:
        data = await request.json()
        if not isinstance(data, dict):
            raise ValueError("body is not a JSON object")
    except (json.JSONDecodeError, ValueError):
        return JSONResponse(
            status_code=400,
            content={
                "error": "invalid_request",
                "message": (
                    'Request body must be a JSON object with either a "task" '
                    '(to start a new proposal) or a "decision" '
                    '(approve/revise/reject), e.g. {"task": "analyze dataset"} or '
                    '{"decision": "approve"}'
                ),
            },
        )

    session_id = request.state.session_id
    invocation_id = request.state.invocation_id

    task = data.get("task")
    decision = data.get("decision")

    if task and decision:
        return JSONResponse(
            {"error": "Cannot provide both 'task' and 'decision' in the same request."},
            status_code=400,
        )

    # --- New task submission ---
    if task:
        if not task.strip():
            return JSONResponse(
                {"error": "task cannot be empty"},
                status_code=400,
            )

        existing = _contexts.get(session_id)
        if existing and existing["status"] == "awaiting_approval":
            return JSONResponse(
                {"error": (
                    f"Session {session_id} has a pending proposal. "
                    "Approve, revise, or reject it before submitting a new task."
                )},
                status_code=409,
            )

        logger.info("[%s] New task received: %s", session_id, task[:100])
        proposal = await _generate_proposal(task, [])

        _contexts[session_id] = {
            "status": "awaiting_approval",  # -> AWAITING_APPROVAL (paused)
            "original_task": task,
            "proposal": proposal,
            "revision_history": [],
            "invocation_id": invocation_id,
            "invocation_ids": [invocation_id],
        }
        _invocation_to_session[invocation_id] = session_id
        _save_session(session_id, _contexts[session_id])

        return JSONResponse({
            "session_id": session_id,
            "invocation_id": invocation_id,
            "status": "awaiting_approval",
            "proposal": proposal,
            "revision_count": 0,
        })

    # --- Decision on existing proposal ---
    if decision:
        ctx = _contexts.get(session_id)
        if not ctx:
            return JSONResponse(
                {"error": f"No pending session found for session_id={session_id}"},
                status_code=400,
            )
        if ctx["status"] != "awaiting_approval":
            return JSONResponse(
                {"error": f"Session is not awaiting approval (status={ctx['status']})"},
                status_code=400,
            )

        # Validate decision and required fields before any state mutation
        if decision not in ("approve", "revise", "reject"):
            return JSONResponse(
                {"error": f"Unknown decision: {decision}. Use 'approve', 'revise', or 'reject'."},
                status_code=400,
            )
        feedback = data.get("feedback", "")
        if decision == "revise" and not feedback:
            return JSONResponse(
                {"error": "feedback is required for 'revise' decision"},
                status_code=400,
            )

        # All validation passed — track the invocation
        ctx["invocation_id"] = invocation_id
        ctx.setdefault("invocation_ids", []).append(invocation_id)
        _invocation_to_session[invocation_id] = session_id

        if decision == "approve":
            logger.info("[%s] Proposal approved", session_id)
            ctx["status"] = "completed"  # -> COMPLETED (terminal)
            _save_session(session_id, ctx)

            return JSONResponse({
                "session_id": session_id,
                "invocation_id": invocation_id,
                "status": "completed",
                "final_output": ctx["proposal"],
                "revision_count": len(ctx["revision_history"]),
            })

        if decision == "revise":
            logger.info("[%s] Revision requested: %s",
                        session_id, feedback[:100])
            ctx["revision_history"].append({
                "proposal": ctx["proposal"],
                "feedback": feedback,
            })

            new_proposal = await _generate_proposal(
                ctx["original_task"], ctx["revision_history"])
            ctx["proposal"] = new_proposal
            ctx["status"] = "awaiting_approval"  # -> AWAITING_APPROVAL (loop)
            _save_session(session_id, ctx)

            return JSONResponse({
                "session_id": session_id,
                "invocation_id": invocation_id,
                "status": "awaiting_approval",
                "proposal": new_proposal,
                "revision_count": len(ctx["revision_history"]),
            })

        # decision == "reject"
        logger.info("[%s] Proposal rejected", session_id)
        ctx["status"] = "rejected"  # -> REJECTED (terminal)
        _save_session(session_id, ctx)

        return JSONResponse({
            "session_id": session_id,
            "invocation_id": invocation_id,
            "status": "rejected",
            "revision_count": len(ctx["revision_history"]),
        })

    return JSONResponse(
        {"error": "Request must include either 'task' (new task) or 'decision' (approve/revise/reject)."},
        status_code=400,
    )


@app.get_invocation_handler
async def handle_get_invocation(request: Request) -> Response:
    """Retrieve the current status and data for a session."""
    invocation_id = request.state.invocation_id
    session_id = _invocation_to_session.get(invocation_id)

    if not session_id or session_id not in _contexts:
        return JSONResponse({"error": "not found"}, status_code=404)

    ctx = _contexts[session_id]
    response_data: dict[str, Any] = {
        "session_id": session_id,
        "invocation_id": ctx["invocation_id"],
        "status": ctx["status"],
        "original_task": ctx["original_task"],
        "revision_count": len(ctx["revision_history"]),
    }

    if ctx["status"] == "awaiting_approval":
        response_data["proposal"] = ctx["proposal"]
    elif ctx["status"] == "completed":
        response_data["final_output"] = ctx["proposal"]

    return JSONResponse(response_data)


@app.cancel_invocation_handler
async def handle_cancel_invocation(request: Request) -> Response:
    """Cancel a pending session."""
    invocation_id = request.state.invocation_id
    session_id = _invocation_to_session.get(invocation_id)

    if not session_id or session_id not in _contexts:
        return JSONResponse({"error": "not found"}, status_code=404)

    ctx = _contexts[session_id]
    if ctx["status"] in ("completed", "rejected"):
        return JSONResponse({
            "session_id": session_id,
            "invocation_id": invocation_id,
            "status": ctx["status"],
            "error": "session already finalized",
        })

    ctx["invocation_id"] = invocation_id
    ctx.setdefault("invocation_ids", []).append(invocation_id)
    _invocation_to_session[invocation_id] = session_id
    ctx["status"] = "cancelled"  # -> CANCELLED (terminal)
    _save_session(session_id, ctx)
    logger.info("[%s] Session cancelled", session_id)

    return JSONResponse({
        "session_id": session_id,
        "invocation_id": invocation_id,
        "status": "cancelled",
    })


if __name__ == "__main__":
    app.run()
