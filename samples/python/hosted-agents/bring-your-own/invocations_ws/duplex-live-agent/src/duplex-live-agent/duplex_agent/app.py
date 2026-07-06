# Copyright (c) Microsoft. All rights reserved.

"""DuplexLiveAgent — top-level orchestrator.

Wires Router + TaskManager + OutputScheduler.
Auto-generates tool schemas and system prompt from registered agents.
"""

from __future__ import annotations

import json
from typing import Any

from duplex_agent.base import AgentSpec, Router
from duplex_agent.output_scheduler import OutputScheduler
from duplex_agent.task_manager import TaskManager


def build_router_tools(specs: list[AgentSpec]) -> list[dict]:
    """Auto-generate the 5 router tool schemas from agent specs."""
    agent_names = [s.name for s in specs]
    desc_lines = "\n".join(f"  - {s.name}: {s.description}" for s in specs)

    return [
        {
            "name": "launch_async_task",
            "description": (
                f"Start a background task. Returns task_id immediately.\n"
                f"Available agents:\n{desc_lines}"
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "agent": {"type": "string", "enum": agent_names},
                    "task_description": {"type": "string"},
                },
                "required": ["agent", "task_description"],
            },
        },
        {
            "name": "check_async_task",
            "description": "Get status/result of a background task.",
            "parameters": {
                "type": "object",
                "properties": {"task_id": {"type": "string"}},
                "required": ["task_id"],
            },
        },
        {
            "name": "list_async_tasks",
            "description": "List all background tasks with statuses.",
            "parameters": {"type": "object", "properties": {}},
        },
        {
            "name": "cancel_async_task",
            "description": "Cancel a running background task.",
            "parameters": {
                "type": "object",
                "properties": {"task_id": {"type": "string"}},
                "required": ["task_id"],
            },
        },
        {
            "name": "update_async_task",
            "description": (
                "Send a message to a running task. Use to: answer its question, "
                "provide additional context, or send a correction."
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "task_id": {"type": "string"},
                    "message": {"type": "string"},
                },
                "required": ["task_id", "message"],
            },
        },
    ]


def build_system_prompt(specs: list[AgentSpec]) -> str:
    """Auto-generate the router system prompt with agent descriptions."""
    agent_block = "\n".join(f"  - **{s.name}**: {s.description}" for s in specs)
    return (
        "You are a real-time voice assistant with background task capabilities.\n"
        "\n"
        "## Available agents:\n"
        f"{agent_block}\n"
        "\n"
        "## Rules:\n"
        "1. For non-trivial work, use launch_async_task. Acknowledge briefly, keep chatting.\n"
        "2. You can launch multiple tasks simultaneously.\n"
        "3. Task updates arrive as system messages — relay them in 1-2 sentences.\n"
        "4. When a task asks a question, ask the user. Use update_async_task when they reply.\n"
        "5. After launching, NEVER call check_async_task immediately — results are pushed to you.\n"
        "6. When the user asks about progress or status of an existing task, use check_async_task.\n"
        "7. Keep all responses SHORT — this is real-time voice.\n"
        "8. Always use the full task_id — never truncate.\n"
    )


class DuplexLiveAgent:
    """Top-level duplex live agent. Each call to handle_session() runs one user session.

    Usage::

        from duplex_agent import DuplexLiveAgent

        agent = DuplexLiveAgent(
            router_class=RealtimeRouter,
            router_kwargs={"endpoint": ..., "model": ...},
            subagents=[...],  # list of AgentSpec
        )
        # In your WebSocket handler:
        await agent.handle_session(websocket)
    """

    def __init__(
        self,
        router_class: type[Router],
        router_kwargs: dict[str, Any],
        subagents: list[AgentSpec],
        gap_threshold_s: float = 0.5,
        milestone_rate_limit_s: float = 5.0,
    ):
        self._router_class = router_class
        self._base_router_kwargs = router_kwargs
        self._agents = subagents
        self._gap_threshold = gap_threshold_s
        self._milestone_rate_limit = milestone_rate_limit_s

        # Pre-compute (same for all sessions)
        self._tools = build_router_tools(self._agents)
        self._system_prompt = build_system_prompt(self._agents)

    async def handle_session(self, transport: Any) -> None:
        """Handle one user session (one WebSocket connection)."""

        # Per-session instances
        router = self._router_class(
            **self._base_router_kwargs,
            tools=self._tools,
            system_prompt=self._system_prompt,
        )
        task_manager = TaskManager(self._agents)
        scheduler = OutputScheduler(
            router=router,
            output_queue=task_manager.output_queue,
            gap_threshold_s=self._gap_threshold,
            milestone_rate_limit_s=self._milestone_rate_limit,
        )

        # Wire tool dispatch
        async def dispatch_tool(name: str, args: dict) -> str:
            if name == "launch_async_task":
                return await task_manager.launch(args["agent"], args["task_description"])
            elif name == "check_async_task":
                return task_manager.check(args["task_id"])
            elif name == "list_async_tasks":
                return task_manager.list_tasks()
            elif name == "cancel_async_task":
                return await task_manager.cancel(args["task_id"])
            elif name == "update_async_task":
                return task_manager.update(args["task_id"], args["message"])
            return json.dumps({"error": f"Unknown tool: {name}"})

        router.set_tool_handler(dispatch_tool)

        # Start
        await router.start(transport)
        await scheduler.start()
        try:
            await router.run_until_disconnect()
        finally:
            await scheduler.stop()
            await router.stop()
