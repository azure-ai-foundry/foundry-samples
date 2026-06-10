# Copyright (c) Microsoft. All rights reserved.

"""Handoff agent — MAF HandoffBuilder workflow as a background task.

Adapts the customer-support handoff demo (triage → refund / order) from
Microsoft Agent Framework into an AsyncTaskAgent for the duplex live agent.
"""

from __future__ import annotations

import asyncio
import logging
import random
from typing import Callable

from agent_framework import ChatMiddleware as _ChatMiddleware
from duplex_agent.base import AgentConfig, AgentSpec, AsyncTaskAgent, TaskEvent

logger = logging.getLogger(__name__)


class HandoffTaskAgent(AsyncTaskAgent):
    """Run a MAF HandoffBuilder workflow as a background task."""

    def __init__(
        self,
        task_id: str,
        output_queue: asyncio.Queue[TaskEvent],
        workflow,
    ):
        super().__init__(task_id, output_queue)
        self._workflow = workflow

    @classmethod
    def create_factory(
        cls,
        workflow,
        name: str = "handoff",
        description: str = "Customer support handoff workflow",
    ) -> Callable[[str, asyncio.Queue], "HandoffTaskAgent"]:
        """Build a factory function suitable for AgentSpec.factory."""

        def factory(task_id: str, queue: asyncio.Queue) -> "HandoffTaskAgent":
            agent = cls(task_id, queue, workflow)
            agent.name = name
            agent.description = description
            return agent

        return factory

    async def run(self, task_description: str) -> str:
        await self.milestone("Processing customer request...")

        chunks: list[str] = []
        # Initial run
        event_stream = self._workflow.run(task_description, stream=True)
        pending_responses = await self._consume_stream(event_stream, chunks)

        # Resume loop: keep going while there are pending approval requests
        while pending_responses:
            responses = pending_responses
            pending_responses = {}
            event_stream = self._workflow.run(responses=responses, stream=True)
            pending_responses = await self._consume_stream(event_stream, chunks)

        return "".join(chunks) or "(no result)"

    _event_logger = logging.getLogger("duplex_agent.workflow_events")

    async def _consume_stream(self, event_stream, chunks: list[str]) -> dict:
        """Consume workflow events, collecting text and handling request_info.

        Returns a dict of {request_id: response} for any request_info events
        that need resuming (empty dict if workflow completed normally).
        """
        pending_responses: dict = {}

        try:
            async for event in event_stream:
                # Log every workflow event for debugging
                try:
                    req_id = event.request_id if event.type == "request_info" else None
                except (RuntimeError, AttributeError):
                    req_id = None
                self._event_logger.debug(
                    "Event type=%s request_id=%s data_type=%s",
                    event.type,
                    req_id,
                    type(event.data).__name__,
                )

                if event.type == "data" and hasattr(event.data, "text") and event.data.text:
                    self._event_logger.debug("  data text: %s", event.data.text[:200])
                    chunks.append(event.data.text)
                elif event.type == "output" and hasattr(event.data, "text") and event.data.text:
                    self._event_logger.debug("  output text: %s", event.data.text[:200])
                    chunks.append(event.data.text)
                elif event.type == "handoff_sent":
                    target = getattr(event.data, "target_agent_id", "next agent")
                    await self.milestone(f"Handing off to {target}...")
                elif event.type == "request_info":
                    request_id = event.request_id
                    request_data = event.data
                    response_type = getattr(event, "response_type", None)
                    self._event_logger.debug(
                        "request_info: id=%s response_type=%s data=%s",
                        request_id, response_type, type(request_data).__name__,
                    )
                    prompt = self._format_request_prompt(request_data)
                    answer = await self.question(prompt)
                    response = self._build_response(request_data, answer, event)
                    self._event_logger.debug(
                        "Built response for %s: type=%s", request_id, type(response).__name__,
                    )
                    pending_responses[request_id] = response

                # Check for mid-flight updates from the user
                if self._updates:
                    msg = self._updates.pop(0)
                    chunks.append(f"\n[Received update: {msg}]\n")
        finally:
            # Always finalize the stream to prevent GeneratorExit / OpenTelemetry
            # context detach errors when the async generator is GC'd.
            if hasattr(event_stream, "aclose"):
                await event_stream.aclose()
            elif hasattr(event_stream, "get_final_response"):
                try:
                    await event_stream.get_final_response()
                except Exception:
                    pass

        return pending_responses

    @staticmethod
    def _format_request_prompt(request_data) -> str:
        """Format a request_info event into a human-readable question."""
        # HandoffAgentUserRequest — the agent spoke and needs the user's next reply
        agent_response = getattr(request_data, "agent_response", None)
        if agent_response:
            # Extract the last assistant message text as the prompt
            messages = getattr(agent_response, "messages", [])
            for msg in reversed(messages):
                text = getattr(msg, "text", None)
                if text and getattr(msg, "role", "") == "assistant":
                    return text
            # Fallback: concatenate all message texts
            texts = [getattr(m, "text", "") for m in messages if getattr(m, "text", "")]
            if texts:
                return " ".join(texts)
        # Function approval request fallback
        fn_call = getattr(request_data, "function_call", None)
        if fn_call:
            name = getattr(fn_call, "name", "action")
            args = getattr(fn_call, "arguments", "")
            return f"The support team needs your approval to take the next action on your order. Shall I go ahead and approve it for you? Yes or no?"
        return "The support team needs your approval to take the next action on your order. Shall I go ahead and approve it for you? Yes or no?"

    @staticmethod
    def _build_response(request_data, answer: str, event):
        """Build the response for a request_info event, matching the expected type."""
        from agent_framework import Message

        # HandoffAgentUserRequest uses create_response() -> list[Message]
        if hasattr(request_data, "create_response"):
            return request_data.create_response(answer)

        # Fallback: wrap as list[Message] (the expected response_type for handoff workflows)
        return [Message(role="user", contents=[answer])]


# ---------------------------------------------------------------------------
# Handoff workflow construction (from ag_ui_workflow_handoff sample)
# ---------------------------------------------------------------------------


def _create_tools():
    """Create agent tools for the handoff workflow."""
    from agent_framework import tool

    @tool(approval_mode="never_require")
    def submit_refund(refund_description: str, amount: str, order_id: str) -> str:
        """Capture a refund request for processing."""
        return f"refund recorded for order {order_id} (amount: {amount}) with details: {refund_description}"

    @tool(approval_mode="never_require")
    def submit_replacement(order_id: str, shipping_preference: str, replacement_note: str) -> str:
        """Capture a replacement request for processing."""
        return (
            f"replacement recorded for order {order_id} "
            f"(shipping: {shipping_preference}) with details: {replacement_note}"
        )

    @tool(approval_mode="never_require")
    def lookup_order_details(order_id: str) -> dict[str, str]:
        """Return synthetic order details for a given order ID."""
        normalized = "".join(ch for ch in order_id if ch.isdigit()) or order_id
        rng = random.Random(normalized)
        catalog = [
            "Wireless Headphones",
            "Mechanical Keyboard",
            "Gaming Mouse",
            "27-inch Monitor",
            "USB-C Dock",
            "Bluetooth Speaker",
            "Laptop Stand",
        ]
        return {
            "order_id": normalized,
            "item_name": catalog[rng.randrange(len(catalog))],
            "amount": f"${rng.randint(39, 349)}.{rng.randint(0, 99):02d}",
            "currency": "USD",
            "purchase_date": f"2025-{rng.randint(1, 12):02d}-{rng.randint(1, 28):02d}",
            "status": "delivered",
        }

    return submit_refund, submit_replacement, lookup_order_details


class _LLMLoggingMiddleware(_ChatMiddleware):
    """ChatMiddleware that logs every LLM request and response in detail."""

    def __init__(self):
        self._logger = logging.getLogger("duplex_agent.llm_calls")
        self._call_count = 0

    async def process(self, context, call_next):
        self._call_count += 1
        call_id = self._call_count

        # Log request
        messages = context.messages
        options = context.options or {}
        model = options.get("model", "unknown")
        self._logger.debug(
            "LLM Call #%d [model=%s, stream=%s, messages=%d]",
            call_id, model, context.stream, len(messages),
        )
        for i, msg in enumerate(messages):
            role = getattr(msg, "role", "?")
            text = getattr(msg, "text", None) or ""
            contents = getattr(msg, "contents", [])
            # Summarize contents types
            content_types = [type(c).__name__ for c in contents] if contents else []
            self._logger.debug(
                "  msg[%d] role=%s text=%s contents=%s",
                i, role, repr(text[:200]) if text else "(empty)", content_types,
            )

        # Execute
        await call_next()

        # Log response
        result = context.result
        if result is None:
            self._logger.debug("LLM Call #%d -> result=None", call_id)
        elif context.stream and hasattr(result, "with_result_hook"):
            # Streaming: hook into the finalized response after stream is consumed
            _logger = self._logger
            _cid = call_id

            async def _log_stream_result(response):
                _logger.debug("LLM Call #%d (stream) -> response finalized", _cid)
                resp_msgs = getattr(response, "messages", [])
                for i, msg in enumerate(resp_msgs):
                    role = getattr(msg, "role", "?")
                    text = getattr(msg, "text", None) or ""
                    contents = getattr(msg, "contents", [])
                    content_types = [type(c).__name__ for c in contents] if contents else []
                    _logger.debug(
                        "  resp[%d] role=%s text=%s contents=%s",
                        i, role, repr(text[:300]) if text else "(empty)", content_types,
                    )
                return response

            context.stream_result_hooks.append(_log_stream_result)
            self._logger.debug("LLM Call #%d -> streaming (result hook attached)", call_id)
        elif hasattr(result, "messages"):
            # ChatResponse (non-streaming)
            resp_msgs = result.messages if hasattr(result, "messages") else []
            self._logger.debug("LLM Call #%d -> response messages=%d", call_id, len(resp_msgs))
            for i, msg in enumerate(resp_msgs):
                role = getattr(msg, "role", "?")
                text = getattr(msg, "text", None) or ""
                contents = getattr(msg, "contents", [])
                content_types = [type(c).__name__ for c in contents] if contents else []
                self._logger.debug(
                    "  resp[%d] role=%s text=%s contents=%s",
                    i, role, repr(text[:300]) if text else "(empty)", content_types,
                )
        else:
            self._logger.debug("LLM Call #%d -> result type=%s", call_id, type(result).__name__)


def _create_handoff_workflow(endpoint: str, model: str):
    """Build the HandoffBuilder workflow with triage, refund, and order agents."""
    from agent_framework import Agent, Message
    from agent_framework.foundry import FoundryChatClient
    from agent_framework.orchestrations import HandoffBuilder
    from azure.identity import DefaultAzureCredential

    llm_middleware = _LLMLoggingMiddleware()

    client = FoundryChatClient(
        project_endpoint=endpoint,
        model=model,
        credential=DefaultAzureCredential(),
        middleware=[llm_middleware],
    )

    submit_refund, submit_replacement, lookup_order_details = _create_tools()

    triage = Agent(
        id="triage_agent",
        name="triage_agent",
        instructions=(
            "You are the customer support triage agent.\n"
            "Routing policy:\n"
            "1. Route refund-related requests to refund_agent.\n"
            "2. Route replacement/shipping requests to order_agent.\n"
            "3. Do not force replacement if the user asked for refund only.\n"
            "4. If the issue is fully resolved, send a concise wrap-up that ends "
            "with exactly: Case complete."
        ),
        client=client,
        require_per_service_call_history_persistence=True,
    )

    refund = Agent(
        id="refund_agent",
        name="refund_agent",
        instructions=(
            "You are the refund specialist.\n"
            "1. If order_id is missing, ask only for order_id.\n"
            "2. Once order_id is available, call lookup_order_details(order_id).\n"
            "3. Do not ask the customer how much they paid unless lookup fails.\n"
            "4. Gather a short refund reason from context if needed.\n"
            "5. Call submit_refund with order_id, amount (from lookup), and refund_description.\n"
            "6. After successful refund, if replacement is explicitly needed hand off to order_agent,\n"
            "   otherwise end with exactly: Case complete."
        ),
        client=client,
        tools=[lookup_order_details, submit_refund],
        require_per_service_call_history_persistence=True,
    )

    order = Agent(
        id="order_agent",
        name="order_agent",
        instructions=(
            "You are the order specialist.\n"
            "Only handle replacement/exchange/shipping tasks.\n"
            "1. If shipping preference is missing, assume standard.\n"
            "2. If order_id is missing, ask for order_id.\n"
            "3. Call submit_replacement(order_id, shipping_preference, replacement_note).\n"
            "4. After completion end with exactly: Case complete.\n"
            "If the user wants refund only, hand off back to triage_agent."
        ),
        client=client,
        tools=[lookup_order_details, submit_replacement],
        require_per_service_call_history_persistence=True,
    )

    def _termination_condition(conversation: list[Message]) -> bool:
        """Stop when any assistant emits the completion marker."""
        for message in reversed(conversation):
            if message.role != "assistant":
                continue
            if (message.text or "").strip().lower().endswith("case complete."):
                return True
        return False

    builder = HandoffBuilder(
        name="handoff_customer_support",
        participants=[triage, refund, order],
        termination_condition=_termination_condition,
    )

    (
        builder
        .add_handoff(
            triage, [refund],
            description="Route for refund-related requests.",
        )
        .add_handoff(
            triage, [order],
            description="Route for replacement/shipping requests.",
        )
        .add_handoff(
            refund, [order],
            description="Route after refund if replacement is also needed.",
        )
        .add_handoff(
            refund, [triage],
            description="Route back for final case closure.",
        )
        .add_handoff(
            order, [triage],
            description="Route back after replacement is complete.",
        )
        .add_handoff(
            order, [refund],
            description="Route if user pivots to refund.",
        )
    )

    return builder.with_start_agent(triage).build()


# ---------------------------------------------------------------------------
# Agent builder (follows project convention)
# ---------------------------------------------------------------------------


class HandoffAgent:
    """Builder for the MAF handoff customer support sub-agent."""

    @classmethod
    def build(cls, config: AgentConfig) -> AgentSpec | None:
        """Construct the handoff AgentSpec, or None if MAF packages are unavailable."""
        try:
            import agent_framework  # noqa: F401
            import agent_framework_orchestrations  # noqa: F401
        except ImportError:
            return None
        
        if "/api/projects/" not in (config.endpoint or ""):
            logger.warning(
                "HandoffAgent requires a Foundry project endpoint (with /api/projects/). "
                "Got '%s', skipping agent.", config.endpoint,
            )
            return None

        workflow = _create_handoff_workflow(config.endpoint, config.model)

        return AgentSpec(
            name="handoff",
            description=(
                "Customer support handoff workflow with triage, refund, and order agents. "
                "Use for customer service scenarios: refunds, replacements, order inquiries."
            ),
            factory=HandoffTaskAgent.create_factory(
                workflow=workflow,
                name="handoff",
                description="Customer support handoff workflow",
            ),
        )
