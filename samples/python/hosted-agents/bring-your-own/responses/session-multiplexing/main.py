# Copyright (c) Microsoft. All rights reserved.

"""Session multiplexing sample for Bring Your Own Responses agents.

Conversation state is platform-managed via previous_response_id and
context.get_history(). The container keeps no in-memory conversation state.
"""

import asyncio
import logging
import os

from azure.ai.agentserver.core import get_request_context
from azure.ai.agentserver.responses import (
    CreateResponse,
    ResponseContext,
    ResponsesAgentServerHost,
    ResponsesServerOptions,
    TextResponse,
)
from azure.ai.agentserver.responses.models import (
    MessageContentInputTextContent,
    MessageContentOutputTextContent,
)
from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential

logger = logging.getLogger(__name__)

_endpoint = os.environ["FOUNDRY_PROJECT_ENDPOINT"]
_model = os.environ["AZURE_AI_MODEL_DEPLOYMENT_NAME"]

_responses_client = AIProjectClient(
    endpoint=_endpoint, credential=DefaultAzureCredential()
).get_openai_client().responses

app = ResponsesAgentServerHost(
    options=ResponsesServerOptions(default_fetch_history_count=20),
)

_SYSTEM_PROMPT = (
    "You are a helpful AI assistant. Be concise and informative. "
    "Use the conversation history that is provided for this request."
)

_ROLE_MAP = {
    MessageContentOutputTextContent: "assistant",
    MessageContentInputTextContent: "user",
}


def _build_input(current_input: str, history: list) -> list[dict]:
    """Convert platform history + current message into Responses API input."""
    items = []
    for item in history:
        for content in getattr(item, "content", None) or []:
            role = _ROLE_MAP.get(type(content))
            if role and content.text:
                items.append({"role": role, "content": content.text})
    items.append({"role": "user", "content": current_input})
    return items


def _require_platform_context() -> None:
    # Validate the hosted protocol 2.0.0 context is present. Conversation
    # history isolation itself is handled by the AgentServer SDK/platform when
    # context.get_history() runs; the container does not pass user_id or call_id
    # for that path.
    request_context = get_request_context()
    if not isinstance(request_context.user_id, str) or not request_context.user_id.strip():
        raise ValueError(
            "A user context is required. In hosted Foundry protocol 2.0.0, "
            "the AgentServer SDK exposes it via get_request_context().user_id."
        )
    if not isinstance(request_context.call_id, str) or not request_context.call_id.strip():
        raise ValueError(
            "A call id is required. In hosted Foundry protocol 2.0.0, "
            "the AgentServer SDK exposes it via get_request_context().call_id."
        )


@app.response_handler
async def handler(
    request: CreateResponse,
    context: ResponseContext,
    _cancellation_signal: asyncio.Event,
):
    """Forward user input to the model with platform-managed history."""
    _require_platform_context()

    user_input = await context.get_input_text() or "Hello!"

    # Platform-managed history is authorized and fetched by the Responses SDK.
    # If this container adds its own outbound Foundry 1P calls later, such as
    # Storage or Toolbox/MCP, forward only get_request_context().platform_headers()
    # on those calls so the per-request call ID is preserved.
    history = await context.get_history()
    input_items = _build_input(user_input, history)

    response = await asyncio.get_running_loop().run_in_executor(
        None,
        lambda: _responses_client.create(
            model=_model,
            instructions=_SYSTEM_PROMPT,
            input=input_items,
            store=False,
        ),
    )

    return TextResponse(context, request, text=response.output_text)


app.run()
