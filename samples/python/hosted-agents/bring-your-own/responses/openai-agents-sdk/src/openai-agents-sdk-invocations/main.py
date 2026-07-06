# Copyright (c) Microsoft. All rights reserved.

"""Getting-started: OpenAI Agents SDK with Foundry model auth and responses protocol.

Uses the openai-agents SDK (Agent + Runner) backed by an Azure OpenAI client obtained
through Foundry's Azure credential flow — no OPENAI_API_KEY required.
"""

import asyncio
import os

from agents import Agent, Runner, set_default_openai_client, set_tracing_disabled
from agents.stream_events import RawResponsesStreamEvent
from azure.identity.aio import DefaultAzureCredential, get_bearer_token_provider
from openai import AsyncOpenAI

from azure.ai.agentserver.responses import (
    CreateResponse,
    ResponseContext,
    ResponsesAgentServerHost,
    TextResponse,
)
from azure.ai.agentserver.responses.models import (
    MessageContentInputTextContent,
    MessageContentOutputTextContent,
)

# Build an AsyncOpenAI client using Entra ID
_token_provider = get_bearer_token_provider(
    DefaultAzureCredential(), "https://ai.azure.com/.default"
)
_async_oai_client = AsyncOpenAI(
    base_url=f"{os.environ.get('FOUNDRY_PROJECT_ENDPOINT', '').rstrip('/')}/openai/v1",
    api_key=_token_provider,
)

# Point the openai-agents SDK at our Foundry-authenticated Azure OpenAI client.
# Disable SDK tracing — it uploads to platform.openai.com which requires an OpenAI API key.
set_default_openai_client(_async_oai_client)
set_tracing_disabled(True)

_SYSTEM_PROMPT = "You are a helpful AI assistant. Be concise and informative."

# Create the agent once at startup. The SDK manages the agent loop, tool calls, etc.
_agent = Agent(
    name="foundry-assistant",
    instructions=_SYSTEM_PROMPT,
    model=os.environ.get("AZURE_AI_MODEL_DEPLOYMENT_NAME"),
)

app = ResponsesAgentServerHost()


def _build_input_items(current_input: str, history: list) -> list[dict[str, str]]:
    """Build openai-agents input from Responses protocol history items."""
    items: list[dict[str, str]] = []
    for item in history:
        content = getattr(item, "content", None)
        if not content:
            continue
        for part in content:
            if isinstance(part, MessageContentOutputTextContent) and part.text:
                items.append({"role": "assistant", "content": part.text})
            elif isinstance(part, MessageContentInputTextContent) and part.text:
                items.append({"role": "user", "content": part.text})

    items.append({"role": "user", "content": current_input})
    return items


@app.response_handler
async def handle_response(
    request: CreateResponse,
    context: ResponseContext,
    _cancellation_signal: asyncio.Event,
):
    user_message = (await context.get_input_text() or "").strip()
    if not user_message:
        user_message = "What can you help me with?"

    history = await context.get_history()
    input_items = _build_input_items(user_message, history)

    async def stream_text():
        try:
            async for event in Runner.run_streamed(_agent, input=input_items).stream_events():
                if not isinstance(event, RawResponsesStreamEvent):
                    continue

                raw_event = event.data
                if getattr(raw_event, "type", None) != "response.output_text.delta":
                    continue

                delta = getattr(raw_event, "delta", None)
                if isinstance(delta, str) and delta:
                    yield delta
        except Exception as ex:
            yield f"Error during openai-agents streaming: {ex}"

    return TextResponse(context, request, text=stream_text())


if __name__ == "__main__":
    app.run()
