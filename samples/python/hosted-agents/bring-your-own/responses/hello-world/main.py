# Copyright (c) Microsoft. All rights reserved.

"""Hello World — Bring Your Own Responses agent.

Minimal hosted agent that forwards user input to a Foundry model via the
Responses API and returns the reply through the Responses protocol.

This sample demonstrates the simplest possible BYO integration: the protocol
SDK (``azure-ai-agentserver-responses``) handles the HTTP contract and SSE
lifecycle, and you supply the model call using the Foundry SDK.

Conversation history is automatically managed by the platform via
``previous_response_id``. The handler calls ``context.get_history()`` to
retrieve prior turns and includes them in the model call so the agent
maintains context across messages.

Required environment variables:
    FOUNDRY_PROJECT_ENDPOINT: Foundry project endpoint (auto-injected in hosted containers)
    AZURE_AI_MODEL_DEPLOYMENT_NAME: Model deployment name (declared in agent.manifest.yaml)

Usage::

    # Set environment variables
    export FOUNDRY_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
    export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4.1-mini"

    # Start the agent
    python main.py

    # Invoke the agent
    curl -sS -X POST http://localhost:8088/responses \\
        -H "Content-Type: application/json" \\
        -d '{"input": "What is Microsoft Foundry?", "stream": false}' | jq .
"""

import asyncio
import logging
import os

from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential

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

logger = logging.getLogger(__name__)

if not os.environ.get("APPLICATIONINSIGHTS_CONNECTION_STRING"):
    logger.warning(
        "APPLICATIONINSIGHTS_CONNECTION_STRING not set — traces will not be sent to "
        "Application Insights. Set it to enable local telemetry. "
        "(This variable is auto-injected in hosted Foundry containers — do not declare it in agent.manifest.yaml.)"
    )

# Initialize Foundry project client — reads FOUNDRY_PROJECT_ENDPOINT.
# FOUNDRY_PROJECT_ENDPOINT is auto-injected in hosted Foundry containers.
# Locally, set it manually or use 'azd ai agent run' which sets it automatically.
_endpoint = os.environ.get("FOUNDRY_PROJECT_ENDPOINT")
if not _endpoint:
    raise EnvironmentError(
        "FOUNDRY_PROJECT_ENDPOINT environment variable is not set. "
        "Set it to your Foundry project endpoint, or use 'azd ai agent run' "
        "which sets it automatically."
    )

_model = os.environ.get("AZURE_AI_MODEL_DEPLOYMENT_NAME")
if not _model:
    raise EnvironmentError(
        "AZURE_AI_MODEL_DEPLOYMENT_NAME environment variable is not set. "
        "Set it to your model deployment name as declared in agent.manifest.yaml."
    )

_credential = DefaultAzureCredential()
_project_client = AIProjectClient(endpoint=_endpoint, credential=_credential)

# Use the Responses client — not chat.completions
_responses_client = _project_client.get_openai_client().responses

_SYSTEM_PROMPT = "You are a helpful AI assistant. Be concise and informative."

app = ResponsesAgentServerHost(
    options=ResponsesServerOptions(default_fetch_history_count=20),
)


def _build_input(current_input: str, history: list) -> list[dict]:
    """Build Responses API input from conversation history and current message.

    The platform stores conversation history as typed items. We convert them
    into simple {"role": ..., "content": ...} dicts that the Responses API accepts.
    """
    input_items = []
    for item in history:
        if hasattr(item, "content") and item.content:
            for content in item.content:
                if isinstance(content, MessageContentOutputTextContent) and content.text:
                    input_items.append(
                        {"role": "assistant", "content": content.text})
                elif isinstance(content, MessageContentInputTextContent) and content.text:
                    input_items.append(
                        {"role": "user", "content": content.text})
    input_items.append({"role": "user", "content": current_input})
    return input_items


@app.response_handler
async def handler(
    request: CreateResponse,
    context: ResponseContext,
    _cancellation_signal: asyncio.Event,
):
    """Forward user input to the model with conversation history."""
    user_input = await context.get_input_text() or "Hello!"
    history = await context.get_history()

    logger.info("Processing request %s", context.response_id)

    input_items = _build_input(user_input, history)

    # Run the synchronous OpenAI SDK call in a thread pool to avoid blocking
    # the async event loop.
    response = await asyncio.get_running_loop().run_in_executor(
        None,
        lambda: _responses_client.create(
            model=_model,
            instructions=_SYSTEM_PROMPT,
            input=input_items,
            store=False,  # The platform manages conversation history — no need to store at the model level
        ),
    )

    return TextResponse(context, request, text=response.output_text)


app.run()
