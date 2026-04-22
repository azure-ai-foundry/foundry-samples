# Copyright (c) Microsoft. All rights reserved.

"""Background (long-running) agent using azure-ai-agentserver-responses with Azure OpenAI.

Demonstrates the background execution pattern where:
  - POST /responses with ``background: true`` returns immediately.
  - GET  /responses/{id} polls for the completed result.
  - POST /responses/{id}/cancel cancels in-flight work.

The agent calls Azure OpenAI to generate a detailed research analysis,
streaming LLM tokens as they arrive via ``text.emit_delta()``.

Required environment variables:
    FOUNDRY_PROJECT_ENDPOINT: Foundry project endpoint (auto-injected by the platform)
    AZURE_AI_MODEL_DEPLOYMENT_NAME: Model deployment name (e.g., gpt-4o)

Uses DefaultAzureCredential for authentication - works with:
- Azure CLI login (az login)
- Managed Identity in Azure
- Environment variables (AZURE_CLIENT_ID, AZURE_TENANT_ID, AZURE_CLIENT_SECRET)

Usage::

    # Set environment variables
    export FOUNDRY_PROJECT_ENDPOINT="https://your-resource.openai.azure.com/"
    export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4o"

    # Start the agent
    python main.py

    # Submit a background request
    curl -X POST http://localhost:8088/responses \\
        -H "Content-Type: application/json" \\
        -d '{"model": "research", "input": "Analyze the impact of AI on healthcare", "background": true, "store": true}'

    # Poll for result (use the id from the response above)
    curl http://localhost:8088/responses/<response_id>

    # Cancel an in-flight request
    curl -X POST http://localhost:8088/responses/<response_id>/cancel
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
    TextResponse,
)

logger = logging.getLogger("background-agent")

if not os.environ.get("APPLICATIONINSIGHTS_CONNECTION_STRING"):
    logger.warning(
        "APPLICATIONINSIGHTS_CONNECTION_STRING not set — traces will not be sent to "
        "Application Insights. Set it to enable local telemetry. "
        "(This variable is auto-injected in hosted Foundry containers — do not declare it in agent.manifest.yaml.)"
    )

# ── Configuration ─────────────────────────────────────────────────────────────

FOUNDRY_PROJECT_ENDPOINT = os.environ.get("FOUNDRY_PROJECT_ENDPOINT")
if not FOUNDRY_PROJECT_ENDPOINT:
    raise EnvironmentError(
        "FOUNDRY_PROJECT_ENDPOINT environment variable is not set. "
        "Set it to your Foundry project endpoint, or use 'azd ai agent run'."
    )

AZURE_AI_MODEL_DEPLOYMENT_NAME = os.environ.get(
    "AZURE_AI_MODEL_DEPLOYMENT_NAME")
if not AZURE_AI_MODEL_DEPLOYMENT_NAME:
    raise EnvironmentError(
        "AZURE_AI_MODEL_DEPLOYMENT_NAME environment variable is not set. "
        "Set it to your model deployment name as declared in agent.manifest.yaml."
    )

_credential = DefaultAzureCredential()
_project_client = AIProjectClient(
    endpoint=FOUNDRY_PROJECT_ENDPOINT, credential=_credential)

# Use the Responses API — not chat.completions (Chat Completions API is legacy).
_openai_client = _project_client.get_openai_client()

_RESEARCH_SYSTEM_PROMPT = (
    "You are a research analyst. When given a topic, produce a thorough "
    "multi-section analysis report. Include:\n"
    "1. Executive Summary\n"
    "2. Background & Context\n"
    "3. Key Findings (at least 3)\n"
    "4. Implications & Recommendations\n"
    "5. Conclusion\n\n"
    "Be detailed and substantive. Target 500-800 words."
)

app = ResponsesAgentServerHost()


@app.response_handler
async def background_handler(
    request: CreateResponse,
    context: ResponseContext,
    cancellation_signal: asyncio.Event,
):
    """Process a request with Azure OpenAI streaming.

    Works in all modes (default, streaming, background, background+streaming)
    — the SDK handles mode negotiation automatically.
    """

    async def stream_openai():
        """Yield tokens from Azure OpenAI as an async iterable."""
        user_input = await context.get_input_text() or "General AI trends analysis"
        logger.info("Starting LLM research analysis for: %s", user_input[:100])
        try:
            loop = asyncio.get_event_loop()
            openai_stream = await loop.run_in_executor(
                None,
                lambda: _openai_client.responses.create(
                    model=AZURE_AI_MODEL_DEPLOYMENT_NAME,
                    instructions=_RESEARCH_SYSTEM_PROMPT,
                    input=f"Research topic: {user_input}",
                    stream=True,
                ),
            )

            for event in openai_stream:
                if event.type == "response.output_text.delta":
                    yield event.delta

        except Exception as exc:
            logger.error("Azure OpenAI error: %s", exc)
            yield f"Error calling Azure OpenAI: {exc}"

        logger.info("Analysis complete")

    return TextResponse(context, request, text=stream_openai())


if __name__ == "__main__":
    app.run()
