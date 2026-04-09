"""Agent Framework toolbox agent using MCPStreamableHTTPTool.

Connects to an Azure AI Foundry toolbox MCP endpoint using the Agent Framework
SDK's MCPStreamableHTTPTool, which implements the MCP Streamable HTTP transport
protocol directly without requiring LangChain or LangGraph.

If TOOLBOX_MCP_ENDPOINT is set, the agent connects to the specified toolbox
and exposes its tools.  If the variable is not set, the agent starts without
toolbox tools and logs a warning.

All changes require an existing Azure AI Foundry project for deployment.
See the LangGraph-based counterpart in ../langgraph/ for comparison.

Usage::

    # Set required environment variables
    export AZURE_AI_PROJECT_ENDPOINT=https://<account>.services.ai.azure.com/api/projects/<project>
    export TOOLBOX_MCP_ENDPOINT=https://<account>.services.ai.azure.com/api/projects/<project>/toolboxes/<name>/mcp?api-version=v1

    # Start the agent
    python main.py

    # Invoke
    curl -X POST http://localhost:8088/responses \\
        -H "Content-Type: application/json" \\
        -d '{"input": "What tools do you have?"}'
"""

import asyncio
import os
import subprocess
import sys
from urllib.parse import urlparse as _urlparse

from setup import AGENT_NAME, logger

from azure.identity import DefaultAzureCredential, get_bearer_token_provider
from azure.ai.agentserver.responses import ResponseContext, ResponseEventStream, ResponsesServerOptions
from azure.ai.agentserver.responses import get_input_text, CreateResponse
from azure.ai.agentserver.responses.hosting import ResponsesAgentServerHost as ResponseHandler
from agent_framework import MCPStreamableHTTPTool
from agent_framework.azure import AzureOpenAIChatClient

# Monkey-patch: The Foundry Toolbox MCP server does not support the MCP 'ping'
# method (added in MCP protocol 2025-03-26). MCPStreamableHTTPTool calls
# send_ping() in _ensure_connected(), causing an infinite reconnect loop.
# Patching _ensure_connected to be a no-op avoids the issue.
async def _noop_ensure_connected(self):
    pass
MCPStreamableHTTPTool._ensure_connected = _noop_ensure_connected

# ── Configuration ─────────────────────────────────────────────────────────────

PROJECT_ENDPOINT = os.getenv("AZURE_AI_PROJECT_ENDPOINT")
if not PROJECT_ENDPOINT:
    raise ValueError("AZURE_AI_PROJECT_ENDPOINT environment variable must be set")

MODEL_DEPLOYMENT_NAME = os.getenv("MODEL_DEPLOYMENT_NAME", "gpt-4o")

# Derive Azure OpenAI endpoint from the project endpoint (strip /api/projects/...)
_parsed = _urlparse(PROJECT_ENDPOINT)
OPENAI_ENDPOINT = os.environ.get(
    "AZURE_OPENAI_ENDPOINT",
    f"{_parsed.scheme}://{_parsed.netloc}",
)

# Toolbox MCP endpoint URL. MUST include ?api-version=v1, e.g.:
# https://<account>.services.ai.azure.com/api/projects/<project>/toolboxes/<name>/mcp?api-version=v1
TOOLBOX_ENDPOINT = os.getenv("TOOLBOX_MCP_ENDPOINT")

# ── Toolbox MCP helpers ───────────────────────────────────────────────────────

def _get_toolbox_token() -> str:
    """Get bearer token for the toolbox MCP endpoint (scope: https://ai.azure.com/.default)."""
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
    """Get required HTTP headers for toolbox MCP calls."""
    return {
        "Authorization": f"Bearer {token}",
        "Foundry-Features": "Toolboxes=V1Preview",
    }


# ── Port helper ───────────────────────────────────────────────────────────────

def _resolve_port(default: int = 8088) -> int:
    """Resolve server port from PORT env var with a safe fallback."""
    raw = os.getenv("PORT")
    if not raw:
        return default
    try:
        return int(raw)
    except ValueError:
        logger.warning("Invalid PORT=%r; falling back to %s", raw, default)
        return default


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

Be conversational and helpful."""


def _create_agent():
    """Create and return the MAF agent with toolbox tools."""
    credential = DefaultAzureCredential()

    chat_client = AzureOpenAIChatClient(
        endpoint=OPENAI_ENDPOINT,
        deployment_name=MODEL_DEPLOYMENT_NAME,
        credential=credential,
    )

    tools = []
    if TOOLBOX_ENDPOINT:
        logger.info("Connecting to toolbox: %s", TOOLBOX_ENDPOINT)
        token = _get_toolbox_token()
        headers = _get_toolbox_headers(token)

        # MCPStreamableHTTPTool requires an httpx.AsyncClient with auth headers
        import httpx
        http_client = httpx.AsyncClient(headers=headers, timeout=120.0)

        mcp_tool = MCPStreamableHTTPTool(
            name="toolbox",
            url=TOOLBOX_ENDPOINT,
            http_client=http_client,
            load_prompts=False,
        )
        tools.append(mcp_tool)
    else:
        logger.warning(
            "TOOLBOX_MCP_ENDPOINT is not set; starting without toolbox tools. "
            "Set this variable to a toolbox MCP endpoint URL (including ?api-version=v1) "
            "to enable toolbox integration."
        )

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


# ── Server ────────────────────────────────────────────────────────────────────

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
        _agent = _create_agent()
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
            agent.run(messages=user_input, stream=False),
            timeout=40.0,
        )
        # Extract text from MAF AgentResponse
        assistant_reply = str(result.message) if hasattr(result, "message") else str(result)
        if not assistant_reply:
            assistant_reply = "(Agent completed without text response)"
    except asyncio.TimeoutError:
        assistant_reply = "Request timed out. Please retry with a simpler prompt."
    except asyncio.CancelledError:
        assistant_reply = "Request was cancelled."
    except Exception as e:
        logger.error("Failed to process request: %s", e, exc_info=True)
        assistant_reply = f"I encountered an error: {e}"

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
