"""LangGraph ReAct Agent with Azure AI Foundry Toolbox MCP Support.

This agent connects to an Azure AI Foundry toolbox via MCP and uses it to
respond to user queries.

## Platform-Injected Environment Variables (container-image-spec)

The Foundry platform injects these at runtime:
- `FOUNDRY_PROJECT_ENDPOINT` — project endpoint
- `FOUNDRY_AGENT_TOOLBOX_ENDPOINT` — base URL for toolbox MCP proxy
- `FOUNDRY_AGENT_TOOLBOX_FEATURES` — feature-flag headers for toolbox requests

## User-Defined Variables

- `AZURE_AI_MODEL_DEPLOYMENT_NAME` — chat model deployment name
- `TOOLBOX_ENDPOINT` — full toolbox MCP endpoint URL

## Starting with an Existing Project Endpoint

For local development, set the FOUNDRY_* variables in `.env`:
```bash
export FOUNDRY_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4.1"
export TOOLBOX_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>/toolboxes/<name>/mcp?api-version=v1"
```
"""

import asyncio
import logging
import os
import pathlib
import re
from urllib.parse import urlparse

import httpx
from dotenv import load_dotenv

load_dotenv(override=False)

from langchain_openai import ChatOpenAI
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

# ── LLM (Chat Completions API via Azure OpenAI endpoint) ────────────────────

PROJECT_ENDPOINT = os.getenv("FOUNDRY_PROJECT_ENDPOINT", "")
if not PROJECT_ENDPOINT:
    raise ValueError("FOUNDRY_PROJECT_ENDPOINT must be set")

MODEL_DEPLOYMENT_NAME = os.getenv("AZURE_AI_MODEL_DEPLOYMENT_NAME", "")
if not MODEL_DEPLOYMENT_NAME:
    raise ValueError("AZURE_AI_MODEL_DEPLOYMENT_NAME environment variable must be set")

_credential = DefaultAzureCredential()
token_provider = get_bearer_token_provider(
    _credential,
    "https://ai.azure.com/.default",
)


class _AzureTokenAuth(httpx.Auth):
    """httpx Auth that injects a fresh bearer token on every request."""

    def auth_flow(self, request):
        request.headers["Authorization"] = f"Bearer {token_provider()}"
        yield request


_llm_http_client = httpx.Client(auth=_AzureTokenAuth())
_llm_async_http_client = httpx.AsyncClient(auth=_AzureTokenAuth())

llm = ChatOpenAI(
    base_url=f"{PROJECT_ENDPOINT.rstrip('/')}/openai/v1",
    api_key="placeholder",  # overridden by _AzureTokenAuth
    model=MODEL_DEPLOYMENT_NAME,
    http_client=_llm_http_client,
    http_async_client=_llm_async_http_client,
)

# ── Toolbox MCP helpers ────────────────────────────────────────────────────

# TOOLBOX_NAME is declared in agent.manifest.yaml and resolved from the toolbox
# resource at deploy time. The full MCP URL is constructed from the project endpoint.
# Falls back to TOOLBOX_ENDPOINT for local testing / explicit override.
_TOOLBOX_NAME = os.getenv("TOOLBOX_NAME", "")
TOOLBOX_ENDPOINT = (
    f"{PROJECT_ENDPOINT.rstrip('/')}/toolboxes/{_TOOLBOX_NAME}/mcp?api-version=v1"
    if _TOOLBOX_NAME
    else os.getenv("TOOLBOX_ENDPOINT", "")
)

# Feature-flag header value (e.g. "Toolboxes=V1Preview").
_TOOLBOX_FEATURES = os.getenv("FOUNDRY_AGENT_TOOLBOX_FEATURES", "Toolboxes=V1Preview")

SYSTEM_PROMPT = """You are a helpful assistant with access to Azure AI Foundry toolbox tools.

When tool output includes Azure AI Search retrieval metadata, use citation-style
grounding based on result.structuredContent.documents[].

For each citation, prefer:
- title (citation label)
- url (source link)
- score (relevance)

If citations are present, include a brief Sources section in your answer.
Do not invent citation links. If no document metadata is present, answer without
fabricated citations.
"""

# ── Agent creation ──────────────────────────────────────────────────────────


def create_agent(model, tools):
    return create_react_agent(model, tools, prompt=SYSTEM_PROMPT)


async def quickstart():
    """Build and return a LangGraph agent wired to an MCP client.

    Connects to the Azure AI Foundry toolbox MCP endpoint specified in
    TOOLBOX_MCP_ENDPOINT.

    When the toolbox requires OAuth consent (e.g. GitHub OAuth connections),
    the MCP server responds with error code -32006 and the consent URL as the
    message.  This function detects that scenario, logs the URL, and re-raises
    so operators can complete the OAuth flow before retrying.
    """
    if not TOOLBOX_ENDPOINT:
        raise ValueError(
            "TOOLBOX_ENDPOINT must be set. Declare it in agent.manifest.yaml "
            "or set it directly for local dev."
        )

    # Connect to the Azure AI Foundry toolbox MCP endpoint.
    logger.info(f"Connecting to toolbox: {TOOLBOX_ENDPOINT}")
    toolbox_auth = _AzureTokenAuth()
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
        # OAuth consent required — the MCP server returns error code -32006
        # with the consent URL as the message.  The MCP client wraps this in
        # one or more ExceptionGroup layers, so we recurse to find it.
        if _is_consent_error(exc):
            consent_url = _extract_consent_url(exc)
            logger.warning(
                "OAuth consent required. Open the following URL in a browser "
                "to authorize, then restart the agent:\n\n  %s\n",
                consent_url,
            )
            # Instead of crashing the container, return an agent with a
            # fallback tool that surfaces the consent URL to the caller.

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

    # Enable error handling so that tool-call failures are returned as tool
    # messages instead of raising ToolException (which breaks the agent's
    # conversation state when tool_calls lack matching tool_messages).
    for t in tools:
        t.handle_tool_error = True

    # Sanitize tool schemas — some MCP servers (e.g. gitmcp.io) return tools
    # with missing or empty 'properties', which the framework rejects for object types.
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

    logger.info(f"Loaded {len(tools)} tools from MCP")
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


# Consent-URL error code returned by the Foundry MCP gateway.
_CONSENT_ERROR_CODE = -32006
_CONSENT_HOST = "consent.azure-apim.net"


def _contains_consent_host(text: str) -> bool:
    """Return True if *text* contains a URL whose hostname is the consent host."""
    for token in re.findall(r"https?://[^\s'\"<>]+", text):
        host = urlparse(token).hostname
        if host and (host == _CONSENT_HOST or host.endswith(f".{_CONSENT_HOST}")):
            return True
    return False


def _extract_allowed_consent_url(text: str) -> str | None:
    """Return the first URL in *text* whose hostname is exactly consent.azure-apim.net."""
    for candidate in re.findall(r"https?://[^\s)>\]\"']+", text):
        parsed = urlparse(candidate)
        if parsed.hostname == "consent.azure-apim.net":
            return candidate
    return None


def _is_consent_error(exc: BaseException) -> bool:
    """Return True if *exc* (or any nested sub-exception) is an MCP consent-URL error."""
    # mcp.McpError carries an .error.code attribute
    error_data = getattr(exc, "error", None)
    if error_data is not None and getattr(error_data, "code", None) == _CONSENT_ERROR_CODE:
        return True
    # Fallback: parse URL(s) from the exception text and validate host
    if _contains_consent_host(str(exc)):
        return True
    # Recurse into ExceptionGroup / BaseExceptionGroup sub-exceptions
    if hasattr(exc, "exceptions"):
        return any(_is_consent_error(sub) for sub in exc.exceptions)
    return False


def _extract_consent_url(exc: BaseException) -> str:
    """Walk nested exceptions and return the consent URL string."""
    error_data = getattr(exc, "error", None)
    if error_data is not None and getattr(error_data, "code", None) == _CONSENT_ERROR_CODE:
        message = getattr(error_data, "message", str(exc))
        return _extract_allowed_consent_url(message) or message
    msg = str(exc)
    matched_url = _extract_allowed_consent_url(msg)
    if matched_url:
        return matched_url
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
