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

from utils.toolbox import ToolboxClient
from utils.browser import BrowserSession
from utils.skills import load_skill, list_skills
from utils.constants import SYSTEM_PROMPT, TOOLS, AZURE_AI_SCOPE
from utils.utils import redact as _redact

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
_used_sessions: set[str] = set()  # sessions touched in current request


async def _create_session(name: str) -> dict:
    """Create a browser session: Toolbox create -> playwright-cli attach."""
    global _last_session
    if name in _sessions:
        return {"status": "already_exists", "session": name, "live_view_url": _sessions[name].get("live_view_url")}

    loop = asyncio.get_running_loop()
    result = await loop.run_in_executor(
        None, _toolbox.call_tool, "create_session", {}
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

        elif name == "end_session":
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
            _used_sessions.add(sess_name)
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
                _used_sessions.add(sess)
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


async def _run_agent_loop_streaming(input_items: list[dict]):
    """Generator version of the agent loop that yields (verbose_log, final_reply) tuples.

    Yields ("log", text) for intermediate tool progress.
    Yields ("reply", text) for the final model response.
    """
    system = SYSTEM_PROMPT.format(skills=", ".join(list_skills()))

    if _sessions:
        input_items.insert(-1, {
            "role": "user",
            "content": f"[Active sessions: {list(_sessions.keys())}, default: {_last_session}]"
        })

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
            yield ("reply", response.output_text or "(No response)")
            return

        for tc in tool_calls:
            name = getattr(tc, "name", "")
            args = json.loads(tc.arguments or "{}")

            # Track sessions before tool call to detect lazy creation
            sessions_before = set(_sessions.keys())

            result_text = await _handle_tool_call(tc)

            # Detect new sessions (covers both explicit create_session and lazy creation via run_browser/run_parallel)
            new_sessions = set(_sessions.keys()) - sessions_before
            for new_sess in new_sessions:
                url = _sessions[new_sess].get("live_view_url", "")
                if url:
                    yield ("session", f"🌐 Created **{new_sess}** → [Live View]({url})\n")
                else:
                    yield ("session", f"🌐 Created **{new_sess}** (session ready)\n")

            # Yield verbose progress log
            log_entry = _format_tool_log(name, args, result_text)
            if log_entry:
                yield log_entry

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


def _format_tool_log(name: str, args: dict, result_text: str) -> tuple[str, str] | None:
    """Format a verbose log entry for a tool call. Returns (kind, text) or None."""
    if name == "end_session":
        sess_name = args.get("name", "?")
        try:
            result = json.loads(result_text)
            if result.get("status") == "killed_all":
                return ("log", f"🔴 Ended ALL sessions\n")
            elif result.get("status") == "killed":
                return ("log", f"🔴 Ended **{sess_name}**\n")
        except (json.JSONDecodeError, TypeError):
            pass
        return ("log", f"🔴 End {sess_name}\n")
    elif name == "run_browser":
        sess = args.get("session") or _last_session or "?"
        cmd = args.get("command", "")
        cmd_args = args.get("args") or []
        safe_args = [_redact(a) for a in cmd_args[:2]]
        return ("log", f"🔧 [{sess}] `{cmd} {' '.join(safe_args)}`\n")
    elif name == "run_parallel":
        tasks = args.get("tasks") or []
        return ("log", f"⚡ Running {len(tasks)} tasks in parallel\n")
    elif name == "load_skill":
        return ("log", f"📖 Loading skill: {args.get('name', '?')}\n")
    elif name == "list_sessions":
        return ("log", f"📋 Listed sessions\n")
    return None


# ── Responses protocol handler ────────────────────────────────────────────────

app = ResponsesAgentServerHost(
    options=ResponsesServerOptions(
        default_fetch_history_count=20,
        sse_keep_alive_interval_seconds=15,
    ),
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
    """Handle browser automation requests with tool-calling loop and verbose streaming."""
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

    # Check for /verbose flag in user input
    verbose_mode = user_input.strip().startswith("/verbose")
    if verbose_mode:
        user_input = user_input.replace("/verbose", "", 1).strip()

    try:
        history = await context.get_history()
    except Exception as exc:
        logger.warning("get_history failed; continuing without history: %s", exc)
        history = []

    input_items = _build_input(user_input, history)
    _used_sessions.clear()
    logger.info("Processing request %s (sessions: %s)", context.response_id, list(_sessions.keys()))

    message_item = stream.add_output_item_message()
    yield message_item.emit_added()

    text_content = message_item.add_text_content()
    yield text_content.emit_added()

    # Stream verbose tool logs as they happen
    verbose_text = ""
    final_reply = ""

    try:
        async for kind, text in _run_agent_loop_streaming(input_items):
            if cancellation_signal.is_set():
                yield text_content.emit_delta("⚠️ Cancelled.\n")
                break
            if kind == "session":
                # Always show session events (live view URL)
                yield text_content.emit_delta(text)
                verbose_text += text
            elif kind == "log":
                if verbose_mode:
                    # Show tool action logs in verbose mode
                    yield text_content.emit_delta(text)
                    verbose_text += text
                else:
                    # Heartbeat: emit empty delta to keep SSE connection alive
                    yield text_content.emit_delta("")
            elif kind == "reply":
                final_reply = text
    except asyncio.TimeoutError:
        final_reply = "Request timed out. Please retry with a simpler prompt."
    except asyncio.CancelledError:
        final_reply = "Request was cancelled."
    except Exception as exc:
        logger.exception("Agent loop failed: %s", exc)
        final_reply = f"Agent error: {exc}"

    # Emit final reply
    if verbose_text:
        yield text_content.emit_delta("\n---\n\n")

    yield text_content.emit_delta(final_reply)

    # Append active session live view links at the end for easy access
    if _sessions:
        links = []
        for name, s in _sessions.items():
            url = s.get("live_view_url", "")
            if url:
                links.append(f"- **{name}**: [Live View]({url})")
            else:
                links.append(f"- **{name}**: (no live view)")
        footer = "\n\n---\n**Active Sessions:**\n" + "\n".join(links)
        if _used_sessions:
            footer += f"\n\n_Used Sessions in this response: {', '.join(_used_sessions)}_"
        yield text_content.emit_delta(footer + "\n")

    yield text_content.emit_text_done()
    yield text_content.emit_done()
    yield message_item.emit_done()

    yield stream.emit_completed()


app.run()
