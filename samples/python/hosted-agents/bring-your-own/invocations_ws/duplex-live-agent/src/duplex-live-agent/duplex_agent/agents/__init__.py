# Copyright (c) Microsoft. All rights reserved.

"""Background agent registry.

Each agent class exposes a ``build(config) -> AgentSpec | None`` classmethod.
Call ``load_agents(config)`` to build all available agents.
"""

from __future__ import annotations

import logging

from duplex_agent.base import AgentConfig, AgentSpec
from duplex_agent.agents.copilot import CopilotAgent
from duplex_agent.agents.handoff_maf import HandoffAgent

logger = logging.getLogger(__name__)

# Register agent builders here — order determines priority.
_REGISTRY: list[type] = [
    HandoffAgent,
    CopilotAgent,
]

__all__ = ["AgentConfig", "load_agents"]


def load_agents(config: AgentConfig) -> list[AgentSpec]:
    """Build all available sub-agents from a shared config.

    Each builder returns an AgentSpec or None (if its prerequisites aren't met).
    """
    agents: list[AgentSpec] = []
    for builder in _REGISTRY:
        spec = builder.build(config)
        if spec is not None:
            agents.append(spec)
            logger.debug("Loaded agent: %s", spec.name)
    return agents
