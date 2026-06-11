# ruff: noqa: E402
from __future__ import annotations

import logging
from collections.abc import AsyncIterable
from typing import Any

from agent_framework._agents import Agent
from agent_framework._mcp import MCPStreamableHTTPTool
from agent_framework._middleware import chat_middleware, function_middleware
from agent_framework._skills import SkillsProvider
from agent_framework._types import ChatResponse, ChatResponseUpdate, Content, ResponseStream
from agent_framework.foundry import FoundryChatClient
from azure.identity import DefaultAzureCredential

from .logging import redact_sensitive_values
from .paths import prompts_root, skill_paths
from .settings import AgentSettings, ScopedAzureCredential
from .tools import (
    make_close_browser_session,
    make_get_live_view_url,
    make_run_playwright_cli,
    make_toolbox_mcp_tool,
)

logger = logging.getLogger(__name__)

# Function name whose result should be scrubbed before the model sees it.
_LIVE_VIEW_FUNCTION_NAME = "get_live_view_url"

# Module-level flag to track if we've already prepended the URL in this request.
# Reset when a new URL is set (i.e., new create_session).
_last_prepended_url: str | None = None


@function_middleware
async def tool_logging_middleware(context: Any, call_next: Any) -> None:
    """Function middleware: logs tool calls."""
    function_name = getattr(getattr(context, "function", None), "name", "")
    arguments = getattr(context, "arguments", None)
    safe_arguments = redact_sensitive_values(str(arguments))

    if function_name == "load_skill":
        logger.info("[skill] load_skill arguments=%s", safe_arguments)
    elif "create_session" in function_name:
        logger.info("[toolbox] create_session arguments=%s", safe_arguments)
    elif function_name == "run_playwright_cli":
        logger.info("[run_playwright_cli] arguments=%s", safe_arguments)
    elif function_name == "close_browser_session":
        logger.info("[close_browser_session] arguments=%s", safe_arguments)

    await call_next()


@chat_middleware
async def live_view_url_scrub_middleware(context: Any, call_next: Any) -> None:
    """Chat middleware: strip real live_view_url from messages and inject it into the text stream.

    The get_live_view_url tool returns the real URL. The model corrupts long tokens,
    so we:
    1. Scrub the URL from function results BEFORE the model sees them
    2. AFTER the model responds, APPEND the real URL at the end of text responses
       (only when the model produces text, not intermediate function_call responses)
    """
    from .tools import _live_view_url

    _SCRUB_MESSAGE = (
        "Live view URL has been delivered directly to the user below. "
        "Do NOT output or repeat any URL. Just tell the user the "
        "live view is available below."
    )

    # Build call_id → function_name map from all messages
    call_id_to_name: dict[str, str] = {}
    for msg in context.messages:
        contents = getattr(msg, "contents", None)
        if not contents:
            continue
        for c in contents:
            if getattr(c, "type", None) == "function_call" and getattr(c, "call_id", None) and getattr(c, "name", None):
                call_id_to_name[c.call_id] = c.name

    # Find and scrub function_result entries from get_live_view_url
    scrubbed = False
    for msg in context.messages:
        contents = getattr(msg, "contents", None)
        if not contents:
            continue
        for c in contents:
            if getattr(c, "type", None) == "function_result":
                func_name = call_id_to_name.get(getattr(c, "call_id", "") or "", "")
                if func_name == _LIVE_VIEW_FUNCTION_NAME:
                    if hasattr(c, "result"):
                        c.result = _SCRUB_MESSAGE
                    if hasattr(c, "items") and c.items:
                        for item in c.items:
                            if getattr(item, "type", None) == "text" and hasattr(item, "text"):
                                item.text = _SCRUB_MESSAGE
                    scrubbed = True
                    logger.info("[chat-middleware] Scrubbed live_view_url from function result")

    await call_next()

    # Post-call: inject the URL into the stream.
    # - PREPEND once (first time we see the result) so user gets the URL immediately
    # - APPEND on text responses (so URL is visible at the end of final answer)
    if scrubbed and _live_view_url and context.stream and isinstance(context.result, ResponseStream):
        global _last_prepended_url
        should_prepend = (_last_prepended_url != _live_view_url)
        original_stream = context.result

        async def _inject_url_stream() -> AsyncIterable[ChatResponseUpdate]:
            global _last_prepended_url
            # Prepend only the first time for this URL
            if should_prepend:
                yield ChatResponseUpdate(
                    contents=[Content.from_text(text=f"🔴 [Browser Live View]({_live_view_url}) \n\n")],
                    role="assistant",
                )
                _last_prepended_url = _live_view_url

            # Yield all original chunks, tracking if any contain text
            has_text = False
            async for update in original_stream:
                yield update
                if not has_text and update.contents:
                    for c in update.contents:
                        if getattr(c, "type", None) == "text":
                            has_text = True
                            break

            # Append URL at end of text responses (so it's always at the bottom)
            if has_text:
                yield ChatResponseUpdate(
                    contents=[Content.from_text(text=f"\n\n🔴 [Browser Live View]({_live_view_url})\n")],
                    role="assistant",
                )

        context.result = ResponseStream(_inject_url_stream(), finalizer=ChatResponse.from_updates)
        logger.info("[chat-middleware] Wrapped stream for live_view_url injection")


def build_agent(settings: AgentSettings) -> tuple[Agent, MCPStreamableHTTPTool]:
    default_credential = DefaultAzureCredential()
    credential = ScopedAzureCredential(
        credential=default_credential,
        scope=settings.azure_scope,
    )
    client = FoundryChatClient(
        project_endpoint=settings.project_endpoint,
        model=settings.model,
        credential=credential,
    )

    skills_provider = SkillsProvider(skill_paths=skill_paths())
    toolbox_mcp_tool = make_toolbox_mcp_tool(settings, default_credential)
    run_playwright_cli = make_run_playwright_cli(settings)
    close_browser_session = make_close_browser_session(settings)
    get_live_view_url = make_get_live_view_url()
    instructions = (prompts_root() / "base.md").read_text(encoding="utf-8").strip()

    agent = Agent(
        client=client,
        name="browser-automation-agent-sample-foundry",
        instructions=instructions,
        tools=[run_playwright_cli, close_browser_session, get_live_view_url, toolbox_mcp_tool],
        context_providers=[skills_provider],
        middleware=[tool_logging_middleware, live_view_url_scrub_middleware],
        default_options={"store": False},
    )
    return agent, toolbox_mcp_tool