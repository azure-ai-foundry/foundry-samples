# Copyright (c) Microsoft. All rights reserved.

"""Duplex Live Agent framework — foreground router + background task agents."""

from duplex_agent.base import (
    AgentConfig,
    AgentSpec,
    AsyncTaskAgent,
    Router,
    TaskEvent,
    TaskEventType,
    TaskState,
)
from duplex_agent.app import DuplexLiveAgent

# Backwards compat
DuplexApp = DuplexLiveAgent

__all__ = [
    "AgentSpec",
    "AsyncTaskAgent",
    "DuplexApp",
    "DuplexLiveAgent",
    "Router",
    "TaskEvent",
    "TaskEventType",
    "TaskState",
]
