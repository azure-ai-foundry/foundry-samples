# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

"""Preserve HTTP request correlation through the Agents SDK turn pipeline."""

from __future__ import annotations

from collections.abc import Awaitable, Callable
from contextvars import ContextVar
import json
import os

from aiohttp.web import Request, Response
from microsoft_agents.activity import Activity, InvokeResponse
from microsoft_agents.hosting.aiohttp import CloudAdapter
from microsoft_agents.hosting.core import Agent, ClaimsIdentity, TurnContext
from opentelemetry import propagate, trace
from opentelemetry.trace import Span, Status, StatusCode


_TRACE_PARENT_ATTRIBUTE = "_foundry_request_trace_parent"
_request_trace_parent: ContextVar[str | None] = ContextVar(
    "foundry_request_trace_parent",
    default=None,
)
_agent_span: ContextVar[Span | None] = ContextVar("foundry_agent_span", default=None)


def set_current_span_response(
    response_id: str | None,
    output_text: str,
) -> None:
    """Record the Responses API result on the active invoke_agent span."""
    span = _agent_span.get()
    if span is None:
        return

    if response_id:
        span.set_attribute("gen_ai.response.id", response_id)

    if not output_text:
        return

    part = {"type": "text"}
    if os.getenv(
        "OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT", "false"
    ).lower() == "true":
        part["content"] = output_text

    span.set_attribute(
        "gen_ai.output.messages",
        json.dumps([{"role": "assistant", "parts": [part]}]),
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
        parent_context = (
            propagate.extract({"traceparent": trace_parent}) if trace_parent else None
        )
        agent_name = os.getenv("FOUNDRY_AGENT_NAME") or "FoundryDigitalWorker"
        agent_version = os.getenv("FOUNDRY_AGENT_VERSION") or "unknown"
        agent_id = f"{agent_name}:{agent_version}"
        tracer = trace.get_tracer(__name__)
        with tracer.start_as_current_span(
            f"invoke_agent {agent_name}",
            context=parent_context,
            record_exception=False,
            set_status_on_exception=False,
        ) as span:
            span_token = _agent_span.set(span)
            try:
                input_text = getattr(context.activity, "text", None)
                span.set_attribute("gen_ai.operation.name", "invoke_agent")
                span.set_attribute("gen_ai.agent.id", agent_id)
                span.set_attribute("microsoft.gen_ai.main_agent.id", agent_id)
                span.set_attribute("gen_ai.agent.name", agent_name)
                parts = []
                if input_text:
                    part = {"type": "text"}
                    if os.getenv(
                        "OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT", "false"
                    ).lower() == "true":
                        part["content"] = input_text
                    parts.append(part)
                span.set_attribute(
                    "gen_ai.input.messages",
                    json.dumps([{"role": "user", "parts": parts}]),
                )
                try:
                    await logic(context)
                except Exception as ex:
                    span.record_exception(ex)
                    span.set_status(Status(StatusCode.ERROR, str(ex)))
                    raise
            finally:
                _agent_span.reset(span_token)
