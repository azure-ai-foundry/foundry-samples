# Copyright (c) Microsoft. All rights reserved.

"""Local authentication options for the Foundry A365 agent.

This module provides configuration options for authentication when running
the agent locally or in development scenarios. When ``BEARER_TOKEN`` is set,
the agent uses it as the bearer token attached to MCP tool requests instead
of acquiring an agentic-user token via the Microsoft 365 Agents SDK.
"""

from __future__ import annotations

import os
from dataclasses import dataclass


@dataclass
class LocalAuthenticationOptions:
    """Configuration options for local MCP tool server access."""

    env_id: str = ""
    bearer_token: str = ""

    def __post_init__(self) -> None:
        if not isinstance(self.env_id, str):
            self.env_id = str(self.env_id) if self.env_id else ""
        if not isinstance(self.bearer_token, str):
            self.bearer_token = str(self.bearer_token) if self.bearer_token else ""

    @property
    def is_valid(self) -> bool:
        return bool(self.env_id and self.bearer_token)

    def validate(self) -> None:
        if not self.env_id:
            raise ValueError("env_id is required for authentication")
        if not self.bearer_token:
            raise ValueError("bearer_token is required for authentication")

    @classmethod
    def from_environment(
        cls,
        env_id_var: str = "ENV_ID",
        token_var: str = "BEARER_TOKEN",
    ) -> "LocalAuthenticationOptions":
        """Build options from environment variables."""

        env_id = os.getenv(env_id_var, "")
        bearer_token = os.getenv(token_var, "")

        print(
            f"🔧 Environment ID: {env_id[:20]}{'...' if len(env_id) > 20 else ''}"
        )
        print(f"🔧 Bearer Token: {'***' if bearer_token else 'NOT SET'}")

        return cls(env_id=env_id, bearer_token=bearer_token)

    def to_dict(self) -> dict:
        return {"env_id": self.env_id, "bearer_token": self.bearer_token}
