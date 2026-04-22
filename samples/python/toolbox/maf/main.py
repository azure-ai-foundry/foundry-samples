"""Agent Framework toolbox agent using MCPStreamableHTTPTool.

Connects to an toolbox MCP endpoint in Microsoft Foundry using the Agent Framework
SDK's MCPStreamableHTTPTool, which implements the MCP Streamable HTTP transport
protocol directly without requiring LangChain or LangGraph.

Platform-injected environment variables (set automatically at runtime):
  - FOUNDRY_PROJECT_ENDPOINT          – project endpoint
  - FOUNDRY_AGENT_TOOLBOX_ENDPOINT    – base URL for toolbox MCP proxy
  - FOUNDRY_AGENT_TOOLBOX_FEATURES    – feature-flag headers

User-defined environment variables (declared in agent.manifest.yaml):
  - MODEL_DEPLOYMENT_NAME             – model deployment name
  - TOOLBOX_ENDPOINT                  – full toolbox MCP endpoint URL

All changes require an existing Microsoft Foundry project for deployment.
See the LangGraph-based counterpart in ../langgraph/ for comparison.

Usage::

    # Set required environment variables
    export FOUNDRY_PROJECT_ENDPOINT=https://<account>.services.ai.azure.com/api/projects/<project>
    export MODEL_DEPLOYMENT_NAME=gpt-4.1
    export TOOLBOX_ENDPOINT=https://<account>.services.ai.azure.com/api/projects/<project>/toolboxes/<name>/mcp?api-version=v1

    # Start the agent
    python main.py

    # Invoke
    curl -X POST http://localhost:8088/responses \\
        -H "Content-Type: application/json" \\
        -d '{"input": "What tools do you have?"}'
"""

import asyncio
import logging
import os
import pathlib
import re

import httpx
from dotenv import load_dotenv

load_dotenv(override=False)

from azure.identity import DefaultAzureCredential, get_bearer_token_provider
from azure.ai.agentserver.responses import (
    ResponseContext,
    ResponseEventStream,
    ResponsesAgentServerHost,
    ResponsesServerOptions,
    get_input_expanded,
)
from azure.ai.agentserver.responses.models import CreateResponse
from agent_framework import MCPStreamableHTTPTool
from agent_framework_foundry import FoundryChatClient
from agent_framework.observability import enable_instrumentation

enable_instrumentation(enable_sensitive_data=True)

# ── Agent name and logger ────────────────────────────────────────────────────


def _read_agent_name() -> str:
    try:
        yaml_text = pathlib.Path("agent.yaml").read_text()
        m = re.search(r"^name:\s*(.+)$", yaml_text, re.MULTILINE)
        return m.group(1).strip() if m else "unknown-agent"
    except Exception:
        return "unknown-agent"


AGENT_NAME = _read_agent_name()
logger = logging.getLogger(AGENT_NAME)

# ── Configuration ─────────────────────────────────────────────────────────────

PROJECT_ENDPOINT = os.getenv("FOUNDRY_PROJECT_ENDPOINT", "")
if not PROJECT_ENDPOINT:
    raise ValueError("FOUNDRY_PROJECT_ENDPOINT must be set")

MODEL_DEPLOYMENT_NAME = os.getenv("MODEL_DEPLOYMENT_NAME", "")
if not MODEL_DEPLOYMENT_NAME:
    raise ValueError("MODEL_DEPLOYMENT_NAME environment variable must be set")

# TOOLBOX_ENDPOINT is the full pre-constructed MCP URL including toolbox name
# and api-version. Declared in agent.manifest.yaml.
TOOLBOX_ENDPOINT = os.getenv("TOOLBOX_ENDPOINT", "")

# Feature-flag headers for toolbox proxy requests.
_TOOLBOX_FEATURES = os.getenv("FOUNDRY_AGENT_TOOLBOX_FEATURES", "Toolboxes=V1Preview")

# ── Toolbox MCP auth ──────────────────────────────────────────────────────────

class _ToolboxAuth(httpx.Auth):
    """httpx Auth that injects a fresh bearer token on every request.

    Uses ``get_bearer_token_provider`` so the underlying credential handles
    caching and proactive token refresh automatically.
    """

    def __init__(self, token_provider):
        self._get_token = token_provider

    def auth_flow(self, request):
        request.headers["Authorization"] = f"Bearer {self._get_token()}"
        yield request


# ── Agent ─────────────────────────────────────────────────────────────────────

SYSTEM_PROMPT = """You are a helpful assistant with access to toolbox tools in Microsoft Foundry.

Use the available tools to help answer user questions accurately and concisely.

Be conversational and helpful."""


def _create_agent():
    """Create and return the MAF agent with toolbox tools."""
    credential = DefaultAzureCredential()

    chat_client = FoundryChatClient(
        project_endpoint=PROJECT_ENDPOINT,
        model=MODEL_DEPLOYMENT_NAME,
        credential=credential,
    )

    if not TOOLBOX_ENDPOINT:
        raise ValueError(
            "TOOLBOX_ENDPOINT must be set. Declare it in agent.manifest.yaml "
            "or set it directly for local dev."
        )

    logger.info("Connecting to toolbox: %s", TOOLBOX_ENDPOINT)
    token_provider = get_bearer_token_provider(credential, "https://ai.azure.com/.default")
    extra_headers = {"Foundry-Features": _TOOLBOX_FEATURES} if _TOOLBOX_FEATURES else {}
    http_client = httpx.AsyncClient(
        auth=_ToolboxAuth(token_provider),
        headers=extra_headers,
        timeout=120.0,
    )

    mcp_tool = MCPStreamableHTTPTool(
        name="toolbox",
        url=TOOLBOX_ENDPOINT,
        http_client=http_client,
        load_prompts=False,
    )
    tools = [mcp_tool]

    agent = chat_client.as_agent(
        name=AGENT_NAME,
        instructions=SYSTEM_PROMPT,
        tools=tools,
    )

    logger.info(
        "[%s] starting up (model=%s, endpoint=%s)",
        AGENT_NAME, MODEL_DEPLOYMENT_NAME, PROJECT_ENDPOINT,
    )
    return agent


def _get_input_text(request: CreateResponse) -> str | None:
    """Extract plain text from a CreateResponse input."""
    inp = request.input
    if isinstance(inp, str):
        return inp
    items = get_input_expanded(request)
    for item in items:
        content = getattr(item, "content", None)
        if content is None:
            continue
        if isinstance(content, str):
            return content
        if isinstance(content, list):
            for part in content:
                text = getattr(part, "text", None)
                if text:
                    return text
    return None


# Consent-URL error code returned by the Foundry MCP gateway.
_CONSENT_ERROR_CODE = -32006


def _is_consent_error(exc: BaseException) -> bool:
    """Return True if *exc* (or any nested sub-exception) is an MCP consent-URL error."""
    error_data = getattr(exc, "error", None)
    if error_data is not None and getattr(error_data, "code", None) == _CONSENT_ERROR_CODE:
        return True
    if "consent.azure-apim.net" in str(exc):
        return True
    if hasattr(exc, "exceptions"):
        if any(_is_consent_error(sub) for sub in exc.exceptions):
            return True
    for chained in (exc.__cause__, exc.__context__):
        if chained is not None and _is_consent_error(chained):
            return True
    return False


def _extract_consent_url(exc: BaseException) -> str:
    """Walk nested exceptions and return the consent URL string."""
    error_data = getattr(exc, "error", None)
    if error_data is not None and getattr(error_data, "code", None) == _CONSENT_ERROR_CODE:
        return getattr(error_data, "message", str(exc))
    msg = str(exc)
    if "consent.azure-apim.net" in msg:
        return msg
    if hasattr(exc, "exceptions"):
        for sub in exc.exceptions:
            url = _extract_consent_url(sub)
            if url:
                return url
    for chained in (exc.__cause__, exc.__context__):
        if chained is not None:
            url = _extract_consent_url(chained)
            if url:
                return url
    return str(exc)


# ── Server ────────────────────────────────────────────────────────────────────

responses = ResponsesAgentServerHost(
    options=ResponsesServerOptions(default_fetch_history_count=20),
)

_agent = None
_agent_lock = asyncio.Lock()


async def _get_agent():
    global _agent
    if _agent is not None:
        return _agent
    async with _agent_lock:
        if _agent is not None:
            return _agent
        _agent = _create_agent()
        return _agent


@responses.response_handler
async def handle_response(
    request: CreateResponse,
    context: ResponseContext,
    cancellation_signal: asyncio.Event,
):
    stream = ResponseEventStream(
        response_id=context.response_id,
        model=getattr(request, "model", None),
    )

    yield stream.emit_created()
    yield stream.emit_in_progress()

    user_input = _get_input_text(request) or ""
    if not user_input:
        message_item = stream.add_output_item_message()
        yield message_item.emit_added()
        for event in message_item.text_content("No input provided."):
            yield event
        yield message_item.emit_done()
        yield stream.emit_completed()
        return

    try:
        agent = await _get_agent()
        result = await asyncio.wait_for(
            agent.run(messages=user_input, stream=False),
            timeout=120.0,
        )
        # Extract text from MAF AgentResponse
        assistant_reply = str(result.message) if hasattr(result, "message") else str(result)
        if not assistant_reply:
            assistant_reply = "(Agent completed without text response)"
    except asyncio.TimeoutError:
        assistant_reply = "I could not complete this request within the local timeout. Please retry with a simpler prompt."
    except asyncio.CancelledError:
        assistant_reply = "The request was cancelled before completion. Please retry."
    except Exception as e:
        if _is_consent_error(e):
            consent_url = _extract_consent_url(e)
            logger.warning(
                "OAuth consent required. Open the following URL in a browser "
                "to authorize, then restart the agent:\n\n  %s\n",
                consent_url,
            )
            assistant_reply = (
                f"OAuth consent is required before this agent's tools can be used. "
                f"Please open the following URL in a browser to authorize access, "
                f"then try again:\n\n  {consent_url}"
            )
        else:
            logger.error("Failed to process request: %s", e, exc_info=True)
            assistant_reply = f"I encountered an error processing your request: {e}"

    message_item = stream.add_output_item_message()
    yield message_item.emit_added()

    for event in message_item.text_content(assistant_reply):
        yield event
    yield message_item.emit_done()

    yield stream.emit_completed()


responses.run()
