# Copyright (c) Microsoft. All rights reserved.
"""The custom tool the Copilot SDK can call: a simple per-conversation to-do list.

Tasks are persisted to a small JSON file under ``$HOME`` so they survive sandbox
idle/recycle. Foundry hosted agents give each session a **persistent ``$HOME``**:
its contents are preserved when the compute is deprovisioned after idle and
restored when the session resumes, so files written there outlive the container.
See https://learn.microsoft.com/azure/foundry/agents/concepts/hosted-agents#session-storage
"""

from __future__ import annotations

import json
import os
import re
import uuid
from pathlib import Path
from typing import Any

from copilot import Tool, define_tool
from pydantic import BaseModel, Field

# Persist under $HOME (durable across idle/recycle); fall back to cwd for local runs.
_STORE_DIR = Path(os.environ.get("HOME") or ".") / ".github-copilot" / "tasks"


class AddTaskParams(BaseModel):
    title: str = Field(description="The task description.")


class CompleteTaskParams(BaseModel):
    task_id: str = Field(description="The id of the task to mark done.")


class _NoParams(BaseModel):
    pass


def _task_file(conversation_id: str) -> Path:
    # Sanitize the conversation id so it is safe to use as a file name.
    safe = re.sub(r"[^A-Za-z0-9_-]", "_", conversation_id) or "default"
    return _STORE_DIR / f"{safe}.json"


def _load_tasks(conversation_id: str) -> list[dict[str, Any]]:
    try:
        return json.loads(_task_file(conversation_id).read_text(encoding="utf-8"))
    except (FileNotFoundError, ValueError):
        return []


def _save_tasks(conversation_id: str, tasks: list[dict[str, Any]]) -> None:
    path = _task_file(conversation_id)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(tasks), encoding="utf-8")


def build_tools(conversation_id: str) -> list[Tool]:
    """Return the to-do tool set bound to ``conversation_id`` (file-backed)."""

    def _add_task(params: AddTaskParams, _inv: Any) -> str:
        title = (params.title or "").strip()
        tasks = _load_tasks(conversation_id)
        for t in tasks:  # idempotent: don't duplicate an open task
            if not t["done"] and t["title"].casefold() == title.casefold():
                return f"Task '{title}' is already on the list."
        task = {"id": uuid.uuid4().hex[:8], "title": title, "done": False}
        tasks.append(task)
        _save_tasks(conversation_id, tasks)
        return f"Added task '{title}' (id {task['id']})."

    def _list_tasks(_params: _NoParams, _inv: Any) -> str:
        tasks = _load_tasks(conversation_id)
        if not tasks:
            return "No tasks yet."
        return "Tasks:\n" + "\n".join(
            f"[{'x' if t['done'] else ' '}] {t['title']} (id {t['id']})" for t in tasks
        )

    def _complete_task(params: CompleteTaskParams, _inv: Any) -> str:
        tasks = _load_tasks(conversation_id)
        for t in tasks:
            if t["id"] == params.task_id:
                t["done"] = True
                _save_tasks(conversation_id, tasks)
                return f"Marked '{t['title']}' as done."
        return f"No task with id '{params.task_id}'."

    return [
        define_tool("add_task", description="Add a task / to-do item.",
                    handler=_add_task, params_type=AddTaskParams),
        define_tool("list_tasks", description="List the current tasks.",
                    handler=_list_tasks, params_type=_NoParams),
        define_tool("complete_task", description="Mark a task as done by its id.",
                    handler=_complete_task, params_type=CompleteTaskParams),
    ]
