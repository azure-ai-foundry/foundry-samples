# Copyright (c) Microsoft. All rights reserved.

r"""Resilient steerable agent (responses protocol).

A crash-resilient, **steerable** long-running agent built with
azure-ai-agentserver-responses. It shows how the cancellation policy and the
crash-recovery contract compose when steering, client cancel, and shutdown
interleave with crash recovery.

Two opt-in options drive the behavior (both default to ``False``):

- ``resilient_background=True`` — a ``store=true, background=true`` response
  survives process crashes: the framework persists handler progress and
  re-invokes the handler on the next process start if a prior attempt did not
  reach a terminal event.
- ``steerable_conversations=True`` — a client can POST a new turn on an
  in-flight conversation. The running handler is woken via the cancellation
  signal (distinguished by ``context.pending_input_count > 0``), winds the
  current turn down cleanly, and the framework re-invokes with the new input.

Recovery strategy here is deliberately **naive**: this handler wraps a
non-deterministic upstream (an LLM) and does NOT checkpoint partial output, so
recovery needs no special code — every entry builds a fresh stream and re-runs
the turn from scratch. The fresh ``response.in_progress`` (empty output) is the
client-visible reset. A ``turn_count`` watermark on
``context.conversation_chain_metadata`` survives crashes and turn boundaries.

The LLM here is simulated so the sample runs offline with no credentials;
replace ``_simulate_llm_stream`` with a real model call to make it live.

Required environment variables (only when you wire a real model):
    FOUNDRY_PROJECT_ENDPOINT: Foundry project endpoint (auto-injected by the platform).
    AZURE_AI_MODEL_DEPLOYMENT_NAME: Model deployment name (e.g., gpt-5.4-mini).

Usage::

    python main.py

    # Turn 1 (background + stored so it is resilient and steerable)
    curl -N -X POST http://localhost:8088/responses \
        -H "Content-Type: application/json" \
        -d '{"model": "agent", "input": "Explain quantum computing",
             "store": true, "background": true}'

    # Steer — supersede turn 1 with a new turn on the same conversation
    curl -X POST http://localhost:8088/responses \
        -H "Content-Type: application/json" \
        -d '{"model": "agent", "input": "Actually explain relativity",
             "store": true, "background": true, "previous_response_id": "<id>"}'

    # Simulate a mid-stream shutdown to exercise the recovery path
    SIMULATE_SHUTDOWN_MS=200 python main.py
"""

import asyncio
import logging
import os

from azure.ai.agentserver.responses import (
    CreateResponse,
    ResponseContext,
    ResponseEventStream,
    ResponsesAgentServerHost,
    ResponsesServerOptions,
)
from azure.ai.agentserver.core.tasks import set_resilient_tasks_enabled

logger = logging.getLogger("resilient-steering")

if not os.environ.get("APPLICATIONINSIGHTS_CONNECTION_STRING"):
    logger.warning(
        "APPLICATIONINSIGHTS_CONNECTION_STRING not set — traces will not be sent to "
        "Application Insights. It is auto-injected in hosted Foundry containers."
    )

options = ResponsesServerOptions(
    resilient_background=True,
    steerable_conversations=True,
)
app = ResponsesAgentServerHost(options=options)

# Explicitly opt into resilient-task startup recovery. The Responses framework
# already registers its internal durable tasks at host construction (so recovery
# runs regardless); this call just makes the opt-in intent explicit, mirroring
# the invocations resilient samples.
set_resilient_tasks_enabled(True)

_SIMULATE_SHUTDOWN_MS = int(os.environ.get("SIMULATE_SHUTDOWN_MS", "0"))


async def _simulate_llm_stream(prompt: str):
    """Simulate an LLM producing tokens. Replace with your real LLM call."""
    words = f"Let me explain {prompt} in detail. Comprehensive answer here.".split()
    for word in words:
        await asyncio.sleep(0.05)
        yield word + " "


@app.response_handler
async def handler(
    request: CreateResponse,
    context: ResponseContext,
    cancellation_signal: asyncio.Event,
):
    """Steerable resilient handler with cancellation × recovery composition."""
    # ── Recovery: naive re-run ──────────────────────────────────────
    # This handler wraps a non-deterministic upstream and does NOT checkpoint
    # partial output, so recovery needs NO special code: build a fresh stream on
    # every entry (recovered or not). The fresh ``response.in_progress`` (empty
    # output) below IS the client-visible reset — the turn re-runs from scratch.
    stream = ResponseEventStream(response_id=context.response_id, request=request)

    yield stream.emit_created()

    # ── Pre-entry cancellation / shutdown check ─────────────────────
    # Shutdown and cancellation are independent, mutually exclusive surfaces —
    # check shutdown FIRST. (Shutdown does NOT fire the cancellation signal.)
    if context.shutdown.is_set():
        # Graceful shutdown before we started: defer to next-lifetime recovery
        # (the framework re-invokes us on restart).
        await context.exit_for_recovery()
    if cancellation_signal.is_set():
        if context.pending_input_count > 0:
            # Steering pre-entry: emit completed so the partial output (none
            # here) becomes valid context for the drain turn that follows.
            yield stream.emit_completed()
        # Otherwise: client-cancelled (framework forces ``cancelled``) —
        # return silently without a terminal.
        return

    yield stream.emit_in_progress()

    # Cross-turn state: bump the turn counter. This survives crashes and turn
    # boundaries since it lives on ``context.conversation_chain_metadata``.
    turn_count = int(context.conversation_chain_metadata.get("turn_count", 0)) + 1
    context.conversation_chain_metadata["turn_count"] = turn_count

    # Optional local shutdown simulation.
    shutdown_timer: asyncio.Task | None = None
    if _SIMULATE_SHUTDOWN_MS > 0:
        shutdown_timer = asyncio.create_task(_simulate_shutdown(context))

    message = stream.add_output_item_message()
    yield message.emit_added()
    text = message.add_text_content()
    yield text.emit_added()

    input_text = await context.get_input_text()
    accumulated = ""

    # ── Mid-stream cancellation / shutdown check ────────────────────
    async for token in _simulate_llm_stream(input_text):
        if cancellation_signal.is_set() or context.shutdown.is_set():
            break
        accumulated += token
        yield text.emit_delta(token)

    # Always close builders so the persisted event stream is well-formed — even
    # on a cancelled / steered turn. The partial content is valid context for
    # steerable conversations.
    yield text.emit_text_done(accumulated.strip())
    yield text.emit_done()
    yield message.emit_done()

    if shutdown_timer and not shutdown_timer.done():
        shutdown_timer.cancel()

    # ── Post-stream shutdown check ──────────────────────────────────
    # Shutdown mid-stream: defer to next-lifetime recovery so the framework
    # re-invokes us; the recovery branch above re-streams from scratch.
    if context.shutdown.is_set():
        await context.exit_for_recovery()

    # All other cases (steered, client-cancelled, normal completion): emit the
    # terminal event. The framework overrides status for client-cancel; for
    # steered, partial output is valid context.
    yield stream.emit_completed()


async def _simulate_shutdown(context: ResponseContext) -> None:
    """Fire the shutdown signal after a delay (local testing only)."""
    await asyncio.sleep(_SIMULATE_SHUTDOWN_MS / 1000.0)
    context.shutdown.set()


def main() -> None:
    app.run()


if __name__ == "__main__":
    main()
