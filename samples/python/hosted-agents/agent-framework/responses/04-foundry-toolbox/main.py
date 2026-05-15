# Copyright (c) Microsoft. All rights reserved.

import httpx
import logging
import os

from agent_framework import Agent, MCPStreamableHTTPTool
from agent_framework.foundry import FoundryChatClient
from agent_framework_foundry_hosting import ResponsesHostServer
from azure.identity import DefaultAzureCredential, get_bearer_token_provider
from dotenv import load_dotenv

# Load environment variables from .env file
load_dotenv()

logger = logging.getLogger(__name__)


class _ResilientResponsesHostServer(ResponsesHostServer):
    """Workaround for an alpha bug in `agent_framework_foundry_hosting`.

    The built-in `_handle_inner_agent` calls `await context.get_history()`
    unconditionally on every request. When the platform issues the request
    with `store=true` + a real `conversation.id` (as the foundry-extension
    deploy path does), the history fetch can raise inside the SDK, which
    bubbles up as a platform-level `server_error: An internal server error
    occurred` with no usable diagnostic.

    Until the SDK is patched upstream, we defensively wrap `get_history` on
    the inbound context so a transient failure degrades to "no prior turns"
    instead of failing the whole request.
    """

    async def _handle_inner_agent(self, request, context):  # type: ignore[override]
        original_get_history = context.get_history

        async def safe_get_history():
            try:
                return await original_get_history()
            except Exception as ex:  # noqa: BLE001 - intentional broad catch
                logger.warning(
                    "context.get_history() failed (%s); proceeding with no prior history.",
                    ex,
                )
                return []

        # Replace the bound method on the instance for the duration of this request.
        context.get_history = safe_get_history  # type: ignore[method-assign]
        async for item in super()._handle_inner_agent(request, context):
            yield item

def resolve_toolbox_endpoint() -> str:
    """Resolve the toolbox MCP endpoint URL."""
    project_endpoint = os.environ["FOUNDRY_PROJECT_ENDPOINT"].rstrip("/")
    toolbox_name = os.environ["TOOLBOX_NAME"]
    return f"{project_endpoint}/toolboxes/{toolbox_name}/mcp?api-version=v1"

class ToolboxAuth(httpx.Auth):
    """Injects a fresh bearer token on every request."""
    def __init__(self, token_provider):
        self._get_token = token_provider
    def auth_flow(self, request):
        request.headers["Authorization"] = f"Bearer {self._get_token()}"
        yield request


def main():
    # NOTE: This sample mirrors the sync `main()` + `server.run()` pattern of
    # the sister 03-mcp sample (which passes on the foundry-ext deploy path).
    # The previous async/`async with Agent(...)` pattern eagerly entered the
    # MCPStreamableHTTPTool context at startup, which performs a network
    # initialize + tools/list against the toolbox MCP endpoint before the
    # HTTP server is bound. On the foundry-ext deploy path the platform
    # probes /readiness within ~90s of container start; if the MCP handshake
    # is still in flight, /readiness never returns 200 and the platform
    # raises 424 session_not_ready on every invoke. Letting the Agent enter
    # the tool context lazily on first request avoids the readiness race.
    credential = DefaultAzureCredential()

    token_provider = get_bearer_token_provider(
        credential, "https://ai.azure.com/.default"
    )

    http_client = httpx.AsyncClient(
        auth=ToolboxAuth(token_provider),
        headers={"Foundry-Features": "Toolboxes=V1Preview"},
        timeout=120.0,
    )

    toolbox = MCPStreamableHTTPTool(
        name=os.environ["TOOLBOX_NAME"],
        url=resolve_toolbox_endpoint(),
        http_client=http_client,
        load_prompts=False,
    )

    client = FoundryChatClient(
        project_endpoint=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
        model=os.environ["AZURE_AI_MODEL_DEPLOYMENT_NAME"],
        credential=credential,
    )

    agent = Agent(
        client=client,
        instructions="You are a friendly assistant. Keep your answers brief.",
        tools=toolbox,
        # History is managed by the hosting infrastructure; we don't need
        # the service to store it. See:
        # https://developers.openai.com/api/reference/resources/responses/methods/create
        default_options={"store": False},
    )

    server = _ResilientResponsesHostServer(agent)
    server.run()


if __name__ == "__main__":
    main()

