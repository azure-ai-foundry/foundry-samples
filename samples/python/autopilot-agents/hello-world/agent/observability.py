"""Configure Microsoft OpenTelemetry for Foundry and Agent 365."""

from __future__ import annotations

import logging
import os
from collections.abc import Awaitable, Callable
from typing import Any

from microsoft.opentelemetry import use_microsoft_opentelemetry
from microsoft.opentelemetry.a365.hosting import (
    BaggageMiddleware,
    OutputLoggingMiddleware,
)
from opentelemetry.sdk.resources import Resource


def configure_observability() -> None:
    """Enable Azure Monitor and Agent 365 telemetry before app imports."""
    logging.getLogger("agent").setLevel(logging.INFO)

    attributes = {
        "service.name": os.environ.get(
            "FOUNDRY_AGENT_NAME",
            "hello-world-autopilot",
        ),
        "service.namespace": "microsoft-foundry.autopilot",
    }
    agent_version = os.environ.get("FOUNDRY_AGENT_VERSION")
    if agent_version:
        attributes["service.version"] = agent_version

    use_microsoft_opentelemetry(
        resource=Resource.create(attributes),
        enable_azure_monitor=bool(
            os.environ.get("APPLICATIONINSIGHTS_CONNECTION_STRING")
        ),
        enable_a365=True,
        a365_enable_observability_exporter=True,
        instrumentation_options={
            "openai_agents": {"enabled": False},
        },
    )


class _MiddlewareCompatibilityAdapter:
    """Adapt distro middleware to the current Agents SDK callback contract."""

    def __init__(self, middleware: Any) -> None:
        self._middleware = middleware

    async def on_turn(
        self,
        context: Any,
        logic: Callable[[Any], Awaitable[None]],
    ) -> None:
        async def call_next(*args: Any) -> None:
            await logic(args[0] if args else context)

        await self._middleware.on_turn(context, call_next)


def configure_hosting_observability(middleware_set: Any) -> None:
    """Register Agent 365 middleware through the SDK compatibility adapter."""
    middleware_set.use(
        _MiddlewareCompatibilityAdapter(BaggageMiddleware()),
        _MiddlewareCompatibilityAdapter(OutputLoggingMiddleware()),
    )
