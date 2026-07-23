# Copyright (c) Microsoft. All rights reserved.

"""Egress Control Test Agent — validates managed egress proxy policies.

A hosted agent that accepts text commands and makes outbound HTTP requests,
returning the full response (status, headers, body). Designed for testing
RAI egress policies (Allow, Deny, Transform, Rewrite) on Azure AI Foundry
hosted agents.

Usage: send a text message to the agent with one of the supported commands.
The agent parses the command, performs the HTTP request through the egress
proxy, and returns the result.
"""

import asyncio
import json
import logging
import os
import re
import traceback
from urllib.parse import urlparse

import aiohttp
from agent_framework import Agent
from agent_framework.foundry import FoundryChatClient
from agent_framework_foundry_hosting import ResponsesHostServer
from azure.identity import DefaultAzureCredential
from dotenv import load_dotenv

load_dotenv()

logger = logging.getLogger(__name__)

# Marker headers added to every outbound request.
# These are useful for testing Transform Set/Remove operations.
DEFAULT_HEADERS = {
    "User-Agent": "egress-test-agent/2.0",
    "X-Test-Marker": "egress-header-test",
}

# Timeout for outbound HTTP requests (seconds).
REQUEST_TIMEOUT = aiohttp.ClientTimeout(total=15)

# TLS certificate verification for outbound requests.
# A managed egress proxy in "Full inspection" mode terminates TLS with its own
# certificate, so verification must be relaxed to exercise those policies. This
# is opt-in and defaults to verification ENABLED; set EGRESS_TEST_VERIFY_TLS=false
# only when testing through a TLS-intercepting egress proxy.
VERIFY_TLS = os.getenv("EGRESS_TEST_VERIFY_TLS", "true").lower() not in ("false", "0", "no")

# ── Command patterns ────────────────────────────────────────────────────
# Each pattern maps to a handler function.  Patterns are tried in order.

COMMAND_PATTERNS: list[tuple[re.Pattern, str]] = [
    (re.compile(r"test\s+connectivity", re.IGNORECASE), "cmd_connectivity"),
    (re.compile(r"test\s+headers\s+to\s+(\S+)", re.IGNORECASE), "cmd_headers"),
    (re.compile(
        r"test\s+response\s+headers\s+from\s+(\S+)", re.IGNORECASE
    ), "cmd_response_headers"),
    (re.compile(
        r"test\s+post\s+to\s+(\S+)\s+(.*)", re.IGNORECASE | re.DOTALL
    ), "cmd_post"),
    (re.compile(r"test\s+egress\s+to\s+(\S+)", re.IGNORECASE), "cmd_egress"),
]

HELP_TEXT = """\
**Egress Test Agent — Supported Commands**

| Command | Description |
|---------|-------------|
| `test egress to <url>` | GET request — returns status + body |
| `test headers to <url>` | GET request — returns echoed request headers + body (use with httpbin.org/headers) |
| `test response headers from <url>` | GET request — returns response headers only |
| `test post to <url> <json>` | POST with JSON body |
| `test connectivity` | Probe httpbin.org, example.com, google.com |
| `help` | Show this help message |
"""


# ── HTTP helpers ────────────────────────────────────────────────────────

async def _fetch(
    method: str,
    url: str,
    *,
    json_body: dict | None = None,
    extra_headers: dict[str, str] | None = None,
) -> dict:
    """Make an HTTP request and return a structured result dict."""
    headers = {**DEFAULT_HEADERS, **(extra_headers or {})}
    if not urlparse(url).scheme:
        url = f"https://{url}"

    try:
        async with aiohttp.ClientSession(
            timeout=REQUEST_TIMEOUT
        ) as session:
            async with session.request(
                method,
                url,
                headers=headers,
                json=json_body,
                ssl=VERIFY_TLS,
            ) as resp:
                body = await resp.text()
                return {
                    "url": str(resp.url),
                    "status": resp.status,
                    "response_headers": dict(resp.headers),
                    "body": body[:4000],
                }
    except aiohttp.ClientError as exc:
        return {"url": url, "error": f"{type(exc).__name__}: {exc}"}
    except Exception as exc:
        return {"url": url, "error": f"{type(exc).__name__}: {exc}"}


# ── Command handlers ───────────────────────────────────────────────────

async def cmd_egress(url: str) -> str:
    """GET request — returns status + truncated body."""
    result = _format_result(await _fetch("GET", url))
    return f"**Egress test to `{url}`**\n\n{result}"


async def cmd_headers(url: str) -> str:
    """GET request — returns echoed headers + body (for httpbin-style endpoints)."""
    data = await _fetch("GET", url)
    return f"**Headers test to `{url}`**\n\n{_format_result(data, include_headers=True)}"


async def cmd_response_headers(url: str) -> str:
    """GET request — returns response headers only."""
    data = await _fetch("GET", url)
    if "error" in data:
        return f"**Error**: {data['error']}"
    headers_str = json.dumps(data.get("response_headers", {}), indent=2)
    return f"**Response headers from `{url}`**\n\n```json\n{headers_str}\n```"


async def cmd_post(url: str, body_str: str) -> str:
    """POST with JSON body."""
    try:
        json_body = json.loads(body_str.strip())
    except json.JSONDecodeError as exc:
        return f"**Error**: invalid JSON body — {exc}"
    data = await _fetch("POST", url, json_body=json_body)
    return f"**POST to `{url}`**\n\n{_format_result(data)}"


async def cmd_connectivity() -> str:
    """Probe multiple well-known hosts."""
    targets = [
        "https://httpbin.org/get",
        "https://example.com",
        "https://www.google.com",
    ]
    lines = ["**Connectivity probe results**\n"]
    results = await asyncio.gather(
        *[_fetch("GET", t) for t in targets]
    )
    for target, data in zip(targets, results):
        status = data.get("status", "—")
        error = data.get("error", "")
        icon = "✅" if isinstance(status, int) and 200 <= status < 400 else "❌"
        detail = f"status={status}" if not error else error
        lines.append(f"| {icon} | `{target}` | {detail} |")

    return "\n".join(
        [lines[0], "| | Target | Result |", "|---|--------|--------|"] + lines[1:]
    )


def _format_result(data: dict, *, include_headers: bool = False) -> str:
    """Format a fetch result dict as markdown."""
    if "error" in data:
        return f"**Error**: {data['error']}"

    parts = [f"**Status**: {data['status']}"]
    if include_headers and "response_headers" in data:
        parts.append(
            f"**Response headers**:\n```json\n"
            f"{json.dumps(data['response_headers'], indent=2)}\n```"
        )
    body = data.get("body", "")
    if body:
        parts.append(f"**Body** (first 4000 chars):\n```\n{body}\n```")
    return "\n\n".join(parts)


# ── Command dispatcher (used as an Agent Framework tool) ────────────────

async def egress_test(command: str) -> str:
    """Parse and execute an egress test command.

    Args:
        command: The test command to execute (e.g. "test egress to https://httpbin.org/get").

    Returns:
        Formatted markdown result of the egress test.
    """
    text = command.strip()

    if text.lower() in ("help", "?"):
        return HELP_TEXT

    for pattern, handler_name in COMMAND_PATTERNS:
        match = pattern.search(text)
        if match:
            handler = globals()[handler_name]
            try:
                return await handler(*match.groups())
            except Exception as exc:
                logger.exception("Command handler %s failed", handler_name)
                return (
                    f"**Error executing command**: {type(exc).__name__}: {exc}\n\n"
                    f"```\n{traceback.format_exc()}\n```"
                )

    return (
        f"Unknown command: `{text}`\n\n"
        f"Send `help` to see supported commands."
    )


# ── Main ────────────────────────────────────────────────────────────────

def main():
    client = FoundryChatClient(
        project_endpoint=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
        model=os.getenv("AZURE_AI_MODEL_DEPLOYMENT_NAME", "gpt-4.1"),
        credential=DefaultAzureCredential(),
    )

    agent = Agent(
        client=client,
        instructions=(
            "You are an egress control test agent. Your purpose is to test "
            "network egress policies on hosted agents.\n\n"
            "When the user sends a command, call the `egress_test` tool with "
            "the user's message as the `command` argument. Return the tool's "
            "output verbatim — do not summarize or modify it.\n\n"
            "If the user doesn't provide a recognized command, call the tool "
            "with 'help' to show available commands."
        ),
        tools=[egress_test],
        default_options={"store": False},
    )

    server = ResponsesHostServer(agent)
    server.run()


if __name__ == "__main__":
    main()
