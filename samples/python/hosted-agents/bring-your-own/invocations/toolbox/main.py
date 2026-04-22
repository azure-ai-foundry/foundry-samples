# Copyright (c) Microsoft. All rights reserved.

"""Toolbox — Bring Your Own Invocations agent with Foundry Toolbox MCP.

Hosted agent that connects to an Azure AI Foundry toolbox via MCP,
discovers tools at startup, and lets the model call them during
conversation. Uses the Invocations protocol for request/response handling.

The agent:
1. Connects to the toolbox MCP endpoint and discovers available tools
2. On each request, sends the conversation + tool definitions to the model
3. If the model requests a tool call, executes it via MCP and loops
4. Returns the final text response as a streaming SSE event stream

Unlike the Responses protocol, the Invocations protocol does **not** provide
built-in server-side conversation history. This agent maintains an in-memory
session store keyed by ``agent_session_id``. In production, replace it with
durable storage (Redis, Cosmos DB, etc.) so history survives restarts.

Required environment variables:
    FOUNDRY_PROJECT_ENDPOINT: Foundry project endpoint (auto-injected in hosted containers)
    AZURE_AI_MODEL_DEPLOYMENT_NAME: Model deployment name (declared in agent.manifest.yaml)
    TOOLBOX_ENDPOINT: Full toolbox MCP endpoint URL (declared in agent.manifest.yaml)

Usage::

    # Set environment variables
    export FOUNDRY_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
    export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4.1"
    export TOOLBOX_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>/toolboxes/<name>/mcp?api-version=v1"

    # Start the agent
    python main.py

    # Turn 1 — start a new conversation
    curl -sS -N -X POST "http://localhost:8088/invocations?agent_session_id=chat-001" \\
        -H "Content-Type: application/json" \\
        -d '{"message": "Search the web for Azure AI Foundry news"}'

    # Turn 2 — continue the same conversation
    curl -sS -N -X POST "http://localhost:8088/invocations?agent_session_id=chat-001" \\
        -H "Content-Type: application/json" \\
        -d '{"message": "Tell me more about the first result"}'
"""

from azure.ai.agentserver.invocations import InvocationAgentServerHost
from azure.identity import DefaultAzureCredential, get_bearer_token_provider
from azure.ai.projects import AIProjectClient
from starlette.responses import JSONResponse, StreamingResponse
from starlette.requests import Request
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

# Platform injects TOOLBOX_{NAME}_MCP_ENDPOINT for declared toolbox resources.
# Fall back to TOOLBOX_ENDPOINT for local dev (.env).
TOOLBOX_ENDPOINT = (
    os.environ.get("TOOLBOX_WEB_SEARCH_TOOLS_MCP_ENDPOINT")
    or os.environ.get("TOOLBOX_ENDPOINT", "")
)
if not TOOLBOX_ENDPOINT:
    raise EnvironmentError(
        "TOOLBOX_ENDPOINT environment variable is not set. "
        "Set it to your toolbox MCP endpoint URL, or declare the toolbox "
        "in agent.manifest.yaml resources."
    )
# Ensure api-version query param is present.
if "api-version=" not in TOOLBOX_ENDPOINT:
    sep = "&" if "?" in TOOLBOX_ENDPOINT else "?"
    TOOLBOX_ENDPOINT += f"{sep}api-version=v1"

# Feature-flag header value (e.g. "Toolboxes=V1Preview").
_TOOLBOX_FEATURES = os.getenv(
    "FOUNDRY_AGENT_TOOLBOX_FEATURES", "Toolboxes=V1Preview")

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
                        "clientInfo": {"name": "byo-invocations-toolbox", "version": "1.0.0"},
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


app = InvocationAgentServerHost()

# In-memory session store — keyed by agent_session_id.
# WARNING: state is lost on restart. Use durable storage in production.
_sessions: dict[str, list[dict]] = {}

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
            # No tool calls — return the text response
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

            # Append the function call and its result to input for the next round
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


async def _stream_agent_reply(input_items: list[dict]):
    """Run the agent loop in a thread and yield the result as SSE events."""
    loop = asyncio.get_running_loop()
    result = await loop.run_in_executor(None, _run_agent_loop, input_items)
    yield f"data: {json.dumps({'type': 'token', 'content': result})}\n\n"


@app.invoke_handler
async def handle_invoke(request: Request):
    """Handle a streaming multi-turn chat request with toolbox tools."""
    # Accept either a JSON object ({"message": "..."}, {"input": "..."}, or
    # {"query": "..."}) or a plain-text body.
    try:
        body = await request.body()
        if not body:
            raise ValueError("empty body")
        try:
            data = json.loads(body)
        except json.JSONDecodeError:
            user_message = body.decode("utf-8", errors="replace").strip()
        else:
            if isinstance(data, dict):
                user_message = (
                    data.get("message")
                    or data.get("input")
                    or data.get("query")
                    or ""
                )
            else:
                user_message = body.decode("utf-8", errors="replace").strip()
        if not isinstance(user_message, str) or not user_message.strip():
            raise ValueError("missing message text")
    except ValueError:
        return JSONResponse(
            status_code=400,
            content={
                "error": "invalid_request",
                "message": (
                    'Request body must be a non-empty JSON object with a "message" '
                    '(or "input" / "query") string, or a plain-text body, '
                    'e.g. {"message": "What is Microsoft Foundry?"}'
                ),
            },
        )

    session_id = request.state.session_id
    invocation_id = request.state.invocation_id

    logger.info("Processing invocation %s (session %s)",
                invocation_id, session_id)

    # Retrieve or create conversation history for this session.
    history = _sessions.setdefault(session_id, [])
    history.append({"role": "user", "content": user_message})
    input_items = list(history)

    async def event_generator():
        full_reply = ""
        try:
            async for delta in _stream_agent_reply(input_items):
                # Parse the SSE data to extract the content for history
                try:
                    event_data = json.loads(delta.split(
                        "data: ", 1)[1].split("\n")[0])
                    full_reply += event_data.get("content", "")
                except (IndexError, json.JSONDecodeError):
                    pass
                yield delta
        except Exception as exc:
            msg = f"Error calling model: {exc}"
            logger.error(msg)
            full_reply = msg
            yield f"data: {json.dumps({'type': 'token', 'content': msg})}\n\n"

        yield f"data: {json.dumps({'type': 'done', 'invocation_id': invocation_id, 'session_id': session_id, 'full_text': full_reply})}\n\n"

        if full_reply:
            history.append({"role": "assistant", "content": full_reply})

    return StreamingResponse(
        event_generator(),
        media_type="text/event-stream",
        headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"},
    )


app.run()
