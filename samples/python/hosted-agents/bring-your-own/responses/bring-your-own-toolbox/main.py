# Copyright (c) Microsoft. All rights reserved.

"""Toolbox — Bring Your Own Responses agent with Foundry Toolbox MCP.

Hosted agent that connects to an Azure AI Foundry toolbox via MCP,
discovers tools at startup, and lets the model call them during
conversation. Uses the Responses protocol for request/response handling.

The agent:
1. Connects to the toolbox MCP endpoint and discovers available tools
2. On each request, sends the conversation + tool definitions to the model
3. If the model requests a tool call, executes it via MCP and loops
4. Returns the final text response through the Responses protocol SSE stream

Conversation history is automatically managed by the platform via
``previous_response_id``. The handler calls ``context.get_history()`` to
retrieve prior turns and includes them in the model call so the agent
maintains context across messages.

Required environment variables:
    FOUNDRY_PROJECT_ENDPOINT: Foundry project endpoint (auto-injected in hosted containers)
    AZURE_AI_MODEL_DEPLOYMENT_NAME: Model deployment name (declared in agent.manifest.yaml)
    TOOLBOX_NAME: Toolbox resource name (declared in agent.manifest.yaml); the MCP URL
        is constructed from this and FOUNDRY_PROJECT_ENDPOINT automatically.

Usage::

    # Set environment variables
    export FOUNDRY_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
    export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4.1"
    export TOOLBOX_NAME="<toolbox-resource-name>"

    # Start the agent
    python main.py

    # Invoke the agent
    curl -sS -X POST http://localhost:8088/responses \\
        -H "Content-Type: application/json" \\
        -d '{"input": "Search the web for Azure AI Foundry news", "stream": false}' | jq .
"""

from azure.ai.agentserver.responses.models import (
    MessageContentInputTextContent,
    MessageContentOutputTextContent,
)
from azure.ai.agentserver.responses import (
    CreateResponse,
    ResponseContext,
    ResponseEventStream,
    ResponsesAgentServerHost,
    ResponsesServerOptions,
    get_input_expanded,
)
from azure.identity import DefaultAzureCredential, get_bearer_token_provider
from azure.ai.projects import AIProjectClient
import asyncio
import json
import logging
import os

import httpx
from dotenv import load_dotenv

load_dotenv(override=False)


logger = logging.getLogger(__name__)

if not os.environ.get("APPLICATIONINSIGHTS_CONNECTION_STRING"):
    logger.warning(
        "APPLICATIONINSIGHTS_CONNECTION_STRING not set — traces will not be sent to "
        "Application Insights. Set it to enable local telemetry. "
        "(This variable is auto-injected in hosted Foundry containers — do not declare it in agent.manifest.yaml.)"
    )

# ── Configuration ─────────────────────────────────────────────────────────────

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

# TOOLBOX_NAME is declared in agent.manifest.yaml and resolved from the toolbox
# resource at deploy time. The full MCP URL is constructed from the project endpoint.
# Falls back to TOOLBOX_ENDPOINT for local testing / explicit override.
_TOOLBOX_NAME = os.getenv("TOOLBOX_NAME", "")
TOOLBOX_ENDPOINT = (
    f"{_endpoint.rstrip('/')}/toolboxes/{_TOOLBOX_NAME}/mcp?api-version=v1"
    if _TOOLBOX_NAME
    else os.getenv("TOOLBOX_ENDPOINT", "")
)
if not TOOLBOX_ENDPOINT:
    logger.warning(
        "TOOLBOX_NAME is not set — agent will start without toolbox tools. "
        "Set TOOLBOX_NAME or declare a toolbox resource in agent.manifest.yaml."
    )
# Ensure api-version query param is present when using a manually set TOOLBOX_ENDPOINT.
elif "api-version=" not in TOOLBOX_ENDPOINT:
    sep = "&" if "?" in TOOLBOX_ENDPOINT else "?"
    TOOLBOX_ENDPOINT += f"{sep}api-version=v1"

# Feature-flag header value (e.g. "Toolboxes=V1Preview").
_TOOLBOX_FEATURES = os.getenv("FOUNDRY_AGENT_TOOLBOX_FEATURES", "Toolboxes=V1Preview")

_credential = DefaultAzureCredential()
_project_client = AIProjectClient(endpoint=_endpoint, credential=_credential)
_responses_client = _project_client.get_openai_client().responses
_token_provider = get_bearer_token_provider(
    _credential, "https://ai.azure.com/.default")

_SYSTEM_PROMPT = (
    "You are a helpful AI assistant with access to tools via Azure AI Foundry toolbox. "
    "Use the available tools when appropriate to answer user questions. "
    "Be concise and informative."
)

# ── Toolbox MCP client ────────────────────────────────────────────────────────


class _McpToolboxClient:
    """Lightweight MCP client for toolbox tool discovery and invocation."""

    def __init__(self, endpoint: str, token_provider):
        self.endpoint = endpoint
        self._get_token = token_provider
        self._session_id: str | None = None
        self._req_id = 0

    def _headers(self) -> dict:
        h = {
            "Content-Type": "application/json",
            "Authorization": f"Bearer {self._get_token()}",
        }
        if _TOOLBOX_FEATURES:
            h["Foundry-Features"] = _TOOLBOX_FEATURES
        if self._session_id:
            h["mcp-session-id"] = self._session_id
        return h

    def _next_id(self) -> int:
        self._req_id += 1
        return self._req_id

    def initialize(self) -> str:
        """Send MCP initialize + initialized notification."""
        with httpx.Client(timeout=60) as client:
            resp = client.post(
                self.endpoint,
                headers=self._headers(),
                json={
                    "jsonrpc": "2.0",
                    "id": self._next_id(),
                    "method": "initialize",
                    "params": {
                        "protocolVersion": "2024-11-05",
                        "capabilities": {},
                        "clientInfo": {"name": "byo-responses-toolbox", "version": "1.0.0"},
                    },
                },
            )
            resp.raise_for_status()
            self._session_id = resp.headers.get("mcp-session-id")
            data = resp.json()

            # Send initialized notification
            client.post(
                self.endpoint,
                headers=self._headers(),
                json={"jsonrpc": "2.0", "method": "notifications/initialized"},
            )
            return data.get("result", {}).get("serverInfo", {}).get("name", "unknown")

    def list_tools(self) -> list[dict]:
        """Call tools/list and return tool definitions."""
        with httpx.Client(timeout=60) as client:
            resp = client.post(
                self.endpoint,
                headers=self._headers(),
                json={"jsonrpc": "2.0", "id": self._next_id(
                ), "method": "tools/list", "params": {}},
            )
            resp.raise_for_status()
            return resp.json().get("result", {}).get("tools", [])

    def call_tool(self, name: str, arguments: dict) -> str:
        """Call a tool and return the text result."""
        with httpx.Client(timeout=120) as client:
            resp = client.post(
                self.endpoint,
                headers=self._headers(),
                json={
                    "jsonrpc": "2.0",
                    "id": self._next_id(),
                    "method": "tools/call",
                    "params": {"name": name, "arguments": arguments},
                },
            )
            resp.raise_for_status()
            result = resp.json().get("result", {})
            content = result.get("content", [])
            texts = []
            for c in content:
                if isinstance(c, dict):
                    if c.get("type") == "text" and c.get("text"):
                        texts.append(c["text"])
                    elif c.get("type") == "resource":
                        resource = c.get("resource", {})
                        if resource.get("text"):
                            texts.append(resource["text"])
            return "\n".join(texts) if texts else json.dumps(result)


# ── Lazy tool discovery ───────────────────────────────────────────────────────
# Defer MCP connection to first request so the container can start and pass
# health checks before the toolbox endpoint is reachable.

_mcp_client: _McpToolboxClient | None = None
_tool_definitions: list[dict] = []
_tools_initialized = False


def _ensure_tools():
    global _mcp_client, _tool_definitions, _tools_initialized
    if _tools_initialized:
        return
    if not TOOLBOX_ENDPOINT:
        logger.warning("TOOLBOX_ENDPOINT not set — skipping toolbox tool discovery")
        _tools_initialized = True
        return
    logger.info("Connecting to toolbox: %s", TOOLBOX_ENDPOINT)
    _mcp_client = _McpToolboxClient(TOOLBOX_ENDPOINT, _token_provider)
    server_name = _mcp_client.initialize()
    mcp_tools = _mcp_client.list_tools()
    logger.info("Toolbox '%s' connected: %d tool(s) discovered",
                server_name, len(mcp_tools))
    for t in mcp_tools:
        _tool_definitions.append({
            "type": "function",
            "name": t["name"],
            "description": t.get("description", ""),
            "parameters": t.get("inputSchema", {"type": "object", "properties": {}}),
        })
    _tools_initialized = True

# ── Agentic loop ──────────────────────────────────────────────────────────────


_MAX_TOOL_ROUNDS = 10


def _call_model(input_items: list[dict]) -> object:
    """Call the model with tool definitions and return the response."""
    _ensure_tools()
    return _responses_client.create(
        model=_model,
        instructions=_SYSTEM_PROMPT,
        input=input_items,
        tools=_tool_definitions if _tool_definitions else None,
        store=False,
    )


def _run_agent_loop(input_items: list[dict]) -> str:
    """Execute the agentic tool-calling loop synchronously.

    Calls the model, checks for tool calls, executes them, feeds results
    back, and repeats until the model produces a text response or we hit
    the max rounds limit.
    """
    for _ in range(_MAX_TOOL_ROUNDS):
        response = _call_model(input_items)

        # Check if the model wants to call tools
        tool_calls = [
            item for item in response.output
            if getattr(item, "type", None) == "function_call"
        ]

        if not tool_calls:
            return response.output_text or "(No response)"

        # Execute each tool call and build result items
        for tc in tool_calls:
            try:
                arguments = json.loads(tc.arguments) if isinstance(
                    tc.arguments, str) else tc.arguments
                result_text = _mcp_client.call_tool(tc.name, arguments)
                logger.info("Tool '%s' returned %d chars",
                            tc.name, len(result_text))
            except Exception as e:
                logger.error("Tool '%s' failed: %s", tc.name, e)
                result_text = f"Error calling tool: {e}"

            input_items.append({
                "type": "function_call",
                "id": tc.id,
                "call_id": tc.call_id,
                "name": tc.name,
                "arguments": tc.arguments if isinstance(tc.arguments, str) else json.dumps(tc.arguments),
            })
            input_items.append({
                "type": "function_call_output",
                "call_id": tc.call_id,
                "output": result_text,
            })

    return "(Reached maximum tool call rounds)"


# ── Responses protocol handler ────────────────────────────────────────────────

app = ResponsesAgentServerHost(
    options=ResponsesServerOptions(default_fetch_history_count=20),
)


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


def _build_input(current_input: str, history: list) -> list[dict]:
    """Build Responses API input from conversation history and current message."""
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
    cancellation_signal: asyncio.Event,
):
    """Forward user input to the model with toolbox tools and conversation history."""
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

    history = await context.get_history()
    input_items = _build_input(user_input, history)

    logger.info("Processing request %s", context.response_id)

    loop = asyncio.get_running_loop()
    assistant_reply = await loop.run_in_executor(None, _run_agent_loop, input_items)

    message_item = stream.add_output_item_message()
    yield message_item.emit_added()

    text_content = message_item.add_text_content()
    yield text_content.emit_added()
    yield text_content.emit_delta(assistant_reply)
    yield text_content.emit_text_done()
    yield text_content.emit_done()
    yield message_item.emit_done()

    yield stream.emit_completed()


app.run()
