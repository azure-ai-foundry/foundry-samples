# Copyright (c) Microsoft. All rights reserved.

"""Core ABCs and data classes for the Duplex Live Agent framework."""

from __future__ import annotations

import asyncio
from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from datetime import datetime, timezone
from enum import Enum
from typing import Any, Awaitable, Callable


# ---------------------------------------------------------------------------
# Data classes
# ---------------------------------------------------------------------------


@dataclass
class AgentConfig:
    """Shared configuration passed to agent builders."""

    endpoint: str
    model: str
    skills_dir: str | None = None


class TaskEventType(str, Enum):
    """Types of events emitted by background task agents."""

    MILESTONE = "milestone"
    QUESTION = "question"
    COMPLETE = "complete"
    ERROR = "error"


@dataclass
class TaskEvent:
    """Event emitted by a background task agent."""

    type: TaskEventType
    task_id: str
    agent_name: str
    content: str
    timestamp: datetime = field(default_factory=lambda: datetime.now(timezone.utc))


@dataclass
class TaskState:
    """Tracked state of a spawned background task."""

    task_id: str
    agent_name: str
    description: str
    status: str = "running"  # running | success | error | cancelled
    result: str | None = None
    pending_question: str | None = None
    created_at: datetime = field(default_factory=lambda: datetime.now(timezone.utc))


@dataclass
class AgentSpec:
    """Registration entry for a background agent type.

    ``factory`` receives (task_id, output_queue) and returns an AsyncTaskAgent.

    Example::

        AgentSpec(
            name="handoff",
            description="Customer support handoff workflow.",
            factory=HandoffTaskAgent.create_factory(workflow=my_workflow),
        )
    """

    name: str
    description: str
    factory: Callable[[str, asyncio.Queue[TaskEvent]], "AsyncTaskAgent"]


# ---------------------------------------------------------------------------
# Router ABC
# ---------------------------------------------------------------------------


class Router(ABC):
    """Foreground conversational agent — user-facing LLM.

    Responsibilities:
    - Real-time conversation (voice or text)
    - Tool/function calling for task orchestration
    - Receives proactive messages from OutputScheduler
    """

    def __init__(self, tools: list[dict], system_prompt: str):
        self._tools = tools
        self._system_prompt = system_prompt
        self._on_tool_call: Callable[[str, dict], Awaitable[str]] | None = None

    def set_tool_handler(self, handler: Callable[[str, dict], Awaitable[str]]) -> None:
        """Register callback: router LLM calls a tool -> handler dispatches."""
        self._on_tool_call = handler

    @abstractmethod
    async def start(self, transport: Any) -> None:
        """Initialize the router with the given transport (WebSocket, etc.)."""
        ...

    @abstractmethod
    async def run_until_disconnect(self) -> None:
        """Block until the user disconnects."""
        ...

    @abstractmethod
    async def stop(self) -> None:
        """Graceful shutdown."""
        ...

    @abstractmethod
    async def inject_message(self, text: str, role: str = "system") -> None:
        """Inject a proactive message. Router speaks/displays it to the user."""
        ...

    @abstractmethod
    def is_idle(self) -> bool:
        """True if bot isn't speaking AND user isn't speaking."""
        ...

    @abstractmethod
    def is_speaking(self) -> bool:
        """True if bot is currently outputting audio/text."""
        ...


# ---------------------------------------------------------------------------
# AsyncTaskAgent ABC
# ---------------------------------------------------------------------------


class AsyncTaskAgent(ABC):
    """Background task agent that runs independently of the conversation.

    Communication primitives:
    - ``milestone(msg)``: push a progress update (non-blocking)
    - ``question(msg) -> str``: ask the user (blocks until answered)
    - return value: final result (pushed on completion)
    """

    name: str = "unnamed"
    description: str = "A background task agent"

    def __init__(self, task_id: str, output_queue: asyncio.Queue[TaskEvent]):
        self._task_id = task_id
        self._output_queue = output_queue
        self._answer_future: asyncio.Future[str] | None = None
        self._updates: list[str] = []

    async def milestone(self, message: str) -> None:
        """Emit a progress update (delivered during next conversational gap)."""
        self._output_queue.put_nowait(
            TaskEvent(
                type=TaskEventType.MILESTONE,
                task_id=self._task_id,
                agent_name=self.name,
                content=message,
            )
        )

    async def question(self, message: str) -> str:
        """Ask the user a question. Suspends until answered via update_async_task."""
        loop = asyncio.get_running_loop()
        self._answer_future = loop.create_future()
        self._output_queue.put_nowait(
            TaskEvent(
                type=TaskEventType.QUESTION,
                task_id=self._task_id,
                agent_name=self.name,
                content=message,
            )
        )
        return await self._answer_future

    @property
    def has_pending_question(self) -> bool:
        """True if the agent is blocked waiting for an answer."""
        return self._answer_future is not None and not self._answer_future.done()

    def deliver_answer(self, answer: str) -> None:
        """Called by TaskManager when the user answers a pending question."""
        if self._answer_future and not self._answer_future.done():
            self._answer_future.set_result(answer)

    def deliver_update(self, message: str) -> None:
        """Called by TaskManager for general updates (context, corrections).

        Subclasses can override to react to mid-flight updates.
        """
        self._updates.append(message)

    @abstractmethod
    async def run(self, task_description: str) -> str:
        """Execute the task. Return final result as string.

        Use ``self.milestone()`` for progress updates.
        Use ``self.question()`` to ask the user for input.
        Raise ``asyncio.CancelledError`` for graceful cancellation.
        """
        ...
