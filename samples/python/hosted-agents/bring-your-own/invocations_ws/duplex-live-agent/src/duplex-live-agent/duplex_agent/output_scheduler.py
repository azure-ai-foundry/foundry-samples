# Copyright (c) Microsoft. All rights reserved.

"""OutputScheduler — gap-aware delivery of background task events.

Reads from TaskManager.output_queue and injects messages into the Router
when the conversation is idle (bot not speaking, user not speaking).
"""

from __future__ import annotations

import asyncio
import logging

from duplex_agent.base import Router, TaskEvent, TaskEventType

logger = logging.getLogger(__name__)


class OutputScheduler:
    """Deliver background agent events to the user during conversational gaps."""

    def __init__(
        self,
        router: Router,
        output_queue: asyncio.Queue[TaskEvent],
        gap_threshold_s: float = 0.5,
        milestone_rate_limit_s: float = 5.0,
    ):
        self._router = router
        self._queue = output_queue
        self._gap_threshold = gap_threshold_s
        self._milestone_rate_limit = milestone_rate_limit_s
        self._last_milestone: float = 0
        self._running = False
        self._task: asyncio.Task | None = None

    async def start(self) -> None:
        self._running = True
        self._task = asyncio.create_task(self._loop(), name="output-scheduler")

    async def stop(self) -> None:
        self._running = False
        if self._task:
            self._task.cancel()
            try:
                await self._task
            except asyncio.CancelledError:
                pass

    async def _loop(self) -> None:
        while self._running:
            try:
                event = await asyncio.wait_for(self._queue.get(), timeout=0.5)
            except (asyncio.TimeoutError, TimeoutError):
                continue

            await self._wait_for_gap(event)

            text = self._format(event)
            if text:
                await self._router.inject_message(text)
                logger.info("Delivered [%s] for task %s", event.type.value, event.task_id)

    async def _wait_for_gap(self, event: TaskEvent) -> None:
        """Wait until the router has been idle for the configured gap."""
        # Milestones get rate-limited
        if event.type == TaskEventType.MILESTONE:
            now = asyncio.get_running_loop().time()
            wait_needed = self._milestone_rate_limit - (now - self._last_milestone)
            if wait_needed > 0:
                await asyncio.sleep(wait_needed)
            self._last_milestone = asyncio.get_running_loop().time()

        # Wait for a stable conversational gap.  A single idle poll can happen
        # during very short pauses, so require router.is_idle() to remain true
        # for _gap_threshold seconds before injecting background speech.
        max_wait = 10.0 if event.type == TaskEventType.MILESTONE else 30.0
        loop = asyncio.get_running_loop()
        start = loop.time()
        idle_since: float | None = None

        while loop.time() - start < max_wait:
            now = loop.time()
            if self._router.is_idle():
                if self._gap_threshold <= 0:
                    return
                idle_since = idle_since or now
                if now - idle_since >= self._gap_threshold:
                    return
            else:
                idle_since = None

            await asyncio.sleep(0.1)

        logger.debug(
            "Delivering [%s] for task %s after waiting %.1fs without a stable %.1fs gap",
            event.type.value,
            event.task_id,
            max_wait,
            self._gap_threshold,
        )

    def _format(self, event: TaskEvent) -> str | None:
        """Format event as a system message for the router LLM to rephrase."""
        if event.type == TaskEventType.MILESTONE:
            return (
                f"[Background task '{event.agent_name}' (id:{event.task_id}) progress]: "
                f"{event.content}. "
                f"Briefly update the user in one sentence."
            )
        elif event.type == TaskEventType.QUESTION:
            return (
                f"[Background task '{event.agent_name}' (id:{event.task_id}) needs input]: "
                f"Question: \"{event.content}\". "
                f"Ask the user this question conversationally. "
                f"When they answer, call update_async_task with task_id='{event.task_id}'."
            )
        elif event.type == TaskEventType.COMPLETE:
            content = event.content[:800] if event.content else ""
            return (
                f"[Background task '{event.agent_name}' (id:{event.task_id}) DONE]: "
                f"Result: {content}. "
                f"Summarize the key findings for the user conversationally."
            )
        elif event.type == TaskEventType.ERROR:
            return (
                f"[Background task '{event.agent_name}' (id:{event.task_id}) FAILED]: "
                f"{event.content}. Let the user know briefly."
            )
        return None
