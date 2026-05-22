from __future__ import annotations

from typing import Any

from .compat import ensure_agent_framework_compat

ensure_agent_framework_compat()

from agent_framework._agents import Agent
from agent_framework._mcp import MCPStreamableHTTPTool
from agent_framework._middleware import function_middleware
from agent_framework._skills import SkillsProvider
from agent_framework.foundry import FoundryChatClient
from azure.identity import DefaultAzureCredential

from .logging import log_blue, log_yellow, log_verbose, redact_sensitive_values
from .paths import skill_paths
from .prompts import build_instructions
from .settings import AgentSettings, ScopedAzureCredential
from .tools import make_close_browser_session, make_run_playwright_cli, make_toolbox_mcp_tool


@function_middleware
async def tool_logging_middleware(context: Any, call_next: Any) -> None:
    function_name = getattr(getattr(context, "function", None), "name", "")
    arguments = getattr(context, "arguments", None)
    safe_arguments = redact_sensitive_values(str(arguments))

    if function_name == "load_skill":
        log_blue(f"[skill] load_skill arguments={safe_arguments}")
    elif function_name == "create_session":
        log_blue(f"[toolbox] create_session arguments={safe_arguments}")
    elif function_name == "run_playwright_cli":
        log_yellow(f"[run_playwright_cli] arguments={safe_arguments}")
    elif function_name == "close_browser_session":
        log_yellow(f"[close_browser_session] arguments={safe_arguments}")

    await call_next()


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
    instructions = build_instructions(settings)

    log_verbose(settings.verbose, f"Active profile: {settings.profile}")

    agent = Agent(
        client=client,
        name="browser-automation-agent-sample-foundry",
        instructions=instructions,
        tools=[run_playwright_cli, close_browser_session, toolbox_mcp_tool],
        context_providers=[skills_provider],
        middleware=[tool_logging_middleware],
        default_options={"store": False},
    )
    return agent, toolbox_mcp_tool

