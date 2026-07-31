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
import logging
import os
import re
import uuid
from pathlib import Path
from typing import Any

from copilot import Tool, define_tool
from pydantic import BaseModel, Field

logger = logging.getLogger("github-copilot.tools")

# Persist under $HOME (durable across idle/recycle); fall back to cwd for local runs.
_HOME = os.environ.get("HOME") or "."
_STORE_DIR = Path(_HOME) / ".github-copilot" / "tasks"


class AddTaskParams(BaseModel):
    title: str = Field(description="The task description.")


class CompleteTaskParams(BaseModel):
    task_id: str = Field(description="The id of the task to mark done.")


class DeliverFileParams(BaseModel):
    path: str = Field(description="The absolute path of the file you created "
                                  "(with your shell / python tools) to send to "
                                  "the user for download.")


class _NoParams(BaseModel):
    pass


# ── Per-turn UI outbox ─────────────────────────────────────────────────────────
# Copilot SDK tools are pure functions and cannot send activities (cards/files)
# back to Teams. When the model calls a tool that should render UI, the tool
# records the request here; ``main.py`` drains it after the turn and attaches the
# card/file to the streaming response (the reliable delivery path).
_OUTBOX: dict[str, list[dict[str, Any]]] = {}


def queue_ui(conversation_id: str, action: dict[str, Any]) -> None:
    _OUTBOX.setdefault(conversation_id, []).append(action)


def drain_ui(conversation_id: str) -> list[dict[str, Any]]:
    return _OUTBOX.pop(conversation_id, [])



def _task_file(conversation_id: str) -> Path:
    # Sanitize the conversation id so it is safe to use as a file name.
    safe = re.sub(r"[^A-Za-z0-9_-]", "_", conversation_id) or "default"
    return _STORE_DIR / f"{safe}.json"


def _load_tasks(conversation_id: str) -> list[dict[str, Any]]:
    path = _task_file(conversation_id)
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError:
        return []
    except ValueError as exc:
        logger.warning("task store corrupt (%s) -> []", exc)
        return []


def _save_tasks(conversation_id: str, tasks: list[dict[str, Any]]) -> None:
    path = _task_file(conversation_id)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(tasks), encoding="utf-8")


# ── Public helpers for the Adaptive Card UI (bypass the model, act on the store
#    directly from card button actions). ────────────────────────────────────────

def get_tasks(conversation_id: str) -> list[dict[str, Any]]:
    """Public accessor for the current tasks of a conversation."""
    return _load_tasks(conversation_id)


def add_task_direct(conversation_id: str, title: str) -> dict[str, Any] | None:
    """Add a task directly (no model). Returns the new task, or None if blank/dupe."""
    title = (title or "").strip()
    if not title:
        return None
    tasks = _load_tasks(conversation_id)
    for t in tasks:
        if not t["done"] and t["title"].casefold() == title.casefold():
            return None
    task = {"id": uuid.uuid4().hex[:8], "title": title, "done": False}
    tasks.append(task)
    _save_tasks(conversation_id, tasks)
    return task


def complete_task_direct(conversation_id: str, task_id: str) -> bool:
    """Mark a task done directly. Returns True if found."""
    tasks = _load_tasks(conversation_id)
    for t in tasks:
        if t["id"] == task_id:
            t["done"] = True
            _save_tasks(conversation_id, tasks)
            return True
    return False


def delete_task_direct(conversation_id: str, task_id: str) -> bool:
    """Delete a task directly. Returns True if removed."""
    tasks = _load_tasks(conversation_id)
    new = [t for t in tasks if t["id"] != task_id]
    if len(new) == len(tasks):
        return False
    _save_tasks(conversation_id, new)
    return True



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
        # Render the interactive task board (drained + sent by main.py after the
        # turn). Return a terse instruction — NOT the task list — so the model
        # does not also enumerate the tasks in text (the card is the UI).
        queue_ui(conversation_id, {"type": "task_board"})
        tasks = _load_tasks(conversation_id)
        if not tasks:
            return ("Displayed an interactive task board to the user (it's empty). "
                    "Do not list tasks in text; just briefly invite them to add one.")
        return ("Displayed an interactive task board to the user showing their "
                f"{len(tasks)} task(s). Do not repeat or list the tasks in text; "
                "the card already shows them. Reply with at most a short sentence.")

    def _complete_task(params: CompleteTaskParams, _inv: Any) -> str:
        tasks = _load_tasks(conversation_id)
        for t in tasks:
            if t["id"] == params.task_id:
                t["done"] = True
                _save_tasks(conversation_id, tasks)
                return f"Marked '{t['title']}' as done."
        return f"No task with id '{params.task_id}'."

    def _deliver_file(params: DeliverFileParams, _inv: Any) -> str:
        # ChatGPT-style: the model creates the file itself using its built-in
        # shell / python tools (writing text directly, or using python-docx /
        # python-pptx / reportlab for Office / PDF), then calls this tool with
        # the path. We read the bytes after the turn and offer the file for
        # download via the Teams File Consent flow (see main._deliver_ui).
        path = (params.path or "").strip()
        if not path or not os.path.isfile(path):
            return (f"No file exists at '{path}'. Create the file first with your "
                    "shell/python tools, then call deliver_file with its path.")
        queue_ui(conversation_id, {"type": "file", "path": path})
        return f"I've prepared **{os.path.basename(path)}** and will offer it for download."

    return [
        define_tool("add_task", description="Add a task / to-do item.",
                    handler=_add_task, params_type=AddTaskParams),
        define_tool("list_tasks",
                    description="Show the user's to-do list. Use this whenever the "
                                "user wants to see, list, or review their tasks.",
                    handler=_list_tasks, params_type=_NoParams),
        define_tool("complete_task", description="Mark a task as done by its id.",
                    handler=_complete_task, params_type=CompleteTaskParams),
        define_tool("deliver_file",
                    description="Send a file you created to the user as a "
                                "download. First create the file yourself using "
                                "your shell / python tools (write text directly "
                                "for .txt/.md/.csv/.json/.html/code, or use "
                                "python-docx / python-pptx / reportlab for .docx / "
                                ".pptx / .pdf), then call this tool with the file's "
                                "path. Use this whenever the user asks you to write, "
                                "generate, or produce a file, document, essay, "
                                "report, or slides. You MUST actually create the "
                                "file and call this tool — never claim you attached "
                                "a file without doing so. Do not generate images.",
                    handler=_deliver_file, params_type=DeliverFileParams),
    ]
