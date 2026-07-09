# Copyright (c) Microsoft. All rights reserved.

"""LangGraph toolbox agent with Foundry Toolbox tools (Responses protocol).

Hosts a LangGraph agent built with `langchain.agents.create_agent` on
Foundry over the Responses protocol, using
`langchain_azure_ai.agents.hosting.ResponsesHostServer`. Tools are loaded
at startup from a Foundry Toolbox via
`langchain_azure_ai.tools.AzureAIProjectToolbox`.

The toolbox exposes `web_search` plus a connection-backed GitHub Copilot
MCP tool. When the toolbox returns an OAuth consent error (MCP code
``-32006``), this sample surfaces the consent URL to the caller through a
`handle_tool_error` callback instead of crashing the turn.
"""

from __future__ import annotations

import asyncio
import os
import re
from typing import Any, AsyncIterator
from urllib.parse import urlparse

from azure.ai.agentserver.responses import CreateResponse, ResponseContext
from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential, get_bearer_token_provider
from dotenv import load_dotenv
from langchain.agents import create_agent
from langchain_core.tools import BaseTool
from langchain_openai import ChatOpenAI

from langchain_azure_ai.agents.hosting import ResponsesHostServer
from langchain_azure_ai.tools import AzureAIProjectToolbox

load_dotenv()

_AZURE_AI_SCOPE = "https://ai.azure.com/.default"

SYSTEM_PROMPT = """You are a helpful assistant with access to Foundry Toolbox tools.

When a tool returns sources (e.g. `web_search` results, or MCP responses
with URLs), ground your answer in those sources and include a brief
"Sources" section listing the titles and URLs of the results you used.
Do not invent citations. If no source URLs are present, answer without
them.
"""


# ── Chat model ───────────────────────────────────────────────────────
def _build_chat_model() -> ChatOpenAI:
    project_endpoint = os.environ["FOUNDRY_PROJECT_ENDPOINT"].rstrip("/")
    deployment = os.environ.get("AZURE_AI_MODEL_DEPLOYMENT_NAME", "gpt-4o")
    credential = DefaultAzureCredential()
    project = AIProjectClient(endpoint=project_endpoint, credential=credential)
    openai_client = project.get_openai_client()
    token_provider = get_bearer_token_provider(credential, _AZURE_AI_SCOPE)

    return ChatOpenAI(
        model=deployment,
        base_url=str(openai_client.base_url),
        api_key=token_provider,
        use_responses_api=True,
        output_version="responses/v1",
    )


# ── OAuth consent handling ───────────────────────────────────────────
# The Foundry MCP gateway signals an OAuth consent prompt by raising an
# MCP error with code -32006 and a URL on consent.azure-apim.net. We
# detect it at tool-invocation time and turn it into a friendly tool
# message instead of letting it crash the agent turn.
_CONSENT_ERROR_CODE = -32006
_CONSENT_HOST = "consent.azure-apim.net"


def _contains_consent_host(text: str) -> bool:
    for token in re.findall(r"https?://[^\s'\"<>]+", text):
        host = urlparse(token).hostname
        if host and (host == _CONSENT_HOST or host.endswith(f".{_CONSENT_HOST}")):
            return True
    return False


def _extract_consent_url(text: str) -> str | None:
    for candidate in re.findall(r"https?://[^\s)>\]\"']+", text):
        if urlparse(candidate).hostname == _CONSENT_HOST:
            return candidate
    return None


def _is_consent_error(exc: BaseException) -> bool:
    error_data = getattr(exc, "error", None)
    if error_data is not None and getattr(error_data, "code", None) == _CONSENT_ERROR_CODE:
        return True
    if _contains_consent_host(str(exc)):
        return True
    sub_exceptions = getattr(exc, "exceptions", None)
    if sub_exceptions:
        return any(_is_consent_error(sub) for sub in sub_exceptions)
    return False


def _consent_url_from_exception(exc: BaseException) -> str:
    error_data = getattr(exc, "error", None)
    if error_data is not None and getattr(error_data, "code", None) == _CONSENT_ERROR_CODE:
        message = getattr(error_data, "message", str(exc))
        return _extract_consent_url(message) or message
    url = _extract_consent_url(str(exc))
    if url:
        return url
    sub_exceptions = getattr(exc, "exceptions", None)
    if sub_exceptions:
        for sub in sub_exceptions:
            nested = _consent_url_from_exception(sub)
            if nested:
                return nested
    return str(exc)


def _consent_aware_error_handler(error: Exception) -> str:
    if _is_consent_error(error):
        url = _consent_url_from_exception(error)
        return (
            "OAuth consent required. Open this URL in a browser to authorize the "
            f"toolbox connection, then retry the request: {url}"
        )
    return f"Tool error: {error}"


# ── Tool-schema sanitization ─────────────────────────────────────────
# Some MCP servers return tools with malformed JSON schemas (e.g. an
# `object`-type schema with no `properties` field), which the OpenAI
# tool format rejects. Patch missing or empty `properties` so the model
# can call the tool.
def _sanitize_tool_schema(tool: BaseTool) -> None:
    schema: Any = tool.args_schema if isinstance(tool.args_schema, dict) else None
    if schema is None:
        return
    if schema.get("type") == "object" and "properties" not in schema:
        schema["properties"] = {}
    props = schema.get("properties", {})
    required = schema.get("required", [])
    if required and not props:
        for field_name in required:
            props[field_name] = {"type": "string"}
        schema["properties"] = props


# ── Toolbox tools ────────────────────────────────────────────────────
async def _load_toolbox_tools() -> list[BaseTool]:
    toolbox = AzureAIProjectToolbox(toolbox_name=os.environ["TOOLBOX_NAME"])
    tools = await toolbox.get_tools()
    print(f"Loaded {len(tools)} tool(s) from Foundry Toolbox '{toolbox.toolbox_name}':")
    for t in tools:
        _sanitize_tool_schema(t)
        t.handle_tool_error = _consent_aware_error_handler
        print(f"  - {t.name}")
    return tools


# ── Lazy host ────────────────────────────────────────────────────────
# Load tools on the first request so /readiness returns 200 before
# Foundry's session manager times out waiting for the container.
class _LazyToolboxHostServer(ResponsesHostServer):
    def __init__(self, chat_model: ChatOpenAI) -> None:
        super().__init__(create_agent(chat_model, tools=[], system_prompt=SYSTEM_PROMPT))
        self._chat_model = chat_model
        self._ready_lock = asyncio.Lock()
        self._ready = False

    async def _ensure_real_graph(self) -> None:
        if self._ready:
            return
        async with self._ready_lock:
            if self._ready:
                return
            tools = await _load_toolbox_tools()
            self._graph = create_agent(
                self._chat_model, tools=tools, system_prompt=SYSTEM_PROMPT
            )
            self._ready = True

    async def handle_create(
        self,
        request: CreateResponse,
        context: ResponseContext,
        cancellation_signal: asyncio.Event,
    ) -> AsyncIterator[Any]:
        await self._ensure_real_graph()
        async for event in super().handle_create(request, context, cancellation_signal):
            yield event


# ── Entrypoint ───────────────────────────────────────────────────────
def main() -> None:
    port = int(os.environ.get("PORT", "8088"))
    _LazyToolboxHostServer(_build_chat_model()).run(port=port)


if __name__ == "__main__":
    main()
