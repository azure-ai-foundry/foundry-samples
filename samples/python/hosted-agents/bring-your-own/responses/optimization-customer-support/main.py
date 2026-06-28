# Copyright (c) Microsoft. All rights reserved.

"""Customer Support Agent — Optimization-ready hosted agent using Responses protocol.

A realistic customer support agent for a consumer electronics company.
Uses an intentionally bare system prompt so that instruction and skill
optimization can produce visible improvement.

Normal operation (no optimization):
    Uses the minimal default prompt — answers are acceptable but lack
    structure, policy specifics, and procedural guidance.

After optimization:
    Instructions are rewritten with specific policies, tone guidelines,
    troubleshooting frameworks, and discovered skills are appended.
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

from azure.ai.agentserver.responses.streaming import ResponseEventStream
from azure.ai.agentserver.responses.models._generated.sdk.models.models._models import (
    ResponseUsage,
    ResponseUsageInputTokensDetails,
    ResponseUsageOutputTokensDetails,
)

from azure.ai.agentserver.optimization import load_config

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("customer-support-agent")

# ── Config ────────────────────────────────────────────────────────────────────
# Intentionally bare — optimization will dramatically improve this.
# Baseline instructions live in .agent_configs/baseline/instructions.md

FALLBACK_INSTRUCTIONS = "You are a helpful customer support agent."

config = load_config()
if config is None:
    from azure.ai.agentserver.optimization import OptimizationConfig
    config = OptimizationConfig(
        instructions=FALLBACK_INSTRUCTIONS,
        model=os.getenv("MODEL_DEPLOYMENT_NAME")
        or os.getenv("AZURE_AI_MODEL_DEPLOYMENT_NAME")
        or "gpt-5.4-mini",
        source="defaults",
    )

MODEL = config.model or os.getenv("MODEL_DEPLOYMENT_NAME", "gpt-5.4-mini")
ALL_INSTRUCTIONS = config.compose_instructions()

logger.info(
    "Config loaded (source=%s, model=%s)", config.source, MODEL
)

# ── Foundry client ────────────────────────────────────────────────────────────

_endpoint = (
    os.environ.get("FOUNDRY_PROJECT_ENDPOINT")
    or os.environ.get("AZURE_AI_PROJECT_ENDPOINT")
    or ""
)
if not _endpoint:
    raise EnvironmentError(
        "FOUNDRY_PROJECT_ENDPOINT or AZURE_AI_PROJECT_ENDPOINT must be set."
    )

_credential = DefaultAzureCredential()
_project_client = AIProjectClient(endpoint=_endpoint, credential=_credential)
_responses_client = _project_client.get_openai_client().responses

# ── Agent server ──────────────────────────────────────────────────────────────

app = ResponsesAgentServerHost(
    options=ResponsesServerOptions(default_fetch_history_count=20),
)


def _build_input(current_input: str, history: list) -> list[dict]:
    """Build Responses API input from conversation history and current message."""
    input_items = []
    for item in history:
        if hasattr(item, "content") and item.content:
            for content in item.content:
                if isinstance(content, MessageContentOutputTextContent) and content.text:
                    input_items.append({"role": "assistant", "content": content.text})
                elif isinstance(content, MessageContentInputTextContent) and content.text:
                    input_items.append({"role": "user", "content": content.text})
    input_items.append({"role": "user", "content": current_input})
    return input_items


@app.response_handler
async def handler(
    request: CreateResponse,
    context: ResponseContext,
    _cancellation_signal: asyncio.Event,
):
    """Forward user input to the model with optimized instructions."""
    user_input = await context.get_input_text() or "Hello!"
    history = await context.get_history()

    logger.info("Processing request %s (model=%s, source=%s)",
                context.response_id, MODEL, config.source)

    input_items = _build_input(user_input, history)

    response = await asyncio.get_running_loop().run_in_executor(
        None,
        lambda: _responses_client.create(
            model=MODEL,
            instructions=ALL_INSTRUCTIONS,
            input=input_items,
            store=False,
        ),
    )

    # Build usage from the model response
    usage = None
    if response.usage:
        usage = ResponseUsage(
            input_tokens=response.usage.input_tokens or 0,
            output_tokens=response.usage.output_tokens or 0,
            total_tokens=response.usage.total_tokens or 0,
            input_tokens_details=ResponseUsageInputTokensDetails(cached_tokens=0),
            output_tokens_details=ResponseUsageOutputTokensDetails(reasoning_tokens=0),
        )

    stream = ResponseEventStream(response_id=context.response_id, request=request)
    async def _events():
        yield stream.emit_created()
        yield stream.emit_in_progress()
        message = stream.add_output_item_message()
        yield message.emit_added()
        text_content = message.add_text_content()
        yield text_content.emit_added()
        yield text_content.emit_delta(response.output_text or "")
        yield text_content.emit_text_done(response.output_text or "")
        yield text_content.emit_done()
        yield message.emit_done()
        yield stream.emit_completed(usage=usage)

    return _events()


app.run()
