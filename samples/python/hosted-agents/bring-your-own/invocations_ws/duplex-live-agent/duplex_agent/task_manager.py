# Copyright (c) Microsoft. All rights reserved.

"""TaskManager — background task lifecycle management.

Provides 5 tools (aligned with LangChain deepagents naming):
- launch_async_task   -> spawn agent, return task_id immediately
- check_async_task    -> get status/result
- list_async_tasks    -> all tasks with statuses
- cancel_async_task   -> cancel running task
- update_async_task   -> send a message to a running task
"""

from __future__ import annotations

import asyncio
import json
import logging
import uuid

from duplex_agent.base import AgentSpec, AsyncTaskAgent, TaskEvent, TaskEventType, TaskState

logger = logging.getLogger(__name__)


class TaskManager:
    """Manage the lifecycle of background task agents."""

    def __init__(self, specs: list[AgentSpec]):
        self._specs: dict[str, AgentSpec] = {s.name: s for s in specs}
        self._tasks: dict[str, TaskState] = {}
        self._agents: dict[str, AsyncTaskAgent] = {}
        self._asyncio_tasks: dict[str, asyncio.Task] = {}
        self.output_queue: asyncio.Queue[TaskEvent] = asyncio.Queue()

    # ------------------------------------------------------------------
    # Tool implementations (called by the router's tool handler)
    # ------------------------------------------------------------------

    async def launch(self, agent_name: str, task_description: str) -> str:
        """Spawn a background task. Returns task_id immediately."""
        if agent_name not in self._specs:
            return json.dumps(
                {"error": f"Unknown agent '{agent_name}'. Available: {list(self._specs)}"}
            )

        task_id = uuid.uuid4().hex
        spec = self._specs[agent_name]

        agent = spec.factory(task_id, self.output_queue)
        agent.name = spec.name
        agent.description = spec.description

        self._agents[task_id] = agent
        self._tasks[task_id] = TaskState(
            task_id=task_id,
            agent_name=agent_name,
            description=task_description,
        )

        self._asyncio_tasks[task_id] = asyncio.create_task(
            self._execute(task_id, agent, task_description),
            name=f"task-{agent_name}-{task_id}",
        )

        logger.info("Launched task %s (%s): %s", task_id, agent_name, task_description[:80])
        return json.dumps({"task_id": task_id, "agent": agent_name, "status": "running"})

    def check(self, task_id: str) -> str:
        """Get status/result of a background task."""
        task = self._tasks.get(task_id)
        if not task:
            return json.dumps({"error": f"Unknown task: {task_id}"})
        return json.dumps({
            "task_id": task.task_id,
            "agent": task.agent_name,
            "description": task.description,
            "status": task.status,
            "result": task.result[:500] if task.result else None,
            "pending_question": task.pending_question,
        })

    def list_tasks(self) -> str:
        """List all tasks with statuses."""
        tasks = [
            {
                "task_id": t.task_id,
                "agent": t.agent_name,
                "status": t.status,
                "description": t.description[:100],
            }
            for t in self._tasks.values()
        ]
        return json.dumps(tasks)

    async def cancel(self, task_id: str) -> str:
        """Cancel a running background task."""
        if task_id not in self._tasks:
            return json.dumps({"error": f"Unknown task: {task_id}"})
        if task_id in self._asyncio_tasks:
            self._asyncio_tasks[task_id].cancel()
        self._tasks[task_id].status = "cancelled"
        logger.info("Cancelled task %s", task_id)
        return json.dumps({"task_id": task_id, "status": "cancelled"})

    def update(self, task_id: str, message: str) -> str:
        """Send a message to a running task (answer, context, correction)."""
        agent = self._agents.get(task_id)
        if not agent:
            return json.dumps({"error": f"Unknown task: {task_id}"})
        task = self._tasks[task_id]
        if agent.has_pending_question:
            agent.deliver_answer(message)
            task.pending_question = None
            logger.info("Delivered answer to task %s: %s", task_id, message[:50])
            return json.dumps({"task_id": task_id, "status": "answer_delivered"})
        else:
            agent.deliver_update(message)
            logger.info("Sent update to task %s: %s", task_id, message[:50])
            return json.dumps({"task_id": task_id, "status": "update_sent"})

    # ------------------------------------------------------------------
    # Internal
    # ------------------------------------------------------------------

    async def _execute(self, task_id: str, agent: AsyncTaskAgent, description: str):
        """Run the agent and handle completion/failure."""
        try:
            result = await agent.run(description)
            self._tasks[task_id].status = "success"
            self._tasks[task_id].result = result
            self.output_queue.put_nowait(
                TaskEvent(
                    type=TaskEventType.COMPLETE,
                    task_id=task_id,
                    agent_name=agent.name,
                    content=result,
                )
            )
        except asyncio.CancelledError:
            self._tasks[task_id].status = "cancelled"
            logger.info("Task %s was cancelled", task_id)
        except Exception as e:
            self._tasks[task_id].status = "error"
            self._tasks[task_id].result = str(e)
            self.output_queue.put_nowait(
                TaskEvent(
                    type=TaskEventType.ERROR,
                    task_id=task_id,
                    agent_name=agent.name,
                    content=str(e),
                )
            )
            logger.exception("Task %s failed", task_id)
