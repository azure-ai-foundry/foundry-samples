"""LangGraph ReAct Agent with Azure AI Foundry Toolbox MCP Support.

This agent connects to an Azure AI Foundry toolbox via MCP and uses it to
respond to user queries. Configure the project and toolbox endpoints with
environment variables before starting the agent.

## Starting with an Existing Project Endpoint

To deploy using an existing Azure AI Foundry project:

1. **Set environment variables** (locally or in deployment):
   ```bash
   export AZURE_AI_PROJECT_ENDPOINT="https://<region>.services.ai.azure.com/api/projects/<project-id>"
   export TOOLBOX_MCP_ENDPOINT="https://<region>.services.ai.azure.com/api/projects/<project-id>/toolboxes/<toolbox-name>/mcp?api-version=v1"
   export OPENAI_API_VERSION="2025-03-01-preview"
   export MODEL_DEPLOYMENT_NAME="gpt-4o"  # or your deployed model name
   ```

2. **Run the agent**:
   ```bash
   python main.py
   ```

The agent uses the endpoints you provide. Hosted deployments should supply
these environment variables through the deployment environment.
"""

import asyncio
import os
import subprocess
import sys
from urllib.parse import urlparse as _urlparse

from setup import logger

from langchain_openai import AzureChatOpenAI
from langchain_core.tools import tool
from azure.ai.agentserver.responses import ResponseContext, ResponseEventStream, ResponsesServerOptions
from azure.ai.agentserver.responses import get_input_text
from azure.ai.agentserver.responses import CreateResponse
from azure.ai.agentserver.responses.hosting import ResponsesAgentServerHost as ResponseHandler
from azure.identity import DefaultAzureCredential, get_bearer_token_provider
from langchain_mcp_adapters.client import MultiServerMCPClient

# ── LLM (Chat Completions API via Azure OpenAI endpoint) ────────────────────

PROJECT_ENDPOINT = os.getenv("AZURE_AI_PROJECT_ENDPOINT")
if not PROJECT_ENDPOINT:
    raise ValueError("AZURE_AI_PROJECT_ENDPOINT environment variable must be set")

MODEL_DEPLOYMENT_NAME = os.getenv("MODEL_DEPLOYMENT_NAME", "gpt-4o")

_parsed = _urlparse(PROJECT_ENDPOINT)
azure_openai_endpoint = os.environ.get(
    "AZURE_OPENAI_ENDPOINT",
    f"{_parsed.scheme}://{_parsed.netloc}",
)

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

# ── Toolbox MCP helpers ────────────────────────────────────────────────────

TOOLBOX_ENDPOINT = os.getenv("TOOLBOX_MCP_ENDPOINT")

def _get_toolbox_token() -> str:
    """Get bearer token for the toolbox MCP endpoint."""
    try:
        credential = DefaultAzureCredential()
        token = credential.get_token("https://ai.azure.com/.default")
        return token.token
    except Exception:
        # Fall back to az CLI
        az_cmd = "az.cmd" if sys.platform == "win32" else "az"
        result = subprocess.run(
            [az_cmd, "account", "get-access-token", "--resource", "https://ai.azure.com", "--query", "accessToken", "-o", "tsv"],
            capture_output=True, text=True, timeout=30,
        )
        if result.returncode != 0:
            raise RuntimeError(f"Failed to get token: {result.stderr}")
        return result.stdout.strip()

def _get_toolbox_headers(token: str) -> dict:
    """Get required headers for toolbox MCP calls."""
    return {
        "Authorization": f"Bearer {token}",
        "Foundry-Features": "Toolboxes=V1Preview",
    }


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
    from langgraph.prebuilt import create_react_agent
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
            "TOOLBOX_MCP_ENDPOINT environment variable must be set. "
            "This agent requires a toolbox MCP endpoint."
        )
    
    # Connect to the Azure AI Foundry toolbox MCP endpoint.
    logger.info(f"Connecting to toolbox: {TOOLBOX_ENDPOINT}")
    token = _get_toolbox_token()
    headers = _get_toolbox_headers(token)

    client = MultiServerMCPClient(
        {
            "toolbox": {
                "url": TOOLBOX_ENDPOINT,
                "transport": "streamable_http",
                "headers": headers,
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
            return create_agent(llm, [oauth_consent_required])
        raise

    # Enable error handling so that tool-call failures are returned as tool
    # messages instead of raising ToolException (which breaks the agent's
    # conversation state when tool_calls lack matching tool_messages).
    for t in tools:
        t.handle_tool_error = True

    # Sanitize tool schemas — some MCP servers (e.g. gitmcp.io) return tools
    # with missing or empty 'properties', which OpenAI rejects for object types.
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
    return create_agent(llm, tools)


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
_CONSENT_HOST = "consent.azure-apim.net"

def _find_consent_url(text: str) -> str:
    """Return the first URL in *text* whose hostname matches the consent host."""
    if not isinstance(text, str):
        return ""
    for token in text.split():
        candidate = token.strip("()[]{}<>,;\"'")
        parsed = _urlparse(candidate)
        if parsed.scheme in ("http", "https") and parsed.hostname == _CONSENT_HOST:
            return candidate
    return ""



def _find_consent_url_in_text(text: str) -> str:
    """Return the first URL token in *text* whose parsed hostname matches the consent host."""
    for token in text.split():
        parsed = _urlparse(token.strip("()[]{}<>,;\"'"))
        if parsed.hostname == _CONSENT_HOST:
    # Fallback: parse URLs from exception text and validate hostname
    if _find_consent_url(str(exc)):


def _is_consent_error(exc: BaseException) -> bool:
    """Return True if *exc* (or any nested sub-exception) is an MCP consent-URL error."""
    # mcp.McpError carries an .error.code attribute
    error_data = getattr(exc, "error", None)
    if error_data is not None and getattr(error_data, "code", None) == _CONSENT_ERROR_CODE:
        return True
    # Fallback: parse URL-like tokens and validate hostname exactly.
    if _find_consent_url_in_text(str(exc)):
        return True
    # Recurse into ExceptionGroup / BaseExceptionGroup sub-exceptions
    if hasattr(exc, "exceptions"):
    consent_url = _find_consent_url(msg)
    if consent_url:
        return consent_url


def _extract_consent_url(exc: BaseException) -> str:
    """Walk nested exceptions and return the consent URL string."""
    error_data = getattr(exc, "error", None)
    if error_data is not None and getattr(error_data, "code", None) == _CONSENT_ERROR_CODE:
        return getattr(error_data, "message", str(exc))
    msg = str(exc)
    consent_url = _find_consent_url_in_text(msg)
    if consent_url:
        return consent_url
    if hasattr(exc, "exceptions"):
        for sub in exc.exceptions:
            url = _extract_consent_url(sub)
            if url:
                return url
    return str(exc)


responses = ResponseHandler(
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

        _agent = await quickstart()
        return _agent


@responses.create_handler
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

    user_input = get_input_text(request) or "Hello!"

    try:
        agent = await _get_agent()
        result = await asyncio.wait_for(
            agent.ainvoke({"messages": [("user", user_input)]}),
            timeout=40.0,
        )
        assistant_reply = _extract_assistant_text(result)
        if not assistant_reply:
            assistant_reply = "(Agent completed without text response)"
    except asyncio.TimeoutError:
        assistant_reply = "I could not complete this request within the local timeout. Please retry with a simpler prompt."
    except asyncio.CancelledError:
        assistant_reply = "The request was cancelled before completion. Please retry."
    except Exception as e:
        logger.error(f"Failed to process request: {e}", exc_info=True)
        assistant_reply = f"I encountered an error: {str(e)}"

    message_item = stream.add_output_item_message()
    yield message_item.emit_added()

    text_content = message_item.add_text_content()
    yield text_content.emit_added()
    yield text_content.emit_delta(assistant_reply)
    yield text_content.emit_done()
    yield message_item.emit_content_done(text_content)
    yield message_item.emit_done()

    yield stream.emit_completed()


if __name__ == "__main__":
    responses.run()
