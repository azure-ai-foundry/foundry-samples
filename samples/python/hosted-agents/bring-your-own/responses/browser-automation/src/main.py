# Copyright (c) Microsoft. All rights reserved.

"""Browser Automation — Bring Your Own Responses agent with Playwright via Toolbox.

Hosted agent that automates browser interactions using playwright-cli via
Foundry Toolbox MCP. Supports multiple concurrent browser sessions with
lazy initialization — sessions are only created when the model first needs
to interact with a browser.

Required environment variables:
    FOUNDRY_PROJECT_ENDPOINT: Foundry project endpoint (auto-injected in hosted containers)
    AZURE_AI_MODEL_DEPLOYMENT_NAME: Model deployment name (declared in agent.manifest.yaml)
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

from dotenv import load_dotenv

from toolbox import ToolboxClient
from browser import BrowserSession
from skills import load_skill, list_skills
from constants import SYSTEM_PROMPT, TOOLS, AZURE_AI_SCOPE

load_dotenv(override=False)

logger = logging.getLogger(__name__)
logging.basicConfig(level=logging.INFO)

# ── Configuration ─────────────────────────────────────────────────────────────

_endpoint = os.environ["FOUNDRY_PROJECT_ENDPOINT"]
_model = os.environ["AZURE_AI_MODEL_DEPLOYMENT_NAME"]

_TOOLBOX_NAME = "browser-automation-tools"
TOOLBOX_ENDPOINT = f"{_endpoint.rstrip('/')}/toolboxes/{_TOOLBOX_NAME}/mcp?api-version=v1"

_credential = DefaultAzureCredential()
_project_client = AIProjectClient(endpoint=_endpoint, credential=_credential)
_responses_client = _project_client.get_openai_client().responses
_token_provider = get_bearer_token_provider(_credential, AZURE_AI_SCOPE)
_toolbox = ToolboxClient(TOOLBOX_ENDPOINT, _token_provider)

# ── Multi-session state ───────────────────────────────────────────────────────

_sessions: dict[str, dict] = {}  # name -> {"browser": BrowserSession, "live_view_url": str}
_last_session: str | None = None


async def _create_session(name: str) -> dict:
    """Create a browser session: Toolbox create -> playwright-cli attach."""
    global _last_session
    if name in _sessions:
        return {"status": "already_exists", "session": name, "live_view_url": _sessions[name].get("live_view_url")}

    loop = asyncio.get_running_loop()
    result = await loop.run_in_executor(
        None, _toolbox.call_tool, "browser_automation_preview___create_session", {}
    )
    cdp_url = result.get("cdp_url") or ""
    live_view_url = result.get("live_view_url") or ""

    if not cdp_url:
        logger.error("No CDP URL from Toolbox: %s", result)
        return {"error": "No CDP URL returned from Toolbox"}

    browser = BrowserSession(session_id=name)
    connect_result = await browser.connect(cdp_url)
    if not connect_result.get("success"):
        err = connect_result.get("stderr") or connect_result.get("error") or "unknown"
        return {"error": f"Browser connect failed: {err}"}

    _sessions[name] = {"browser": browser, "live_view_url": live_view_url}
    _last_session = name
    logger.info("Session '%s' created (live_view: %s)", name, bool(live_view_url))
    return {"status": "created", "session": name, "live_view_url": live_view_url}


async def _kill_session(name: str) -> dict:
    """Kill a browser session or all sessions."""
    global _last_session
    if name == "all":
        killed = list(_sessions.keys())
        for sn in killed:
            try:
                await _sessions[sn]["browser"].close()
            except Exception:
                pass
        _sessions.clear()
        _last_session = None
        return {"status": "killed_all", "sessions": killed}
    if name not in _sessions:
        return {"error": f"Session '{name}' not found. Available: {list(_sessions.keys())}"}
    try:
        await _sessions[name]["browser"].close()
    except Exception:
        pass
    del _sessions[name]
    if _last_session == name:
        _last_session = next(iter(_sessions), None)
    return {"status": "killed", "session": name, "remaining": list(_sessions.keys())}


# ── Agentic loop ──────────────────────────────────────────────────────────────


async def _handle_tool_call(call) -> str:
    """Execute a single tool call and return JSON result string."""
    global _last_session
    name = getattr(call, "name", "")
    args = json.loads(call.arguments or "{}")

    try:
        if name == "load_skill":
            result = load_skill(args.get("name", ""))

        elif name == "create_session":
            result = await _create_session(args.get("name", f"session-{len(_sessions)+1}"))

        elif name == "kill_session":
            result = await _kill_session(args.get("name", ""))

        elif name == "run_browser":
            sess_name = args.get("session") or _last_session
            # Lazy session creation — create default on first use
            if not _sessions:
                create_result = await _create_session("default")
                if create_result.get("error"):
                    return json.dumps(create_result)
                sess_name = "default"
            elif not sess_name or sess_name not in _sessions:
                return json.dumps({"error": f"Session '{sess_name}' not found. Available: {list(_sessions.keys())}"})

            browser = _sessions[sess_name]["browser"]
            command = args.get("command", "")
            cmd_args = args.get("args") or []
            _last_session = sess_name
            result = await browser.run(command, cmd_args)

        elif name == "run_parallel":
            tasks = args.get("tasks", [])
            if not tasks:
                return json.dumps({"error": "No tasks provided"})

            # Ensure default session exists for tasks without explicit session
            if not _sessions:
                create_result = await _create_session("default")
                if create_result.get("error"):
                    return json.dumps(create_result)

            async def _run_one(task: dict) -> dict:
                sess = task.get("session") or _last_session or "default"
                if sess not in _sessions:
                    return {"session": sess, "error": f"Session '{sess}' not found"}
                browser = _sessions[sess]["browser"]
                r = await browser.run(task.get("command", ""), task.get("args") or [])
                return {"session": sess, "command": task.get("command", ""), **r}

            results = await asyncio.gather(*[_run_one(t) for t in tasks], return_exceptions=True)
            output = []
            for r in results:
                if isinstance(r, Exception):
                    output.append({"error": str(r)})
                else:
                    output.append(r)
            result = {"parallel_results": output}

        elif name == "list_sessions":
            info = {}
            for sname, s in _sessions.items():
                info[sname] = {"live_view_url": s.get("live_view_url"), "connected": s["browser"]._connected}
            result = {"sessions": info, "default": _last_session}

        else:
            result = {"error": f"Unknown tool: {name}"}

    except Exception as e:
        logger.error("Tool '%s' failed: %s", name, e)
        result = {"error": str(e)}

    return json.dumps(result)


async def _run_agent_loop(input_items: list[dict]) -> str:
    """Execute the agentic tool-calling loop until the model produces a final response."""
    system = SYSTEM_PROMPT.format(skills=", ".join(list_skills()))

    # Inject active session state so model knows what's available
    if _sessions:
        input_items.insert(-1, {
            "role": "user",
            "content": f"[Active sessions: {list(_sessions.keys())}, default: {_last_session}]"
        })

    # Loop until the model produces a final text response (no tool calls).
    # Bounded by the 300s timeout in the handler via asyncio.wait_for.
    while True:
        response = await asyncio.get_running_loop().run_in_executor(
            None,
            lambda: _responses_client.create(
                model=_model,
                instructions=system,
                input=input_items,
                tools=TOOLS,
                store=False,
            ),
        )

        tool_calls = [
            item for item in response.output
            if getattr(item, "type", None) == "function_call"
        ]

        if not tool_calls:
            return response.output_text or "(No response)"

        for tc in tool_calls:
            result_text = await _handle_tool_call(tc)
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
                    input_items.append({"role": "assistant", "content": content.text})
                elif isinstance(content, MessageContentInputTextContent) and content.text:
                    input_items.append({"role": "user", "content": content.text})
    input_items.append({"role": "user", "content": current_input})
    return input_items


@app.response_handler
async def handler(
    request: CreateResponse,
    context: ResponseContext,
    cancellation_signal: asyncio.Event,
):
    """Handle browser automation requests with tool-calling loop."""
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
        history = await context.get_history()
    except Exception as exc:
        logger.warning("get_history failed; continuing without history: %s", exc)
        history = []

    input_items = _build_input(user_input, history)
    logger.info("Processing request %s (sessions: %s)", context.response_id, list(_sessions.keys()))

    try:
        assistant_reply = await asyncio.wait_for(
            _run_agent_loop(input_items),
            timeout=300.0,
        )
    except asyncio.TimeoutError:
        assistant_reply = "Request timed out. Please retry with a simpler prompt."
    except asyncio.CancelledError:
        assistant_reply = "Request was cancelled."
    except Exception as exc:
        logger.exception("Agent loop failed: %s", exc)
        assistant_reply = f"Agent error: {exc}"

    # Prepend session info to response
    session_header = ""
    if _sessions:
        for sname, s in _sessions.items():
            url = s.get("live_view_url")
            if url:
                session_header += f"🔴 **[{sname} Live View]({url})**\n"
        if session_header:
            session_header += "\n---\n\n"

    message_item = stream.add_output_item_message()
    yield message_item.emit_added()

    text_content = message_item.add_text_content()
    yield text_content.emit_added()
    yield text_content.emit_delta(session_header + assistant_reply)
    yield text_content.emit_text_done()
    yield text_content.emit_done()
    yield message_item.emit_done()

    yield stream.emit_completed()


app.run()
