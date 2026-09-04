# Copyright (c) Microsoft. All rights reserved.

r"""Resilient streaming agent with reconnect (responses protocol).

A crash-resilient, **resumable-streaming** agent built with
azure-ai-agentserver-responses. It streams a multi-stage response and
checkpoints after each stage, so a client that disconnects — or a container
that crashes — can reconnect and continue from the last completed stage **with
no gap, no duplicated content, and no regeneration** of finished stages.

This is the canonical **framework-checkpoint** recovery pattern:

- The handler runs three stages (``analyze`` → ``generate`` → ``refine``) and
  emits **one output item per stage**, calling ``yield stream.checkpoint()``
  after each. A checkpoint durably persists the response snapshot — every
  finished output item, with its **original id**.
- On a recovered entry, the handler seeds the stream from
  ``context.persisted_response``, so the already-checkpointed stage items are
  present in ``stream.response.output`` with their original ids. It re-emits
  ``response.in_progress`` (the client-visible reset point, re-emitting those
  same items) and resumes at ``len(stream.response.output)`` — the first stage
  not yet checkpointed.
- A stage interrupted **before** its checkpoint is simply re-run — correct by
  construction, no watermark bookkeeping, no LLM output stored in metadata.

How reconnect works for the client: because the response is
``store=true, background=true``, the framework persists the SSE event stream.
A client that drops mid-stream reconnects to the same ``response_id`` (the
Responses API replays persisted events from a cursor, then live-tails the
continuation). A container crash is handled by ``resilient_background=True``:
the handler is re-invoked on restart and resumes from the last checkpoint.

Good for: long-form document/report generation, deep-research answers,
code generation, and any expensive multi-stage generation where a client
disconnect or container crash must not lose (or re-bill for) completed work.

The stages here stream simulated tokens so the sample runs offline with no
credentials. Replace ``_stage_tokens`` with a real model call (e.g. Azure
OpenAI via ``AIProjectClient(...).get_openai_client()``) to make it live.

Environment:

- ``SIMULATE_CRASH_AFTER_STAGE`` — hard-crash the process (``os._exit(1)``) right
  after the given stage index (0-based) is checkpointed, to exercise crash
  recovery locally. On restart the handler is re-invoked and resumes from the
  next stage. Default: ``-1`` (disabled). Best on Linux/WSL/container — see the
  README for the Windows caveat.

Usage::

    python main.py

    # Stream a resilient, resumable response (background + stored).
    curl -N -X POST http://localhost:8088/responses \
        -H "Content-Type: application/json" \
        -d '{"model": "streamer", "input": "Tell me a joke",
             "stream": true, "store": true, "background": true}'

    # Reconnect after a drop: re-open the stream for the same response id.
    curl -N "http://localhost:8088/responses/<response_id>?stream=true"

    # Simulate a crash after the first stage checkpoints; restart to resume.
    SIMULATE_CRASH_AFTER_STAGE=0 python main.py
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

logger = logging.getLogger("resilient-streaming")

if not os.environ.get("APPLICATIONINSIGHTS_CONNECTION_STRING"):
    logger.warning(
        "APPLICATIONINSIGHTS_CONNECTION_STRING not set — traces will not be sent to "
        "Application Insights. It is auto-injected in hosted Foundry containers."
    )

options = ResponsesServerOptions(resilient_background=True)
app = ResponsesAgentServerHost(options=options)

# Explicitly opt into resilient-task startup recovery. The Responses framework
# already registers its internal durable tasks at host construction (so recovery
# runs regardless); this call just makes the opt-in intent explicit.
set_resilient_tasks_enabled(True)

# Local recovery-testing hook: hard-crash the process right after the stage with
# this index checkpoints. A hard crash (os._exit) exercises the framework's
# lease-based recovery — the handler is re-invoked on restart and resumes from
# ``context.persisted_response``. (This is the correct way to test recovery: a
# real crash, not a simulated graceful shutdown. Manually setting
# ``context.shutdown`` does NOT set the underlying task shutdown, so
# ``exit_for_recovery`` would reject it.)
_SIMULATE_CRASH_AFTER_STAGE = int(os.environ.get("SIMULATE_CRASH_AFTER_STAGE", "-1"))

# Stages run in order. Each emits one message output item and is made resilient
# with a ``stream.checkpoint()`` after its ``output_item.done``.
_STAGE_ORDER: tuple[str, ...] = ("analyze", "generate", "refine")


async def _stage_tokens(stage: str, prompt: str):
    """Simulated upstream — produce a few tokens for the given stage.

    Replace with your real LLM call, document analysis, etc.
    """
    text = {
        "analyze": f"[analyze] Examining input: '{prompt}'.",
        "generate": f"[generate] Drafting response for: '{prompt}'.",
        "refine": f"[refine] Polished result for: '{prompt}'.",
    }[stage]
    for token in text.split():
        await asyncio.sleep(0.03)
        yield token + " "


@app.response_handler
async def handler(
    request: CreateResponse,
    context: ResponseContext,
    cancellation_signal: asyncio.Event,
):
    """Three-stage resilient streaming handler with framework checkpoints."""
    # ── Recovery branch ─────────────────────────────────────────────
    # On recovery, seed the stream from the last resiliently-checkpointed
    # snapshot. The completed stages' items are already in
    # ``stream.response.output`` (carrying their ORIGINAL ids), so we resume
    # from their count. This run's ``response.in_progress`` re-emits those same
    # items and IS the client-visible snapshot reset point.
    if context.is_recovery and context.persisted_response is not None:
        stream = ResponseEventStream(
            response_id=context.response_id,
            response=context.persisted_response,
        )
        start = len(stream.response.get("output") or [])
    else:
        stream = ResponseEventStream(response_id=context.response_id, request=request)
        start = 0

    yield stream.emit_created()  # library dedups the store write on recovery

    # ── Pre-entry cancellation / shutdown check ─────────────────────
    # This sample does NOT enable steerable_conversations, so STEERED cannot
    # occur. Shutdown and client-cancel are independent, mutually exclusive
    # surfaces — check shutdown FIRST.
    if context.shutdown.is_set():
        # Graceful shutdown before we started: defer to next-lifetime recovery.
        await context.exit_for_recovery()
    if cancellation_signal.is_set():
        # Client-cancelled: return without a terminal (framework forces
        # ``cancelled``).
        return

    yield stream.emit_in_progress()

    input_text = await context.get_input_text()

    # Run stages starting at the first one not yet checkpointed.
    for index, stage in enumerate(_STAGE_ORDER):
        if index < start:
            continue
        message = stream.add_output_item_message()
        yield message.emit_added()
        text = message.add_text_content()
        yield text.emit_added()

        accumulated = ""
        async for token in _stage_tokens(stage, input_text):
            if cancellation_signal.is_set() or context.shutdown.is_set():
                break
            accumulated += token
            yield text.emit_delta(token)

        # Always close builders for the current stage so the persisted event
        # stream is well-formed even if the stage was cancelled.
        yield text.emit_text_done(accumulated.strip())
        yield text.emit_done()
        yield message.emit_done()

        # ── Mid-stream cancellation / shutdown check ────────────────
        # If cancelled or shutdown mid-stage, do NOT checkpoint — the stage is
        # not resiliently committed, so a recovered attempt re-runs it.
        if cancellation_signal.is_set() or context.shutdown.is_set():
            break

        # Stage finished cleanly — checkpoint it. The framework persists the
        # response snapshot (this stage's item + all prior), so recovery resumes
        # past it. Backpressured: the write completes before the yield returns.
        yield stream.checkpoint()

        # Local recovery test: hard-crash right after this stage is committed.
        if index == _SIMULATE_CRASH_AFTER_STAGE:
            logger.warning("SIMULATE_CRASH_AFTER_STAGE=%d: hard-crashing after stage '%s'", index, stage)
            os._exit(1)

    # ── Post-stream shutdown check ──────────────────────────────────
    # Shutdown mid-stream: defer to next-lifetime recovery so the framework
    # re-invokes us; the recovery branch above picks up from the last
    # checkpointed stage via ``context.persisted_response``.
    if context.shutdown.is_set():
        await context.exit_for_recovery()

    yield stream.emit_completed()


def main() -> None:
    app.run()


if __name__ == "__main__":
    main()
