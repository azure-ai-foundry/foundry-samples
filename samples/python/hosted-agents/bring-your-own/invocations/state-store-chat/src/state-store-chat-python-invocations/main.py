# Copyright (c) Microsoft. All rights reserved.

"""State Store Chat — Bring Your Own Invocations agent with durable history.

A hosted agent that behaves like the Hello World invocations sample, but stores
its conversation history in **Foundry durable storage** instead of an in-memory
dict. History therefore survives container restarts, scale-out, and
redeployments.

The Invocations protocol does **not** provide built-in server-side conversation
history, so the agent persists it itself using ``FoundryStateStore`` from
``azure.ai.agentserver.core.storage`` — a durable, server-backed key-value store
scoped to the Foundry project.

Storage model:
    * One state store per conversation, named ``chat-history/<agent_session_id>``.
      Encoding the session id into the store name is how you scope data to a
      conversation (there is no separate session-isolation knob).
    * The full message list is kept as a single item under the key ``history``,
      with value ``{"messages": [{"role": ..., "content": ...}, ...]}``.
    * Each turn is appended with an optimistic-concurrency (``if_match``) guarded
      read-modify-write, so concurrent requests to the same session cannot
      lose each other's messages.

Required environment variables:
    FOUNDRY_PROJECT_ENDPOINT: Foundry project endpoint (auto-injected in hosted containers).
        Also used to resolve the durable-storage endpoint.
    AZURE_AI_MODEL_DEPLOYMENT_NAME: Model deployment name (declared in azure.yaml).

Optional environment variables:
    ENABLE_USER_ISOLATION: Set to "true" to partition each conversation's history
        per end user (see the user-isolation notes below). Defaults to off.

Usage::

    # Set environment variables
    export FOUNDRY_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
    export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-5.4-mini"

    # Start the agent
    python main.py

    # Turn 1 — start a new conversation
    curl -sS -N -X POST "http://localhost:8088/invocations?agent_session_id=chat-001" \\
        -H "Content-Type: application/json" \\
        -d '{"message": "My name is Ada. Remember it."}'

    # Restart the process, then continue the SAME conversation — history persists
    curl -sS -N -X POST "http://localhost:8088/invocations?agent_session_id=chat-001" \\
        -H "Content-Type: application/json" \\
        -d '{"message": "What is my name?"}'
"""

import json
import logging
import os

from starlette.requests import Request
from starlette.responses import JSONResponse, StreamingResponse

from azure.ai.projects.aio import AIProjectClient
from azure.identity.aio import DefaultAzureCredential

from azure.ai.agentserver.core.storage import (
    FoundryStateStore,
    FoundryStoragePreconditionError,
)
from azure.ai.agentserver.invocations import InvocationAgentServerHost

logger = logging.getLogger(__name__)

if not os.environ.get("APPLICATIONINSIGHTS_CONNECTION_STRING"):
    logger.warning(
        "APPLICATIONINSIGHTS_CONNECTION_STRING not set — traces will not be sent to "
        "Application Insights. Set it to enable local telemetry. "
        "(This variable is auto-injected in hosted Foundry containers — do not declare it in azure.yaml.)"
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
        "Set it to your model deployment name as declared in azure.yaml."
    )

# One shared credential drives both the model client and the storage client.
_credential = DefaultAzureCredential()
_project_client = AIProjectClient(endpoint=_endpoint, credential=_credential)

# Use the Responses API — not chat.completions (Chat Completions API is legacy).
_openai_client = _project_client.get_openai_client()

_SYSTEM_PROMPT = "You are a helpful AI assistant. Be concise and informative."

# Store-level TTL: idle conversations age out after 30 days. Any write to a
# conversation renews its window. Set to -1 to keep history forever.
_HISTORY_TTL_SECONDS = 30 * 24 * 60 * 60

# The single item key that holds the whole message list for a conversation.
_HISTORY_KEY = "history"

# Retries for the optimistic-concurrency guarded write when another request for
# the same session commits first.
_MAX_WRITE_ATTEMPTS = 3

# ── User isolation ────────────────────────────────────────────────────────────
# When enabled, item operations on a store are partitioned per end user, so two
# users sharing the same store name never see each other's items. This is a
# store-level property fixed at creation (get_or_create) — it cannot be toggled
# later. Turn it on with ENABLE_USER_ISOLATION=true.
#
# There are two ways the platform learns *which* user an item belongs to:
#   1. Direct callers — the platform derives the user identity from the caller's
#      token automatically. You only set user_isolation=True; you pass no id.
#   2. Trusted/delegated callers — a service calling on behalf of an end user
#      passes that user's id as `user_id`. The SDK then sends it as the
#      `x-ms-user-id` header on item operations (get_item/set_item/etc.), and the
#      platform partitions by that delegated id.
# Store-management calls (get_or_create/get/update/delete) stay store-scoped and
# never send the delegated header — isolation only applies to item operations.
_USER_ISOLATION = os.environ.get("ENABLE_USER_ISOLATION", "false").lower() == "true"

# Header a trusted upstream may set to name the end user it is acting for. Only
# consulted when user isolation is on; direct callers can leave it unset and let
# the platform derive identity from the token.
_DELEGATED_USER_ID_HEADER = "x-ms-user-id"

app = InvocationAgentServerHost()

# Cache one durable-store client per (conversation, user) pair. Each client owns
# an HTTP pipeline, so reuse it across turns instead of reconstructing it per
# request. With user isolation on, the delegated user id is part of the cache key
# because a store client carries that id (and thus the x-ms-user-id header).
_stores: dict[tuple[str, str | None], FoundryStateStore] = {}


async def _get_store(
    session_id: str, user_id: str | None = None
) -> FoundryStateStore:
    """Return a durable state store bound to this conversation, creating it once.

    The store name encodes the conversation scope. ``get_or_create`` resolves the
    server-side resource in a single call, so the store is ready for item reads
    and writes immediately.

    When ``_USER_ISOLATION`` is on, the store is created with
    ``user_isolation=True`` so its items are partitioned per user. For a trusted
    caller acting on behalf of an end user, ``user_id`` is forwarded so the SDK
    tags item operations with the delegated ``x-ms-user-id`` header; a direct
    caller can omit it and let the platform derive identity from the token.
    """
    cache_key = (session_id, user_id if _USER_ISOLATION else None)
    store = _stores.get(cache_key)
    if store is None:
        store = await FoundryStateStore.get_or_create(
            f"chat-history/{session_id}",
            credential=_credential,
            user_isolation=_USER_ISOLATION,
            # user_id only has an effect when the store is user-isolated; it is
            # ignored otherwise, so it is always safe to pass through.
            user_id=user_id,
            item_ttl_seconds=_HISTORY_TTL_SECONDS,
            description="Durable chat history for a single conversation.",
            tags={"scenario": "state-store-chat"},
        )
        _stores[cache_key] = store
    return store


async def _load_history(store: FoundryStateStore) -> list[dict[str, str]]:
    """Return the persisted messages for this conversation (empty if none yet)."""
    item = await store.get_item(_HISTORY_KEY)
    if item is None:
        return []
    return list(item.value.get("messages", []))


async def _append_turn(
    store: FoundryStateStore, user_message: str, assistant_message: str
) -> None:
    """Durably append one user/assistant turn with an ``if_match`` guard.

    Re-reads the latest history immediately before writing so a concurrent
    request that committed first is not clobbered; on a precondition failure it
    reloads and retries.
    """
    messages: list[dict[str, str]] = []
    for _ in range(_MAX_WRITE_ATTEMPTS):
        item = await store.get_item(_HISTORY_KEY)
        messages = list(item.value.get("messages", [])) if item is not None else []
        etag = item.etag if item is not None else None

        messages.append({"role": "user", "content": user_message})
        messages.append({"role": "assistant", "content": assistant_message})

        try:
            await store.set_item(_HISTORY_KEY, {"messages": messages}, if_match=etag)
            return
        except FoundryStoragePreconditionError:
            # Another request for this session wrote first — reload and retry.
            logger.info("Concurrent history write for a session; retrying")

    # Best-effort final write without the guard so the turn is not lost.
    await store.set_item(_HISTORY_KEY, {"messages": messages})


# ── Required handler ──────────────────────────────────────────────────────────
# @app.invoke_handler is the only handler you must implement. It receives every
# POST /invocations request. The function name below is arbitrary.
# ─────────────────────────────────────────────────────────────────────────────
@app.invoke_handler
async def handle_invoke(request: Request):
    """Handle a streaming multi-turn chat request with durable history."""
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
    # incoming request and exposes them via request.state.
    session_id = request.state.session_id
    invocation_id = request.state.invocation_id

    # For user-isolated stores, a trusted upstream may name the end user it is
    # acting for via the x-ms-user-id header. Direct callers can leave this unset
    # and let the platform derive identity from the token. Ignored when user
    # isolation is off.
    delegated_user_id = request.headers.get(_DELEGATED_USER_ID_HEADER)

    logger.info("Processing invocation %s (session %s)", invocation_id, session_id)

    # Load durable history for this conversation and build the model input. With
    # user isolation on, history is additionally partitioned by the end user, so
    # the same session_id yields separate transcripts per user.
    store = await _get_store(session_id, delegated_user_id)
    prior_messages = await _load_history(store)
    model_input = prior_messages + [{"role": "user", "content": user_message}]

    async def event_generator():
        full_reply = ""
        async for event in await _openai_client.responses.create(
            model=_model,
            instructions=_SYSTEM_PROMPT,
            input=model_input,
            store=False,
            stream=True,
        ):
            if event.type == "response.output_text.delta":
                full_reply += event.delta
                yield f"data: {json.dumps({'type': 'token', 'content': event.delta})}\n\n"

        # Persist the completed turn durably before signalling done.
        await _append_turn(store, user_message, full_reply)

        yield f"data: {json.dumps({'type': 'done', 'full_text': full_reply, 'session_id': session_id})}\n\n"

    return StreamingResponse(
        event_generator(),
        media_type="text/event-stream",
        headers={"Cache-Control": "no-cache"},
    )


if __name__ == "__main__":
    app.run()
