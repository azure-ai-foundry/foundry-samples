"""CopilotToolboxAgent — Copilot SDK agent with toolbox MCP in Foundry tools.

Bridges the Copilot SDK with toolbox in Foundry by:
1. Connecting to the toolbox MCP endpoint via HTTP (JSON-RPC)
2. Discovering available tools via tools/list
3. Creating Copilot SDK Tool wrappers that forward calls to the MCP endpoint
4. Passing the tools to create_session for the agent to use
"""

import asyncio
import json
import logging
import os
import subprocess
import sys
from types import SimpleNamespace
from typing import Any

import httpx
from copilot import CopilotClient, SubprocessConfig
from copilot.tools import Tool, ToolInvocation, ToolResult
from copilot.generated.session_events import SessionEvent, SessionEventType

logger = logging.getLogger("copilot_toolbox_agent")


def _approve_all(request, context):
    """Auto-approve all permission requests (no interactive user in container)."""
    return {"kind": "approved"}


def _get_toolbox_token() -> str:
    """Get bearer token for the toolbox MCP endpoint (scope: https://ai.azure.com/.default)."""
    try:
        from azure.identity import DefaultAzureCredential
        credential = DefaultAzureCredential()
        token = credential.get_token("https://ai.azure.com/.default")
        return token.token
    except Exception:
        az_cmd = "az.cmd" if sys.platform == "win32" else "az"
        result = subprocess.run(
            [az_cmd, "account", "get-access-token", "--resource", "https://ai.azure.com",
             "--query", "accessToken", "-o", "tsv"],
            capture_output=True, text=True, timeout=30,
        )
        if result.returncode != 0:
            raise RuntimeError(f"Failed to get token: {result.stderr}")
        return result.stdout.strip()


def _get_toolbox_headers(token: str) -> dict:
    """Get required headers for toolbox MCP calls."""
    return {
        "Content-Type": "application/json",
        "Authorization": f"Bearer {token}",
        "Foundry-Features": "Toolboxes=V1Preview",
    }


# ── MCP Bridge ──────────────────────────────────────────────────────────────


class McpBridge:
    """HTTP-based MCP client that connects to a toolbox MCP endpoint in Foundry."""

    def __init__(self, endpoint: str, token: str):
        self.endpoint = endpoint
        self.headers = _get_toolbox_headers(token)
        self._session_id: str | None = None
        self._client = httpx.AsyncClient(timeout=60.0)
        self._req_id = 0

    def _next_id(self) -> int:
        self._req_id += 1
        return self._req_id

    async def initialize(self) -> str:
        """Send MCP initialize + notifications/initialized."""
        resp = await self._client.post(
            self.endpoint, headers=self.headers,
            json={
                "jsonrpc": "2.0", "id": self._next_id(), "method": "initialize",
                "params": {
                    "protocolVersion": "2025-03-26",
                    "capabilities": {},
                    "clientInfo": {"name": "copilot-toolbox-bridge", "version": "1.0.0"},
                },
            },
        )
        resp.raise_for_status()
        data = resp.json()
        self._session_id = resp.headers.get("mcp-session-id")

        headers = dict(self.headers)
        if self._session_id:
            headers["mcp-session-id"] = self._session_id
        await self._client.post(
            self.endpoint, headers=headers,
            json={"jsonrpc": "2.0", "method": "notifications/initialized"},
        )
        return data.get("result", {}).get("serverInfo", {}).get("name", "unknown")

    async def list_tools(self) -> list[dict]:
        """Call tools/list and return the tools array."""
        headers = dict(self.headers)
        if self._session_id:
            headers["mcp-session-id"] = self._session_id
        resp = await self._client.post(
            self.endpoint, headers=headers,
            json={"jsonrpc": "2.0", "id": self._next_id(), "method": "tools/list", "params": {}},
        )
        resp.raise_for_status()
        return resp.json().get("result", {}).get("tools", [])

    async def call_tool(self, name: str, arguments: dict) -> str:
        """Call tools/call and return the text result."""
        headers = dict(self.headers)
        if self._session_id:
            headers["mcp-session-id"] = self._session_id
        resp = await self._client.post(
            self.endpoint, headers=headers,
            json={
                "jsonrpc": "2.0", "id": self._next_id(), "method": "tools/call",
                "params": {"name": name, "arguments": arguments},
            },
        )
        resp.raise_for_status()
        result = resp.json().get("result", {})
        return _format_tool_result_with_citations(result)

    async def close(self):
        await self._client.aclose()


def _sanitize_tool_name(name: str) -> str:
    """Copilot SDK rejects tool names with dots — replace with underscores."""
    return name.replace(".", "_").replace("-", "_")


def _extract_ai_search_citations(result: dict[str, Any]) -> list[dict[str, Any]]:
    """Extract citation metadata from Azure AI Search tool outputs.

    Citation pattern is represented as:
    result.structuredContent.documents[]
    """
    structured = result.get("structuredContent")
    if not isinstance(structured, dict):
        return []

    docs = structured.get("documents")
    if not isinstance(docs, list):
        return []

    citations: list[dict[str, Any]] = []
    for doc in docs:
        if not isinstance(doc, dict):
            continue
        title = doc.get("title") or doc.get("id") or "source"
        url = doc.get("url")
        score = doc.get("score")
        citations.append(
            {
                "title": title,
                "url": url,
                "score": score,
            }
        )
    return citations


def _format_tool_result_with_citations(result: dict[str, Any]) -> str:
    """Return tool text output, appending normalized citation metadata when present."""
    content = result.get("content", [])
    texts = [c.get("text", "") for c in content if isinstance(c, dict) and c.get("type") == "text"]
    base_text = "\n".join(t for t in texts if t).strip()

    citations = _extract_ai_search_citations(result)
    if not citations:
        if base_text:
            return base_text
        return json.dumps(result)

    lines = ["", "Sources:"]
    for idx, c in enumerate(citations, start=1):
        title = c.get("title") or "source"
        url = c.get("url") or ""
        score = c.get("score")
        if score is not None:
            lines.append(f"{idx}. {title} (score: {score})")
        else:
            lines.append(f"{idx}. {title}")
        if url:
            lines.append(f"   {url}")

    citation_block = "\n".join(lines)
    if base_text:
        return f"{base_text}\n{citation_block}"
    return citation_block.lstrip()


def _make_copilot_tools(bridge: McpBridge, mcp_tools: list[dict]) -> list[Tool]:
    """Convert MCP tool definitions into Copilot SDK Tool objects.

    Tool names are sanitized (dots/hyphens → underscores) because the Copilot
    API rejects names with those characters.  The original MCP name is kept
    for the ``tools/call`` RPC.
    """
    tools = []
    for mcp_tool in mcp_tools:
        mcp_name = mcp_tool["name"]                           # original MCP name
        sdk_name = _sanitize_tool_name(mcp_name)              # safe for Copilot SDK
        desc = mcp_tool.get("description", f"MCP tool: {mcp_name}")
        schema = mcp_tool.get("inputSchema", {"type": "object", "properties": {}})

        def _make_handler(original_name):
            async def handler(invocation: ToolInvocation) -> ToolResult:
                args = invocation.arguments if isinstance(invocation.arguments, dict) else {}
                try:
                    result_text = await bridge.call_tool(original_name, args)
                    return ToolResult(text_result_for_llm=result_text)
                except Exception as e:
                    logger.warning("Tool %s failed: %s", original_name, e)
                    return ToolResult(text_result_for_llm="", result_type="error", error=str(e))
            return handler

        tools.append(Tool(
            name=sdk_name,
            description=desc,
            parameters=schema,
            handler=_make_handler(mcp_name),
            skip_permission=True,
        ))
    return tools


def _make_stream_event_handler(queue: "asyncio.Queue[SimpleNamespace | Exception | None]"):
    """Build an event handler that maps Copilot SDK events to queued text chunks.

    Surfaces tool execution, reasoning, and skill events as inline annotations.
    """
    active_tools: dict[str, str] = {}

    def _tool_name(event_data) -> str:
        return (
            getattr(event_data, "tool_name", None)
            or getattr(event_data, "mcp_tool_name", None)
            or "tool"
        )

    def handler(event: SessionEvent) -> None:
        etype = event.type

        if etype == SessionEventType.ASSISTANT_MESSAGE_DELTA:
            if event.data.delta_content:
                queue.put_nowait(SimpleNamespace(text=event.data.delta_content))

        elif etype == SessionEventType.TOOL_EXECUTION_START:
            name = _tool_name(event.data)
            call_id = getattr(event.data, "tool_call_id", None)
            if call_id:
                active_tools[call_id] = name
            queue.put_nowait(SimpleNamespace(
                text=f"\n> Calling `{name}` ...\n",
                annotation=True,
            ))
        elif etype == SessionEventType.TOOL_EXECUTION_PROGRESS:
            msg = getattr(event.data, "progress_message", None)
            if msg:
                queue.put_nowait(SimpleNamespace(text=f"> {msg}\n", annotation=True))
        elif etype == SessionEventType.TOOL_EXECUTION_COMPLETE:
            call_id = getattr(event.data, "tool_call_id", None)
            name = active_tools.pop(call_id, None) if call_id else None
            if not name:
                name = _tool_name(event.data)
            queue.put_nowait(SimpleNamespace(
                text=f"> `{name}` done\n",
                annotation=True,
            ))
        elif etype == SessionEventType.SKILL_INVOKED:
            name = getattr(event.data, "tool_name", None) or "skill"
            queue.put_nowait(SimpleNamespace(
                text=f"\n> Skill: `{name}`\n",
                annotation=True,
            ))

        elif etype == SessionEventType.ASSISTANT_REASONING_DELTA:
            if getattr(event.data, "delta_content", None):
                queue.put_nowait(SimpleNamespace(
                    text=event.data.delta_content,
                    annotation=True,
                ))

        elif etype == SessionEventType.ASSISTANT_TURN_START:
            queue.put_nowait(SimpleNamespace(
                text="\n> Processing...\n",
                annotation=True,
            ))

        elif etype == SessionEventType.SESSION_IDLE:
            queue.put_nowait(None)
        elif etype == SessionEventType.SESSION_ERROR:
            queue.put_nowait(RuntimeError(getattr(event.data, "message", None) or "Session error"))

    return handler


class CopilotToolboxAgent:
    """Wraps CopilotClient with toolbox MCP integration.

    Connects to the toolbox MCP endpoint in Foundry via HTTP, discovers tools,
    and registers them as Copilot SDK custom tools.
    """

    def __init__(
        self,
        *,
        skill_directories: list[str] | None = None,
        toolbox_endpoint: str | None = None,
    ):
        self._skill_directories = skill_directories or []
        self._toolbox_endpoint = toolbox_endpoint
        self._client: CopilotClient | None = None
        self._bridge: McpBridge | None = None
        self._toolbox_tools: list[Tool] = []
        self._sessions: dict[str, object] = {}

    async def start(self) -> None:
        if self._client is not None:
            return
        github_token = os.environ.get("GITHUB_TOKEN")
        config = SubprocessConfig(github_token=github_token) if github_token else None
        self._client = CopilotClient(config, auto_start=False)
        await self._client.start()

        # Connect to toolbox MCP and discover tools
        if not self._toolbox_endpoint:
            raise ValueError(
                "Toolbox endpoint is required. Set FOUNDRY_AGENT_TOOLBOX_ENDPOINT "
                "(platform-injected) or TOOLBOX_MCP_ENDPOINT (local dev)."
            )
        token = _get_toolbox_token()
        self._bridge = McpBridge(self._toolbox_endpoint, token)
        server_name = await self._bridge.initialize()
        mcp_tools = await self._bridge.list_tools()
        self._toolbox_tools = _make_copilot_tools(self._bridge, mcp_tools)
        logger.info(
            "Toolbox '%s' connected: %d tools discovered",
            server_name, len(self._toolbox_tools),
        )

    async def stop(self) -> None:
        if self._client is not None:
            for conv_id, session in list(self._sessions.items()):
                try:
                    await session.disconnect()
                except Exception:
                    logger.debug("Failed to disconnect session for %s", conv_id, exc_info=True)
            self._sessions.clear()
            await self._client.stop()
            self._client = None
        if self._bridge is not None:
            await self._bridge.close()
            self._bridge = None

    def has_session(self, conversation_id: str) -> bool:
        return conversation_id in self._sessions

    def _build_session_kwargs(self, streaming: bool) -> dict:
        kwargs: dict = {
            "streaming": streaming,
            "on_permission_request": _approve_all,
        }
        model = os.environ.get("GITHUB_COPILOT_MODEL")
        if model:
            kwargs["model"] = model
        if self._skill_directories:
            kwargs["skill_directories"] = self._skill_directories

        # Register toolbox MCP tools as custom tools
        if self._toolbox_tools:
            kwargs["tools"] = self._toolbox_tools

        return kwargs

    async def _get_or_create_session(
        self,
        conversation_id: str,
        streaming: bool,
        history: str | None = None,
    ):
        """Session retrieval: hot cache or cold create."""
        assert self._client is not None, "Call start() first"

        # Hot: return cached session
        if conversation_id in self._sessions:
            logger.debug("Hot session for %s", conversation_id)
            return self._sessions[conversation_id]

        # Cold: create new session, optionally bootstrap with history
        kwargs = self._build_session_kwargs(streaming)
        session = await self._client.create_session(**kwargs)
        if history:
            logger.info("Bootstrapping session with conversation history for %s", conversation_id)
            try:
                preamble = (
                    "Here is the prior conversation history for context. "
                    "Do not repeat or summarize it — just use it as context "
                    "for the user's next message.\n\n" + history
                )
                await session.send_and_wait(preamble, timeout=120.0)
            except Exception:
                logger.warning("Failed to bootstrap history", exc_info=True)

        self._sessions[conversation_id] = session
        logger.info("Created new session for %s", conversation_id)
        return session

    def _evict_session(self, conversation_id: str) -> None:
        self._sessions.pop(conversation_id, None)

    async def _run_once(
        self,
        prompt: str,
        *,
        conversation_id: str | None = None,
        history: str | None = None,
    ) -> SimpleNamespace:
        assert self._client is not None, "Call start() first"

        if not conversation_id:
            kwargs = self._build_session_kwargs(streaming=False)
            session = await self._client.create_session(**kwargs)
            try:
                event = await session.send_and_wait(prompt, timeout=120.0)
                text = event.data.content if event else ""
                return SimpleNamespace(text=text or "")
            finally:
                await session.disconnect()

        session = await self._get_or_create_session(conversation_id, streaming=False, history=history)
        try:
            event = await session.send_and_wait(prompt, timeout=120.0)
            text = event.data.content if event else ""
            return SimpleNamespace(text=text or "")
        except Exception:
            logger.exception("Session error for %s, evicting", conversation_id)
            self._evict_session(conversation_id)
            raise

    async def _stream(
        self,
        prompt: str,
        *,
        conversation_id: str | None = None,
        history: str | None = None,
    ):
        assert self._client is not None, "Call start() first"

        if not conversation_id:
            kwargs = self._build_session_kwargs(streaming=True)
            session = await self._client.create_session(**kwargs)
            queue: asyncio.Queue[SimpleNamespace | Exception | None] = asyncio.Queue()
            unsubscribe = session.on(_make_stream_event_handler(queue))
            try:
                await session.send(prompt)
                while True:
                    item = await queue.get()
                    if item is None:
                        break
                    if isinstance(item, Exception):
                        raise item
                    yield item
            finally:
                unsubscribe()
                await session.disconnect()
            return

        session = await self._get_or_create_session(conversation_id, streaming=True, history=history)
        queue: asyncio.Queue[SimpleNamespace | Exception | None] = asyncio.Queue()
        unsubscribe = session.on(_make_stream_event_handler(queue))
        try:
            await session.send(prompt)
            while True:
                item = await queue.get()
                if item is None:
                    break
                if isinstance(item, Exception):
                    raise item
                yield item
        except Exception:
            logger.exception("Streaming error for %s, evicting", conversation_id)
            self._evict_session(conversation_id)
            raise
        finally:
            unsubscribe()

    def run(
        self,
        prompt: str,
        *,
        stream: bool = False,
        conversation_id: str | None = None,
        history: str | None = None,
    ):
        """Return a coroutine (stream=False) or async generator (stream=True)."""
        if stream:
            return self._stream(prompt, conversation_id=conversation_id, history=history)
        return self._run_once(prompt, conversation_id=conversation_id, history=history)
