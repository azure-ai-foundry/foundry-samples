# Copyright (c) Microsoft. All rights reserved.

import httpx
import os

from agent_framework import Agent, MCPStreamableHTTPTool
from agent_framework.foundry import FoundryChatClient
from agent_framework_foundry_hosting import ResponsesHostServer
from azure.identity import DefaultAzureCredential, get_bearer_token_provider
from dotenv import load_dotenv

# Load environment variables from .env file
load_dotenv()


def is_work_iq_enabled() -> bool:
    """Return whether Work IQ toolbox integration is enabled."""
    value = os.getenv("ENABLE_WORK_IQ", "true").strip().lower()
    return value not in {"0", "false", "no", "off"}


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
    # NOTE: This sample mirrors the sync `main()` + `server.run()` pattern used
    # by the other responses samples (which passes on the foundry-ext deploy path).
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
        tools=toolbox if is_work_iq_enabled() else None,
        # History is managed by the hosting infrastructure; we don't need
        # the service to store it. See:
        # https://developers.openai.com/api/reference/resources/responses/methods/create
        default_options={"store": False},
    )

    server = ResponsesHostServer(agent)
    server.run()


if __name__ == "__main__":
    main()
