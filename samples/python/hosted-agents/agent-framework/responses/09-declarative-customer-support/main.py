# Copyright (c) Microsoft. All rights reserved.

import os
from typing import Literal

from agent_framework import (
    Agent,
    AgentResponse,
    AgentResponseUpdate,
    BaseAgent,
    Content,
    Message,
)
from agent_framework.foundry import FoundryChatClient
from agent_framework_foundry_hosting import ResponsesHostServer
from azure.identity import DefaultAzureCredential
from dotenv import load_dotenv
from pydantic import BaseModel, Field

# Load environment variables from .env file
load_dotenv()


# --- Structured triage response --------------------------------------------------

class TriageResponse(BaseModel):
    """Triage decision produced from the conversation so far."""

    Category: Literal["Technical", "Billing", "General"] = Field(
        description=(
            "The best category for the user's request. "
            "Use 'Technical' for hardware/software/network issues, "
            "'Billing' for invoices/subscriptions/refunds, and "
            "'General' for anything else (greetings, FAQs, small talk)."
        ),
    )
    NeedsClarification: bool = Field(
        description=(
            "True if you cannot confidently classify the request yet and "
            "need to ask the user one focused follow-up question."
        ),
    )
    ClarificationQuestion: str = Field(
        default="",
        description=(
            "A single, polite follow-up question to ask the user. "
            "Required when NeedsClarification is true; otherwise empty."
        ),
    )
    Reply: str = Field(
        default="",
        description=(
            "A natural-language reply to the user. "
            "Used when Category is 'General'; otherwise may be left empty."
        ),
    )


# --- Agent instructions ----------------------------------------------------------

TRIAGE_INSTRUCTIONS = """
You are the front-line triage agent for a customer support workflow.

You will see the full conversation so far. Decide whether to:
- Ask the user one focused follow-up question (set NeedsClarification = true), or
- Route the conversation to the right specialist by setting Category, or
- Answer directly for general/small-talk requests via Reply.

Be efficient: do not ask a clarification if a category is already clear.
""".strip()

TECH_SUPPORT_INSTRUCTIONS = """
You are a senior technical support specialist. The conversation history shows
what the user has told you so far and which steps were already attempted.

Provide one concrete next troubleshooting step at a time, then wait for the
user's response. Be concise and friendly. If the issue appears resolved,
congratulate the user and ask if there's anything else.
""".strip()

BILLING_INSTRUCTIONS = """
You are a customer billing specialist. The conversation history shows what
the user has asked.

Help the user with invoice, subscription, refund, and payment-method
questions. If you need account details (e.g., last 4 of card, account email),
ask for them one at a time. Keep responses short and polite.
""".strip()


# --- Routing agent ---------------------------------------------------------------

_TECH_HANDOFF = "Connecting you with technical support..."
_BILLING_HANDOFF = "Connecting you with billing support..."


class CustomerSupportAgent(BaseAgent):
    """A code-based replacement for the declarative triage workflow.

    On every turn the hosting infrastructure passes the full conversation
    history as ``messages``. We run the triage agent for a structured decision,
    then route to a specialist or answer directly -- the same branching the
    workflow.yaml ``ConditionGroup`` expressed, but in plain Python so no
    PowerFx/.NET runtime is required.
    """

    def __init__(self, *, triage_agent: Agent, tech_support_agent: Agent, billing_agent: Agent, **kwargs) -> None:
        super().__init__(**kwargs)
        self._triage_agent = triage_agent
        self._tech_support_agent = tech_support_agent
        self._billing_agent = billing_agent

    def run(self, messages=None, *, stream: bool = False, session=None, **kwargs):
        return self._run_stream(messages) if stream else self._run(messages)

    async def _route(self, messages) -> "tuple[str, Agent | None]":
        """Map the triage decision to (text_to_send, specialist_or_None)."""
        decision = (await self._triage_agent.run(messages)).value
        if not isinstance(decision, TriageResponse):
            return "Sorry, I couldn't process that. Could you rephrase?", None
        if decision.NeedsClarification:
            return decision.ClarificationQuestion or "Could you tell me a bit more?", None
        if decision.Category == "Technical":
            return _TECH_HANDOFF, self._tech_support_agent
        if decision.Category == "Billing":
            return _BILLING_HANDOFF, self._billing_agent
        return decision.Reply or "", None

    async def _run(self, messages) -> AgentResponse:
        text, specialist = await self._route(messages)
        out = [Message(role="assistant", contents=[Content.from_text(text=text)])]
        if specialist is not None:
            out += (await specialist.run(messages)).messages
        return AgentResponse(messages=out)

    async def _run_stream(self, messages):
        text, specialist = await self._route(messages)
        yield AgentResponseUpdate(role="assistant", contents=[Content.from_text(text=text)])
        if specialist is not None:
            async for update in specialist.run(messages, stream=True):
                yield update


# --- Host setup ------------------------------------------------------------------

def main() -> None:
    client = FoundryChatClient(
        project_endpoint=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
        model=os.environ["AZURE_AI_MODEL_DEPLOYMENT_NAME"],
        credential=DefaultAzureCredential(),
    )

    # One agent per role, all sharing the same FoundryChatClient. The triage
    # agent emits a structured TriageResponse; the specialists reply in plain
    # text. History is managed by the hosting infrastructure, so store=False.
    triage_agent = Agent(
        client=client,
        name="TriageAgent",
        instructions=TRIAGE_INSTRUCTIONS,
        default_options={"response_format": TriageResponse, "store": False},
    )
    tech_support_agent = Agent(
        client=client,
        name="TechSupportAgent",
        instructions=TECH_SUPPORT_INSTRUCTIONS,
        default_options={"store": False},
    )
    billing_agent = Agent(
        client=client,
        name="BillingAgent",
        instructions=BILLING_INSTRUCTIONS,
        default_options={"store": False},
    )

    # The routing agent re-runs triage on every turn with the full conversation
    # history the host provides, then dispatches to a specialist or replies
    # directly -- the same logic the workflow.yaml ConditionGroup expressed.
    support_agent = CustomerSupportAgent(
        triage_agent=triage_agent,
        tech_support_agent=tech_support_agent,
        billing_agent=billing_agent,
        name="customer-support-triage",
        description=(
            "A multi-turn customer-support triage agent that routes between "
            "technical and billing specialists based on the conversation history."
        ),
    )

    ResponsesHostServer(support_agent).run()


if __name__ == "__main__":
    main()
