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
async def live_view_url_inject_middleware(context: Any, call_next: Any) -> None:
    """Chat middleware: inject live_view_url into the response stream post-call.

    The get_live_view_url tool returns a placeholder (no real URL).
    After the model responds, we inject the real URL directly into the stream
    so it reaches the user without model tokenization.
    """
    from .tools import _live_view_url

    await call_next()

    # Post-call: inject the URL if available
    if not _live_view_url:
        return

    global _last_prepended_url
    should_prepend = (_last_prepended_url != _live_view_url)

    # Streaming path
    if context.stream and isinstance(context.result, ResponseStream):
        original_stream = context.result

        async def _inject_url_stream() -> AsyncIterable[ChatResponseUpdate]:
            global _last_prepended_url
            # Prepend only the first time for this URL
            if should_prepend:
                yield ChatResponseUpdate(
                    contents=[Content.from_text(text=f"🔴Created Browser Session: [Live View]({_live_view_url}) \n\n")],
                    role="assistant",
                )
                _last_prepended_url = _live_view_url

            # Yield all original chunks
            has_text = False
            async for update in original_stream:
                yield update
                if not has_text and update.contents:
                    for c in update.contents:
                        if getattr(c, "type", None) == "text":
                            has_text = True
                            break

            # Append URL at end of text responses
            if has_text:
                yield ChatResponseUpdate(
                    contents=[Content.from_text(text=f"\n\n🔴 [Browser Live View]({_live_view_url})\n")],
                    role="assistant",
                )

        context.result = ResponseStream(_inject_url_stream(), finalizer=ChatResponse.from_updates)
        logger.info("[chat-middleware] Wrapped stream for live_view_url injection")

    # Non-streaming path — only inject once per URL (same as streaming prepend guard)
    elif isinstance(context.result, ChatResponse) and should_prepend:
        from agent_framework._types import Message
        url_message = Message("assistant", [f"\n\n🔴 [Browser Live View]({_live_view_url})"])
        context.result.messages.append(url_message)
        _last_prepended_url = _live_view_url
        logger.info("[chat-middleware] Injected live_view_url into non-streaming response")


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
        middleware=[tool_logging_middleware, live_view_url_inject_middleware],
        default_options={"store": False},
    )
    return agent, toolbox_mcp_tool