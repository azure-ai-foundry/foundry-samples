# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

"""Preserve HTTP request correlation through the Agents SDK turn pipeline."""

from __future__ import annotations

from collections.abc import Awaitable, Callable
from contextvars import ContextVar

from aiohttp.web import Request, Response
from microsoft_agents.activity import Activity, InvokeResponse
from microsoft_agents.hosting.aiohttp import CloudAdapter
from microsoft_agents.hosting.core import Agent, ClaimsIdentity, TurnContext
from opentelemetry import propagate, trace
from opentelemetry.trace import Status, StatusCode


_TRACE_PARENT_ATTRIBUTE = "_foundry_request_trace_parent"
_request_trace_parent: ContextVar[str | None] = ContextVar(
    "foundry_request_trace_parent",
    default=None,
)


def _capture_current_trace_parent() -> str | None:
    carrier: dict[str, str] = {}
    propagate.inject(carrier)
    return carrier.get("traceparent")


class CorrelatingCloudAdapter(CloudAdapter):
    """Attach the current HTTP trace parent to each inbound activity."""

    async def process(self, request: Request, agent: Agent) -> Response | None:
        token = _request_trace_parent.set(_capture_current_trace_parent())
        try:
            return await super().process(request, agent)
        finally:
            _request_trace_parent.reset(token)

    async def process_activity(
        self,
        claims_identity: ClaimsIdentity,
        activity: Activity,
        callback: Callable[[TurnContext], Awaitable[None]],
    ) -> InvokeResponse | None:
        trace_parent = _request_trace_parent.get()
        if trace_parent:
            object.__setattr__(activity, _TRACE_PARENT_ATTRIBUTE, trace_parent)

        return await super().process_activity(claims_identity, activity, callback)


class AgentRequestCorrelationMiddleware:
    """Restore the request trace while the agent processes a turn."""

    async def on_turn(
        self,
        context: TurnContext,
        logic: Callable[[TurnContext], Awaitable[None]],
    ) -> None:
        trace_parent = getattr(
            context.activity,
            _TRACE_PARENT_ATTRIBUTE,
            None,
        ) or _request_trace_parent.get()
        if not trace_parent:
            await logic(context)
            return

        parent_context = propagate.extract({"traceparent": trace_parent})
        tracer = trace.get_tracer(__name__)
        with tracer.start_as_current_span(
            "A365AgentApplication",
            context=parent_context,
            record_exception=False,
            set_status_on_exception=False,
        ) as span:
            try:
                await logic(context)
            except Exception as ex:
                span.record_exception(ex)
                span.set_status(Status(StatusCode.ERROR, str(ex)))
                raise
