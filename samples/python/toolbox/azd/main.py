"""LangGraph ReAct Agent with Azure AI Foundry Toolbox MCP Support.

This is the default agent for the azd toolbox sample. It uses LangGraph with
langchain-mcp-adapters to connect to an Azure AI Foundry toolbox MCP endpoint.

Platform-injected environment variables (set automatically at runtime):
  - FOUNDRY_PROJECT_ENDPOINT          - project endpoint
  - FOUNDRY_AGENT_TOOLBOX_ENDPOINT    - base URL for toolbox MCP proxy
  - FOUNDRY_AGENT_TOOLBOX_FEATURES    - feature-flag headers

User-defined environment variables (declared in agent.manifest.yaml):
  - MODEL_DEPLOYMENT_NAME             - model deployment name
  - TOOLBOX_ENDPOINT                  - full toolbox MCP endpoint URL

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
from urllib.parse import urlparse as _urlparse

import httpx

os.environ["ENABLE_APPLICATION_INSIGHTS_LOGGER"] = "false"

# ── Tracing ───────────────────────────────────────────────────────────────────
import setup
setup.setup()

from langchain_azure_ai.callbacks.tracers import enable_auto_tracing
enable_auto_tracing(
    enable_content_recording=True,
    trace_all_langgraph_nodes=True,
    provider_name="azure_openai",
    auto_configure_azure_monitor=False,
)

logger = logging.getLogger(__name__)

from langchain_openai import AzureChatOpenAI
from langchain_core.tools import tool
from langgraph.prebuilt import create_react_agent
from azure.ai.agentserver.responses import (
    ResponseContext,
    ResponseEventStream,
    ResponsesAgentServerHost,
    ResponsesServerOptions,
    get_input_expanded,
)
from azure.ai.agentserver.responses.models import CreateResponse
from azure.identity import DefaultAzureCredential, get_bearer_token_provider
from langchain_mcp_adapters.client import MultiServerMCPClient

# ── Configuration ─────────────────────────────────────────────────────────────

PROJECT_ENDPOINT = os.getenv("FOUNDRY_PROJECT_ENDPOINT", "")
if not PROJECT_ENDPOINT:
    raise ValueError("FOUNDRY_PROJECT_ENDPOINT must be set")

MODEL_DEPLOYMENT_NAME = os.getenv("MODEL_DEPLOYMENT_NAME", "")
if not MODEL_DEPLOYMENT_NAME:
    raise ValueError("MODEL_DEPLOYMENT_NAME environment variable must be set")

# Derive Azure OpenAI endpoint from the project endpoint (strip /api/projects/...)
_parsed = _urlparse(PROJECT_ENDPOINT)
azure_openai_endpoint = f"{_parsed.scheme}://{_parsed.netloc}"

token_provider = get_bearer_token_provider(
    DefaultAzureCredential(),
    "https://ai.azure.com/.default",
)

llm = AzureChatOpenAI(
    model=MODEL_DEPLOYMENT_NAME,
    azure_endpoint=azure_openai_endpoint,
    azure_ad_token_provider=token_provider,
    api_version=os.environ.get("OPENAI_API_VERSION", "2025-03-01-preview"),
)

# ── Toolbox MCP endpoint resolution ──────────────────────────────────────────

# TOOLBOX_ENDPOINT is the full pre-constructed MCP URL including toolbox name
# and api-version. Declared in agent.manifest.yaml.
TOOLBOX_ENDPOINT = os.getenv("TOOLBOX_ENDPOINT", "")

# Feature-flag header value (e.g. "Toolboxes=V1Preview").
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

SYSTEM_PROMPT = """You are a helpful assistant with access to Azure AI Foundry toolbox tools.

Use the available tools to help answer user questions accurately and concisely.

When tool output includes Azure AI Search retrieval metadata, treat
result.structuredContent.documents[] as the citation source.

For citations, prefer these document fields:
- title
- url
- score

If citations are present, include a short Sources section. Do not invent
citations when metadata is not available.
"""


def create_agent(model, tools):
    return create_react_agent(model, tools, prompt=SYSTEM_PROMPT)


async def quickstart():
    """Build and return a LangGraph agent wired to an MCP client.

    Connects to the Azure AI Foundry toolbox MCP endpoint specified in
    TOOLBOX_ENDPOINT.

    When the toolbox requires OAuth consent (e.g. GitHub OAuth connections),
    the MCP server responds with error code -32006 and the consent URL as the
    message. This function detects that scenario, logs the URL, and returns
    an agent with a fallback tool that surfaces the consent URL to the caller.
    """
    if not TOOLBOX_ENDPOINT:
        raise ValueError(
            "TOOLBOX_ENDPOINT must be set. Declare it in agent.manifest.yaml "
            "or set it directly for local dev."
        )

    logger.info("Connecting to toolbox: %s", TOOLBOX_ENDPOINT)
    credential = DefaultAzureCredential()
    tp = get_bearer_token_provider(credential, "https://ai.azure.com/.default")
    toolbox_auth = _ToolboxAuth(tp)
    extra_headers = {"Foundry-Features": _TOOLBOX_FEATURES} if _TOOLBOX_FEATURES else {}

    client = MultiServerMCPClient(
        {
            "toolbox": {
                "url": TOOLBOX_ENDPOINT,
                "transport": "streamable_http",
                "headers": extra_headers,
                "auth": toolbox_auth,
            }
        }
    )

    try:
        tools = await client.get_tools()
    except BaseException as exc:
        # OAuth consent required - MCP error code -32006
        if _is_consent_error(exc):
            consent_url = _extract_consent_url(exc)
            logger.warning(
                "OAuth consent required. Open the following URL in a browser "
                "to authorize, then restart the agent:\n\n  %s\n",
                consent_url,
            )

            @tool
            def oauth_consent_required(query: str) -> str:
                """Return instructions for completing OAuth consent."""
                return (
                    f"OAuth consent is required before this agent's tools can "
                    f"be used. Please open the following URL in a browser to "
                    f"authorize access, then try again:\n\n  {consent_url}"
                )
            return create_agent(llm, [oauth_consent_required]), client
        raise

    # Enable error handling so tool-call failures don't crash the conversation.
    for t in tools:
        t.handle_tool_error = True

    # Sanitize tool schemas - some MCP servers return malformed schemas.
    for t in tools:
        schema = t.args_schema if isinstance(t.args_schema, dict) else None
        if schema is None:
            continue
        if schema.get("type") == "object" and "properties" not in schema:
            schema["properties"] = {}
        props = schema.get("properties", {})
        required = schema.get("required", [])
        if required and not props:
            for field_name in required:
                props[field_name] = {"type": "string"}
            schema["properties"] = props

    logger.info("Loaded %d tools from MCP", len(tools))
    return create_agent(llm, tools), client


def _extract_assistant_text(result: dict) -> str:
    """Best-effort extraction of assistant text from a LangGraph response."""
    messages = result.get("messages", []) if isinstance(result, dict) else []
    for msg in reversed(messages):
        msg_type = getattr(msg, "type", "")
        if msg_type != "ai":
            continue
        content = getattr(msg, "content", "")
        if isinstance(content, str):
            return content
        if isinstance(content, list):
            parts: list[str] = []
            for item in content:
                if isinstance(item, str):
                    parts.append(item)
                elif isinstance(item, dict) and isinstance(item.get("text"), str):
                    parts.append(item["text"])
            if parts:
                return "\n".join(parts)
    return ""


_CONSENT_ERROR_CODE = -32006


def _is_consent_error(exc: BaseException) -> bool:
    """Return True if *exc* (or any nested sub-exception) is an MCP consent-URL error."""
    error_data = getattr(exc, "error", None)
    if error_data is not None and getattr(error_data, "code", None) == _CONSENT_ERROR_CODE:
        return True
    if "consent.azure-apim.net" in str(exc):
        return True
    if hasattr(exc, "exceptions"):
        return any(_is_consent_error(sub) for sub in exc.exceptions)
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
    return str(exc)


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


# ── Server ────────────────────────────────────────────────────────────────────

server = ResponsesAgentServerHost(
    options=ResponsesServerOptions(default_fetch_history_count=20),
)

_agent = None
_mcp_client = None  # Keep MCP client alive to prevent session GC
_agent_lock = asyncio.Lock()


async def _get_agent():
    global _agent, _mcp_client
    if _agent is not None:
        return _agent
    async with _agent_lock:
        if _agent is not None:
            return _agent
        _agent, _mcp_client = await quickstart()
        return _agent


@server.response_handler
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
            agent.ainvoke({"messages": [("user", user_input)]}),
            timeout=240.0,
        )
        assistant_reply = _extract_assistant_text(result)
        if not assistant_reply:
            assistant_reply = "(Agent completed without text response)"
    except asyncio.TimeoutError:
        assistant_reply = "I could not complete this request within the local timeout. Please retry with a simpler prompt."
    except asyncio.CancelledError:
        assistant_reply = "The request was cancelled before completion. Please retry."
    except Exception as e:
        logger.error("Failed to process request: %s", e, exc_info=True)
        assistant_reply = f"I encountered an error processing your request: {e}"

    message_item = stream.add_output_item_message()
    yield message_item.emit_added()

    text_content = message_item.add_text_content()
    yield text_content.emit_added()
    yield text_content.emit_delta(assistant_reply)
    yield text_content.emit_text_done()
    yield text_content.emit_done()
    yield message_item.emit_done()

    yield stream.emit_completed()


if __name__ == "__main__":
    server.run()
