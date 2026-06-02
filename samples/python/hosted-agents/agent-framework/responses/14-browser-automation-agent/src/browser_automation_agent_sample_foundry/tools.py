# ruff: noqa: E402
from __future__ import annotations

import asyncio
import json
import logging
import os
import shlex
import shutil
import sys
from pathlib import Path
from typing import Annotated, Any

import httpx
import websockets
from websockets.exceptions import ConnectionClosedOK

from agent_framework._mcp import MCPStreamableHTTPTool
from agent_framework._tools import tool
from azure.identity import get_bearer_token_provider
from pydantic import Field

from .logging import redact_sensitive_values
from .paths import project_root
from .settings import AgentSettings

logger = logging.getLogger(__name__)


def make_subprocess_env() -> dict[str, str]:
    env = os.environ.copy()
    env["PYTHONIOENCODING"] = "utf-8"
    environment_scripts_path = Path(sys.executable).parent
    env["PATH"] = str(environment_scripts_path) + os.pathsep + env.get("PATH", "")
    return env


def resolve_playwright_cli_command(env: dict[str, str]) -> str:
    return shutil.which("playwright-cli", path=env["PATH"]) or "playwright-cli"


def decode_subprocess_output(value: bytes | str | None) -> str:
    if value is None:
        return ""
    if isinstance(value, str):
        return value
    return value.decode("utf-8", errors="replace")


async def run_playwright_cli_cleanup_command(
    playwright_cli: str,
    session_id: str | None,
    command: str,
    timeout_seconds: int,
    env: dict[str, str],
) -> dict[str, Any]:
    args = [playwright_cli]
    if session_id:
        args.append(f"-s={session_id}")
    args.append(command)

    process = await asyncio.create_subprocess_exec(
        *args,
        stdout=asyncio.subprocess.PIPE,
        stderr=asyncio.subprocess.PIPE,
        env=env,
        cwd=str(project_root()),
    )
    try:
        stdout, stderr = await asyncio.wait_for(
            process.communicate(),
            timeout=timeout_seconds,
        )
        timed_out = False
    except asyncio.TimeoutError:
        process.kill()
        stdout, stderr = await process.communicate()
        timed_out = True

    return {
        "command": redact_sensitive_values(" ".join(args)),
        "timedOut": timed_out,
        "exitCode": process.returncode,
        "stdout": redact_sensitive_values(decode_subprocess_output(stdout)).strip(),
        "stderr": redact_sensitive_values(decode_subprocess_output(stderr)).strip(),
    }


def parse_playwright_cli_command(command: str) -> list[str]:
    try:
        parts = shlex.split(command, posix=True)
    except ValueError as ex:
        raise ValueError(f"Invalid playwright-cli command arguments: {ex}") from ex
    if not parts:
        raise ValueError("command is required.")
    if parts[0] in {"playwright-cli", "npx", "npm"}:
        raise ValueError("Pass only playwright-cli arguments, not the executable name.")
    return parts


def make_run_playwright_cli(settings: AgentSettings):
    @tool(
        name="run_playwright_cli",
        description=(
            "Run playwright-cli with a named session and return stdout, stderr, and exit code. "
            "Set cdpUrl on the first command after create_session so the tool can pass "
            "PLAYWRIGHT_MCP_CDP_ENDPOINT to playwright-cli."
        ),
    )
    async def run_playwright_cli(
        sessionId: Annotated[
            str,
            Field(
                description="Local Playwright CLI session name to use for this browser session."
            ),
        ],
        command: Annotated[
            str,
            Field(
                description='playwright-cli arguments, excluding the executable and session. Example: "goto https://example.com".'
            ),
        ],
        cdpUrl: Annotated[
            str | None,
            Field(
                description="CDP WebSocket URL returned by create_session. Pass only for the first open/attach command."
            ),
        ] = None,
        timeout_seconds: Annotated[
            int | None,
            Field(
                description="Optional timeout in seconds. Defaults to the agent Playwright CLI timeout."
            ),
        ] = None,
    ) -> str:
        session_id = sessionId.strip()
        if not session_id:
            raise ValueError("sessionId is required.")

        env = make_subprocess_env()
        if cdpUrl:
            env["PLAYWRIGHT_MCP_CDP_ENDPOINT"] = cdpUrl

        effective_timeout = timeout_seconds or settings.playwright_cli_timeout_seconds
        playwright_cli = resolve_playwright_cli_command(env)
        cli_args = parse_playwright_cli_command(command)
        process_args = [playwright_cli, f"-s={session_id}", *cli_args]
        safe_command = redact_sensitive_values(" ".join(process_args))
        logger.info(
            "[run_playwright_cli] timeout=%ss command=%s",
            effective_timeout,
            safe_command,
        )

        process = await asyncio.create_subprocess_exec(
            *process_args,
            cwd=str(project_root()),
            stdout=asyncio.subprocess.PIPE,
            stderr=asyncio.subprocess.PIPE,
            env=env,
        )
        try:
            stdout_bytes, stderr_bytes = await asyncio.wait_for(
                process.communicate(),
                timeout=effective_timeout,
            )
        except asyncio.TimeoutError:
            process.kill()
            stdout_bytes, stderr_bytes = await process.communicate()
            stdout = redact_sensitive_values(decode_subprocess_output(stdout_bytes))
            stderr = redact_sensitive_values(decode_subprocess_output(stderr_bytes))
            return (
                f"Command timed out after {effective_timeout} seconds.\n"
                f"stdout:\n{stdout}\n\nstderr:\n{stderr}"
            )

        stdout = redact_sensitive_values(decode_subprocess_output(stdout_bytes))
        stderr = redact_sensitive_values(decode_subprocess_output(stderr_bytes))
        return (
            f"exit_code: {process.returncode}\n"
            f"stdout:\n{stdout or '<empty>'}\n\n"
            f"stderr:\n{stderr or '<empty>'}"
        )

    return run_playwright_cli


class ToolboxAuth(httpx.Auth):
    def __init__(self, token_provider: Any):
        self._token_provider = token_provider

    def auth_flow(self, request: httpx.Request):
        request.headers["Authorization"] = f"Bearer {self._token_provider()}"
        yield request


def resolve_toolbox_endpoint(settings: AgentSettings) -> str:
    project_endpoint = settings.project_endpoint.rstrip("/")
    return f"{project_endpoint}/toolboxes/{settings.toolbox_name}/mcp?api-version=v1"


def parse_toolbox_result(mcp_result: Any) -> str:
    parsed_content: list[Any] = []
    for item in getattr(mcp_result, "content", []) or []:
        text = getattr(item, "text", None)
        if text is None:
            parsed_content.append(str(item))
            continue

        try:
            parsed_content.append(json.loads(text))
        except json.JSONDecodeError:
            parsed_content.append(text)

    if not parsed_content:
        return "null"

    if len(parsed_content) == 1:
        value = parsed_content[0]
        if isinstance(value, str):
            return value
        # Toolbox JSON text can contain escaped characters like \u0026 in URLs.
        # Decode and re-serialize it so the model sees the actual values.
        return json.dumps(value, ensure_ascii=False)

    return json.dumps(parsed_content, ensure_ascii=False)


def make_toolbox_mcp_tool(
    settings: AgentSettings, credential: Any
) -> MCPStreamableHTTPTool:
    token_provider = get_bearer_token_provider(credential, settings.azure_scope)
    http_client = httpx.AsyncClient(
        auth=ToolboxAuth(token_provider),
        headers={"Foundry-Features": "Toolboxes=V1Preview"},
        timeout=settings.mcp_timeout_seconds,
    )

    return MCPStreamableHTTPTool(
        name=settings.toolbox_name,
        url=resolve_toolbox_endpoint(settings),
        http_client=http_client,
        request_timeout=settings.mcp_timeout_seconds,
        load_prompts=False,
        parse_tool_results=parse_toolbox_result,
        description="Creates Microsoft Playwright Workspaces remote browser sessions.",
    )


async def close_browser_by_cdp_url(cdp_url: str) -> dict[str, Any]:
    async with websockets.connect(
        cdp_url, open_timeout=10, close_timeout=10
    ) as websocket:
        await websocket.send(json.dumps({"id": 1, "method": "Browser.close"}))
        while True:
            try:
                message = json.loads(
                    await asyncio.wait_for(websocket.recv(), timeout=10)
                )
            except ConnectionClosedOK:
                return {"closed": True}
            if message.get("id") != 1:
                continue
            if "error" in message:
                raise RuntimeError(
                    f"Browser.close failed: {json.dumps(message['error'])}"
                )
            return {"closed": True}


def make_close_browser_session(settings: AgentSettings):
    @tool(
        name="close_browser_session",
        description=(
            "Close a browser automation session. This first runs playwright-cli detach "
            "to release local Playwright CLI state, then closes the remote browser by CDP URL."
        ),
    )
    async def close_browser_session(
        sessionId: Annotated[
            str,
            Field(
                description="Local Playwright CLI session name used for this browser session."
            ),
        ],
        cdpUrl: Annotated[
            str,
            Field(
                description="CDP WebSocket URL returned by create_session. Pass it so the tool can close the remote browser."
            ),
        ],
    ) -> str:
        session_id = sessionId.strip()
        if not session_id:
            raise ValueError("sessionId is required.")
        cdp_url = cdpUrl.strip()
        if not cdp_url:
            raise ValueError("cdpUrl is required.")

        env = make_subprocess_env()
        playwright_cli = resolve_playwright_cli_command(env)
        logger.info("[playwright-cli] detach sessionId=%s", session_id)
        detach_result = await run_playwright_cli_cleanup_command(
            playwright_cli,
            session_id,
            "detach",
            settings.playwright_cli_timeout_seconds,
            env,
        )

        close_error: str | None = None
        logger.info("[CDP] Browser.close sessionId=%s", session_id)
        try:
            close_result = await close_browser_by_cdp_url(cdp_url)
        except Exception as ex:
            close_result = {}
            close_error = redact_sensitive_values(str(ex))

        result = json.dumps(
            {
                "sessionId": session_id,
                "playwrightCliDetach": detach_result,
                "remoteCloseResult": close_result,
                "remoteCloseError": close_error,
            },
            indent=2,
        )
        return redact_sensitive_values(result)

    return close_browser_session
