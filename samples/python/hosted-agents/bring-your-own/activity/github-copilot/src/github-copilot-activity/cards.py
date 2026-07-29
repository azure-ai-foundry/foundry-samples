# Copyright (c) Microsoft. All rights reserved.
"""Adaptive Card UI for the to-do list.

Renders the current tasks as an interactive Adaptive Card with **Universal
Actions** (`Action.Execute`). Button taps arrive back as an
``adaptiveCard/action`` invoke activity; the handler mutates the store and
returns a **fresh card** so the message updates in place.

Works in Microsoft Teams (personal + group scope). Copilot's rich interactive
card support is more limited, so we also keep the plain-text/model path.
"""

from __future__ import annotations

import logging
from typing import Any

from microsoft_agents.activity import Attachment

logger = logging.getLogger("github-copilot.cards")

_ADAPTIVE_CONTENT_TYPE = "application/vnd.microsoft.card.adaptive"
ADAPTIVE_ACTION_INVOKE = "adaptiveCard/action"


def build_tasks_card(conversation_id: str, tasks: list[dict[str, Any]]) -> dict:
    """Build an Adaptive Card (v1.5) listing tasks with Complete/Delete buttons."""
    body: list[dict] = [
        {
            "type": "TextBlock",
            "text": "📋 Your to-dos",
            "weight": "Bolder",
            "size": "Medium",
            "wrap": True,
        }
    ]

    if not tasks:
        body.append({"type": "TextBlock", "text": "_No tasks yet. Add one below._", "wrap": True, "isSubtle": True})
    else:
        for t in tasks:
            done = t.get("done")
            title = t.get("title", "")
            tid = t.get("id", "")
            columns = [
                {
                    "type": "Column",
                    "width": "auto",
                    "items": [{"type": "TextBlock", "text": "✅" if done else "⬜", "size": "Medium"}],
                    "verticalContentAlignment": "Center",
                },
                {
                    "type": "Column",
                    "width": "stretch",
                    "items": [{
                        "type": "TextBlock",
                        "text": (f"~~{title}~~" if done else title),
                        "wrap": True,
                        "isSubtle": bool(done),
                    }],
                    "verticalContentAlignment": "Center",
                },
            ]
            # Action buttons per row.
            actions = []
            if not done:
                actions.append({
                    "type": "Action.Execute",
                    "title": "Done",
                    "verb": "complete_task",
                    "data": {"taskId": tid},
                })
            actions.append({
                "type": "Action.Execute",
                "title": "Delete",
                "verb": "delete_task",
                "data": {"taskId": tid},
                "style": "destructive",
            })
            columns.append({
                "type": "Column",
                "width": "auto",
                "items": [{"type": "ActionSet", "actions": actions}],
                "verticalContentAlignment": "Center",
            })
            body.append({"type": "ColumnSet", "columns": columns, "separator": True})

    # Add-task input + button at the bottom.
    body.append({
        "type": "Input.Text",
        "id": "newTask",
        "placeholder": "Add a new task…",
    })

    card = {
        "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
        "type": "AdaptiveCard",
        "version": "1.5",
        "body": body,
        "actions": [
            {"type": "Action.Execute", "title": "➕ Add", "verb": "add_task"},
            {"type": "Action.Execute", "title": "🔄 Refresh", "verb": "refresh_tasks"},
        ],
        # NOTE: intentionally NO top-level "refresh" block. An auto-refresh action
        # fires on every render; combined with sending a fresh card on each invoke
        # it creates an infinite render→refresh loop. Refresh is manual (button).
    }
    return card


def tasks_card_attachment(conversation_id: str, tasks: list[dict[str, Any]]) -> Attachment:
    """Wrap the tasks card as an Attachment for send_activity."""
    return Attachment(
        content_type=_ADAPTIVE_CONTENT_TYPE,
        content=build_tasks_card(conversation_id, tasks),
    )


def card_invoke_response(card: dict) -> dict:
    """Build the invoke response body Teams expects for an Action.Execute that
    returns a refreshed Adaptive Card."""
    return {
        "statusCode": 200,
        "type": "application/vnd.microsoft.card.adaptive",
        "value": card,
    }


def parse_action(activity) -> tuple[str, dict] | None:
    """Extract (verb, data) from an adaptiveCard/action invoke activity."""
    value = getattr(activity, "value", None)
    if value is None:
        return None
    if not isinstance(value, dict):
        dump = getattr(value, "model_dump", None)
        value = dump(by_alias=True) if callable(dump) else None
    if not isinstance(value, dict):
        return None
    action = value.get("action") or {}
    if not isinstance(action, dict):
        dump = getattr(action, "model_dump", None)
        action = dump(by_alias=True) if callable(dump) else {}
    verb = action.get("verb")
    data = action.get("data") or {}
    if not verb:
        return None
    return (verb, data if isinstance(data, dict) else {})
