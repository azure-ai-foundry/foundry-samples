# Copyright (c) Microsoft. All rights reserved.

"""Resilient plan-approve-execute agent (invocations protocol).

A **long-running, crash-resilient** human-in-the-loop agent built on the
resilient ``@multi_turn_task`` primitive from
``azure.ai.agentserver.core.tasks``. Unlike a quick "generate a proposal and
approve it" flow, this agent does real, long-running autonomous work and gates
the *dangerous* parts on a human:

1. **Plan** — given a goal, the agent uses Azure OpenAI to decompose it into an
   ordered plan and flags which steps are **irreversible**. It then *suspends*,
   presenting the plan for human approval.
2. **Approve the plan** — the human approves (or edits, or rejects). The agent
   begins executing the plan step by step, **checkpointing after every step** so
   a crash resumes from the next unfinished step instead of restarting.
3. **Confirm each irreversible step** — before any irreversible action the agent
   *suspends again* for an explicit human confirmation, then performs that action
   **exactly once** (an at-most-once watermark survives crashes so a restart can
   never double-execute it).

State machine::

    [plan] ─► AWAITING_PLAN_APPROVAL ─► (approve/edit) ─► EXECUTING ─┐
                    │                                                 │
                    └─► (reject) ─► REJECTED          ┌──────────────┘
                                                      ▼
                             AWAITING_ACTION_APPROVAL ─► (approve_action) ─► EXECUTING
                                                      └─► (reject_action) ─► RESOLVED (stopped)
                                                      ...
                                              (all steps done) ─► RESOLVED (completed)

Why the resilient primitive (vs. hand-rolled JSON state): the ``@multi_turn_task``
framework persists the chain's input and metadata to a task store and, after a
container restart / OOM kill / redeploy, **re-invokes the same turn with the same
input** (``ctx.entry_mode == "recovered"``). The long ``EXECUTING`` phase — the
part most likely to be interrupted — resumes from its last checkpoint. See the
`Resilient Task Developer Guide
<https://github.com/Azure/azure-sdk-for-python/blob/main/sdk/agentserver/azure-ai-agentserver-core/docs/tasks-guide.md>`__.

Because execution is long-running, every POST returns ``202`` immediately with an
``invocation_id``; poll ``GET /invocations/{invocation_id}`` for the current
status and output. This is the resilient long-running contract (a disconnecting
client never loses in-flight work), in contrast to a synchronous request/response
agent.

Required environment variables:
    FOUNDRY_PROJECT_ENDPOINT: Foundry project endpoint (auto-injected when hosted;
        set locally, or use ``azd ai agent run``). If unset, the agent runs in an
        **offline demo mode** with deterministic stand-ins for the model calls, so
        you can exercise the resilient control flow without any credentials.
    AZURE_AI_MODEL_DEPLOYMENT_NAME: Model deployment name (e.g. gpt-5.4-mini).

Usage::

    python main.py

    # 1) Submit a goal — the agent plans, then suspends for approval.
    curl -X POST "http://localhost:8088/invocations?agent_session_id=job-1" \\
        -H "Content-Type: application/json" \\
        -d '{"action": "plan", "goal": "Prepare the Q3 release"}'
    # -> 202 {"invocation_id": "<i1>", "status": "running"}

    # 2) Poll until the plan is ready.
    curl "http://localhost:8088/invocations/<i1>?agent_session_id=job-1"
    # -> {"status": "awaiting_plan_approval", "output": {"plan": [...]}}

    # 3) Approve the plan — the agent starts executing (long-running).
    curl -X POST "http://localhost:8088/invocations?agent_session_id=job-1" \\
        -H "Content-Type: application/json" \\
        -d '{"action": "approve_plan", "approver": "sam"}'

    # 4) Poll — execution pauses at the first irreversible step.
    curl "http://localhost:8088/invocations/<i2>?agent_session_id=job-1"
    # -> {"status": "awaiting_action_approval", "output": {"next_step": {...}}}

    # 5) Confirm the irreversible step — it runs exactly once.
    curl -X POST "http://localhost:8088/invocations?agent_session_id=job-1" \\
        -H "Content-Type: application/json" \\
        -d '{"action": "approve_action", "approver": "sam"}'
    # ... repeat 4-5 for each irreversible step, until status == "resolved".
"""

from __future__ import annotations

import asyncio
import json
import logging
import os
import uuid
from datetime import datetime, timezone
from typing import Any

from starlette.requests import Request
from starlette.responses import JSONResponse, Response

from azure.ai.agentserver.core.tasks import (
    TaskConflictError,
    TaskContext,
    multi_turn_task,
)
from azure.ai.agentserver.core.tasks._manager import get_task_manager
from azure.ai.agentserver.invocations import InvocationAgentServerHost

logger = logging.getLogger("resilient-approval-gate")

if not os.environ.get("APPLICATIONINSIGHTS_CONNECTION_STRING"):
    logger.warning(
        "APPLICATIONINSIGHTS_CONNECTION_STRING not set — traces will not be sent to "
        "Application Insights. It is auto-injected in hosted Foundry containers."
    )

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------
FOUNDRY_PROJECT_ENDPOINT = os.environ.get("FOUNDRY_PROJECT_ENDPOINT")
AZURE_AI_MODEL_DEPLOYMENT_NAME = os.environ.get("AZURE_AI_MODEL_DEPLOYMENT_NAME")

# Offline demo mode: with no Foundry endpoint we substitute deterministic
# stand-ins for the model so the resilient control flow runs with no credentials.
OFFLINE_MODE = not FOUNDRY_PROJECT_ENDPOINT
if OFFLINE_MODE:
    logger.warning(
        "FOUNDRY_PROJECT_ENDPOINT not set — running in OFFLINE demo mode with "
        "deterministic stand-ins for the model. Set FOUNDRY_PROJECT_ENDPOINT and "
        "AZURE_AI_MODEL_DEPLOYMENT_NAME (or use 'azd ai agent run') for real planning."
    )

# Per-step execution work simulated as a cooldown, so EXECUTING is genuinely
# long-running and the crash-recovery / at-most-once paths are meaningful.
STEP_DURATION_SEC = float(os.environ.get("STEP_DURATION_SEC", "5"))

_openai_client: Any = None


def _get_client() -> Any:
    """Lazily construct the Foundry OpenAI client (kept out of import time)."""

    global _openai_client  # pylint: disable=global-statement
    if _openai_client is not None:
        return _openai_client
    from azure.ai.projects import AIProjectClient  # pylint: disable=import-outside-toplevel
    from azure.identity import DefaultAzureCredential  # pylint: disable=import-outside-toplevel

    project = AIProjectClient(
        endpoint=FOUNDRY_PROJECT_ENDPOINT,
        credential=DefaultAzureCredential(),
    )
    _openai_client = project.get_openai_client()
    return _openai_client


def _now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


# ---------------------------------------------------------------------------
# OpenAPI 3.0 spec — served at GET /invocations/docs/openapi.json
# ---------------------------------------------------------------------------
OPENAPI_SPEC: dict[str, Any] = {
    "openapi": "3.0.0",
    "info": {
        "title": "Resilient Approval-Gate Agent",
        "version": "1.0.0",
        "description": (
            "A long-running, crash-resilient agent that plans a goal, gates the "
            "plan on human approval, executes it step by step, and gates each "
            "irreversible step on a second human confirmation."
        ),
    },
    "paths": {
        "/invocations": {
            "post": {
                "summary": "Submit a goal or respond to a pending gate",
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
                                    "action": {
                                        "type": "string",
                                        "enum": [
                                            "plan",
                                            "approve_plan",
                                            "edit_plan",
                                            "reject",
                                            "approve_action",
                                            "reject_action",
                                        ],
                                    },
                                    "goal": {"type": "string"},
                                    "plan": {"type": "array", "items": {"type": "object"}},
                                    "approver": {"type": "string"},
                                    "reason": {"type": "string"},
                                },
                                "required": ["action"],
                            }
                        }
                    },
                },
                "responses": {
                    "202": {"description": "Accepted; poll the invocation for status."},
                    "409": {"description": "The chain is busy executing; retry shortly."},
                },
            }
        },
        "/invocations/{invocation_id}": {
            "get": {
                "summary": "Poll the status/output of an invocation",
                "parameters": [
                    {
                        "name": "invocation_id",
                        "in": "path",
                        "required": True,
                        "schema": {"type": "string"},
                    },
                    {
                        "name": "agent_session_id",
                        "in": "query",
                        "required": False,
                        "schema": {"type": "string"},
                    },
                ],
                "responses": {
                    "200": {"description": "Current status and output."},
                    "404": {"description": "Invocation not found."},
                },
            }
        },
        "/invocations/{invocation_id}/cancel": {
            "post": {
                "summary": "Cancel the job (deletes the resilient chain)",
                "parameters": [
                    {
                        "name": "invocation_id",
                        "in": "path",
                        "required": True,
                        "schema": {"type": "string"},
                    },
                    {
                        "name": "agent_session_id",
                        "in": "query",
                        "required": False,
                        "schema": {"type": "string"},
                    },
                ],
                "responses": {"200": {"description": "Cancellation result."}},
            }
        },
    },
}

# ---------------------------------------------------------------------------
# Model helpers (with deterministic offline fallback)
# ---------------------------------------------------------------------------
_PLANNER_PROMPT = (
    "You are a planning assistant for an autonomous agent. Given a goal, produce "
    "an ordered plan of 3-6 concrete steps. Mark a step irreversible only if it "
    "has an external, hard-to-undo effect (publishing, sending, provisioning, "
    "deleting, tagging a release). Return ONLY a JSON array of objects with keys "
    '"action" (string) and "irreversible" (boolean). No prose.'
)


async def _call_llm(instructions: str, user_input: str) -> str:
    """Call the Foundry Responses API (sync client) off the event loop."""

    client = _get_client()
    loop = asyncio.get_running_loop()
    response = await loop.run_in_executor(
        None,
        lambda: client.responses.create(
            model=AZURE_AI_MODEL_DEPLOYMENT_NAME,
            instructions=instructions,
            input=user_input,
        ),
    )
    for item in response.output:
        if item.type == "message":
            for part in item.content:
                if part.type == "output_text":
                    return part.text
    return ""


def _offline_plan(goal: str) -> list[dict[str, Any]]:
    """Deterministic stand-in plan so the sample runs with no credentials."""

    return [
        {"action": f"Gather inputs and context for: {goal}", "irreversible": False},
        {"action": "Draft the changes and validate them locally", "irreversible": False},
        {"action": "Run checks and summarize the results", "irreversible": False},
        {"action": "Publish / apply the result (external effect)", "irreversible": True},
    ]


async def _generate_plan(goal: str) -> list[dict[str, Any]]:
    """Produce an ordered, irreversibility-tagged plan for the goal."""

    if OFFLINE_MODE:
        return _offline_plan(goal)

    raw = await _call_llm(_PLANNER_PROMPT, f"Goal: {goal}")
    try:
        text = raw.strip()
        if text.startswith("```"):
            text = text.split("```", 2)[1].removeprefix("json").strip()
        parsed = json.loads(text)
        steps = [
            {"action": str(s["action"]), "irreversible": bool(s.get("irreversible", False))}
            for s in parsed
            if isinstance(s, dict) and s.get("action")
        ]
        if steps:
            return steps
    except (json.JSONDecodeError, KeyError, TypeError, ValueError):
        logger.warning("Planner returned unparseable output; falling back to a default plan.")
    return _offline_plan(goal)


async def _execute_step(step: dict[str, Any], ctx: TaskContext[dict]) -> str:
    """Perform one plan step. Replace the body with real tool calls.

    Simulated as cooldown work that stays responsive to shutdown so an evicted
    container can defer the turn for recovery.
    """

    await _sleep_or_defer(ctx, STEP_DURATION_SEC)
    return f"done: {step['action']}"


# ---------------------------------------------------------------------------
# Resilient chain — one @multi_turn_task per job (task_id == job session).
# ---------------------------------------------------------------------------
@multi_turn_task(name="approval_workflow")
async def approval_workflow(ctx: TaskContext[dict]) -> dict[str, Any]:
    """One resilient chain per job. Each POST runs this from the top.

    The default metadata namespace holds the per-invocation result the HTTP
    ``GET`` handler polls. The ``"job"`` namespace holds cross-turn state — the
    goal, the plan, per-step results, execution watermark, and at-most-once
    tokens — that must survive both the human wait and any crash.
    """

    data = ctx.input
    invocation_id: str = data.get("invocation_id", ctx.input_id)
    action = str(data.get("action", "plan")).lower()
    job = ctx.metadata("job")

    ctx.metadata["invocation_id"] = invocation_id
    ctx.metadata["status"] = "running"
    await ctx.metadata.flush()

    if ctx.entry_mode == "recovered":
        logger.warning("Recovered job %s mid-turn (phase=%s)", ctx.task_id, job.get("phase"))

    if action == "plan":
        return await _do_plan(ctx, job, data)
    if action in ("approve_plan", "edit_plan"):
        return await _begin_execution(ctx, job, data)
    if action == "approve_action":
        return await _resume_execution(ctx, job, approved=True)
    if action == "reject_action":
        return await _resume_execution(ctx, job, approved=False)
    if action == "reject":
        job["phase"] = "resolved"
        await job.flush()
        return await _complete(ctx, {"status": "rejected", "note": "Plan rejected by human."})

    return await _complete(ctx, {"status": "error", "message": f"Unknown action: {action}"})


async def _do_plan(ctx: TaskContext[dict], job: Any, data: dict[str, Any]) -> dict[str, Any]:
    """Turn 1: generate the plan, then suspend for human approval."""

    if job.get("phase") in ("executing", "awaiting_action_approval"):
        return await _complete(
            ctx,
            {"status": job.get("phase"), "note": "Job already in progress.", "plan": job.get("plan")},
        )

    goal = str(data.get("goal", "")).strip()
    if not goal:
        return await _complete(ctx, {"status": "error", "message": "goal is required for action=plan."})

    plan = await _generate_plan(goal)
    job["goal"] = goal
    job["plan"] = plan
    job["results"] = []
    job["completed_steps"] = 0
    job["phase"] = "awaiting_plan_approval"
    await job.flush()

    return await _complete(
        ctx,
        {
            "status": "awaiting_plan_approval",
            "goal": goal,
            "plan": plan,
            "note": "Review the plan. POST action=approve_plan (or edit_plan / reject).",
        },
    )


async def _begin_execution(ctx: TaskContext[dict], job: Any, data: dict[str, Any]) -> dict[str, Any]:
    """Turn 2: accept (or replace) the plan and run the execution loop."""

    if job.get("phase") not in ("awaiting_plan_approval", None):
        # Idempotent: an approve replayed after execution started just continues.
        if job.get("phase") in ("executing", "awaiting_action_approval"):
            return await _run_execution(ctx, job)
        return await _complete(ctx, {"status": "error", "message": "No plan awaiting approval."})

    if str(data.get("action")).lower() == "edit_plan":
        edited = data.get("plan")
        if isinstance(edited, list) and edited:
            job["plan"] = [
                {"action": str(s["action"]), "irreversible": bool(s.get("irreversible", False))}
                for s in edited
                if isinstance(s, dict) and s.get("action")
            ]

    job["approver"] = data.get("approver", "unknown")
    job["phase"] = "executing"
    await job.flush()
    return await _run_execution(ctx, job)


async def _resume_execution(ctx: TaskContext[dict], job: Any, *, approved: bool) -> dict[str, Any]:
    """Later turns: apply the human's decision on the pending irreversible step."""

    if job.get("phase") != "awaiting_action_approval":
        return await _complete(ctx, {"status": "error", "message": "No action awaiting confirmation."})

    if not approved:
        job["phase"] = "resolved"
        await job.flush()
        return await _complete(
            ctx,
            {
                "status": "resolved",
                "outcome": "stopped",
                "note": "Irreversible step rejected; execution halted.",
                "results": job.get("results", []),
                "completed_steps": job.get("completed_steps", 0),
            },
        )

    # Human confirmed — mark the pending step cleared for execution, then continue.
    job["confirmed_step"] = job.get("pending_step_index")
    job["phase"] = "executing"
    await job.flush()
    return await _run_execution(ctx, job)


async def _run_execution(ctx: TaskContext[dict], job: Any) -> dict[str, Any]:
    """The long-running loop. Resumes from the checkpoint on recovery."""

    plan: list[dict[str, Any]] = job.get("plan", [])
    results: list[dict[str, Any]] = job.get("results", [])
    completed: int = int(job.get("completed_steps", 0) or 0)

    for idx in range(completed, len(plan)):
        step = plan[idx]

        # Gate: an irreversible step needs an explicit confirmation turn, unless
        # the human already confirmed *this* index (survives crashes via metadata).
        if step.get("irreversible") and job.get("confirmed_step") != idx:
            job["pending_step_index"] = idx
            job["phase"] = "awaiting_action_approval"
            await job.flush()
            return await _complete(
                ctx,
                {
                    "status": "awaiting_action_approval",
                    "next_step": {"index": idx, **step},
                    "completed_steps": completed,
                    "results": results,
                    "note": "Confirm the irreversible step: POST action=approve_action (or reject_action).",
                },
            )

        outcome = await _execute_step_once(ctx, job, idx, step)
        results.append({"index": idx, "action": step["action"], "outcome": outcome, "at": _now_iso()})
        job["results"] = results

        # Watermark: a crash after this flush resumes at idx+1, never re-running idx.
        completed = idx + 1
        job["completed_steps"] = completed
        job.pop("confirmed_step", None)
        job.pop("pending_step_index", None)
        await job.flush()

    job["phase"] = "resolved"
    await job.flush()
    return await _complete(
        ctx,
        {
            "status": "resolved",
            "outcome": "completed",
            "goal": job.get("goal"),
            "results": results,
            "summary": f"Completed {completed}/{len(plan)} steps.",
        },
    )


async def _execute_step_once(ctx: TaskContext[dict], job: Any, idx: int, step: dict[str, Any]) -> str:
    """Execute a step **at most once** across crashes (§6.2 of the tasks guide).

    For irreversible steps we reserve an idempotency token and flush it BEFORE
    the side effect, so a recovered run reuses the same token and a completed
    step is never repeated.
    """

    done: dict[str, Any] = job.get("step_outcomes", {})
    key = str(idx)
    if key in done:
        return done[key]

    if step.get("irreversible"):
        tokens: dict[str, Any] = job.get("step_tokens", {})
        if key not in tokens:
            tokens[key] = uuid.uuid4().hex
            job["step_tokens"] = tokens
            await job.flush()
        # Real impl: pass tokens[key] as an idempotency_key to the external API.
        logger.info("Executing irreversible step %d with token %s", idx, tokens[key])

    outcome = await _execute_step(step, ctx)

    done[key] = outcome
    job["step_outcomes"] = done
    await job.flush()
    return outcome


# ---------------------------------------------------------------------------
# Small helpers
# ---------------------------------------------------------------------------
async def _complete(ctx: TaskContext[dict], result: dict[str, Any]) -> dict[str, Any]:
    """Publish the per-invocation result to the default namespace, then return."""

    ctx.metadata["status"] = "completed"
    ctx.metadata["output"] = result
    await ctx.metadata.flush()
    return result


async def _sleep_or_defer(ctx: TaskContext[dict], seconds: float) -> None:
    """Cooldown that stays responsive to container shutdown.

    If the container is going down mid-work, hand the turn back to the next
    lifetime via ``exit_for_recovery`` instead of being force-killed.
    """

    try:
        await asyncio.wait_for(ctx.shutdown.wait(), timeout=seconds)
    except asyncio.TimeoutError:
        return
    await ctx.exit_for_recovery()


# ---------------------------------------------------------------------------
# Server + HTTP handlers
# ---------------------------------------------------------------------------
app = InvocationAgentServerHost(openapi_spec=OPENAPI_SPEC)

# In-memory convenience index so GET works with just an invocation_id while the
# process is alive. The authoritative, crash-surviving state is the task store;
# this map is only a lookup shortcut (GET also accepts ?agent_session_id=).
_inv_to_task: dict[str, str] = {}


def _task_id(session_id: str) -> str:
    return f"job-{session_id}"


def _resolve_task_id(request: Request, invocation_id: str) -> str | None:
    """Locate the job's task_id for a GET/cancel.

    On these routes the framework does not populate ``request.state.session_id``,
    so we resolve the job from (in priority order) the in-memory invocation index,
    an explicit ``?agent_session_id=`` query param, or the platform-provided
    session id when hosted.
    """

    if invocation_id in _inv_to_task:
        return _inv_to_task[invocation_id]
    session_id = request.query_params.get("agent_session_id") or getattr(
        request.state, "session_id", ""
    )
    return _task_id(session_id) if session_id else None


@app.invoke_handler
async def handle_invoke(request: Request) -> Response:
    """Start or resume the resilient chain for this job; return 202 immediately."""

    try:
        data = await request.json()
        if not isinstance(data, dict):
            raise ValueError
    except Exception:  # pylint: disable=broad-except
        return JSONResponse({"error": "Body must be a JSON object with an 'action'."}, status_code=400)

    session_id: str = request.state.session_id
    invocation_id: str = request.state.invocation_id
    task_id = _task_id(session_id)
    data["invocation_id"] = invocation_id

    try:
        await approval_workflow.start(task_id=task_id, input=data)
    except TaskConflictError:
        # The chain is mid-execution (in-flight, non-steerable). The caller
        # should wait for the current gate before posting the next decision.
        return JSONResponse(
            {"error": "Job is executing; wait for the next gate before posting again."},
            status_code=409,
        )

    _inv_to_task[invocation_id] = task_id
    return JSONResponse(
        {"session_id": session_id, "invocation_id": invocation_id, "status": "running"},
        status_code=202,
    )


async def _read_job_metadata(task_id: str) -> dict[str, Any] | None:
    """Read the default-namespace metadata the handler publishes for polling."""

    info = await get_task_manager().provider.get(task_id)
    if info is None:
        return None
    return (info.payload or {}).get("metadata") or {}


@app.get_invocation_handler
async def poll_invocation(request: Request) -> Response:
    """Poll a specific invocation's status/output from the resilient store."""

    invocation_id: str = request.state.invocation_id
    task_id = _resolve_task_id(request, invocation_id)
    if not task_id:
        return JSONResponse(
            {"error": "Provide ?agent_session_id=<id> to locate the job."},
            status_code=404,
        )

    meta = await _read_job_metadata(task_id)
    if meta is None:
        return JSONResponse({"error": "Job not found."}, status_code=404)
    if meta.get("invocation_id") != invocation_id:
        return JSONResponse(
            {"error": "This invocation is not the most recent for the job.", "current": meta.get("status")},
            status_code=404,
        )

    return JSONResponse(
        {"invocation_id": invocation_id, "status": meta.get("status"), "output": meta.get("output")}
    )


@app.cancel_invocation_handler
async def cancel_invocation(request: Request) -> Response:
    """Cancel the whole job — deletes the resilient chain (idempotent)."""

    invocation_id: str = request.state.invocation_id
    task_id = _resolve_task_id(request, invocation_id)
    if not task_id:
        return JSONResponse({"error": "Provide ?agent_session_id=<id> to locate the job."}, status_code=404)

    await approval_workflow.delete(task_id)
    return JSONResponse({"invocation_id": invocation_id, "status": "cancelled"})


def main() -> None:
    app.run()


if __name__ == "__main__":
    main()
