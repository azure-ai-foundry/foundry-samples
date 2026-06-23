# Copyright (c) Microsoft. All rights reserved.

import asyncio
import os
from collections.abc import Callable

import httpx
from agent_framework import Agent, MCPStreamableHTTPTool
from agent_framework.foundry import FoundryChatClient
from agent_framework_foundry_hosting import ResponsesHostServer
from azure.identity import DefaultAzureCredential, get_bearer_token_provider
from dotenv import load_dotenv

# Load environment variables from .env file
load_dotenv()


def resolve_toolbox_endpoint() -> str:
    """Resolve the toolbox MCP endpoint URL from the ``TOOLBOX_ENDPOINT`` env var.

    Set it to the versioned endpoint printed by ``azd ai toolbox create`` (see
    README.md / toolbox.yaml).
    """
    endpoint = os.environ.get("TOOLBOX_ENDPOINT")
    if not endpoint:
        raise ValueError("TOOLBOX_ENDPOINT is not set")
    return endpoint


class ToolboxAuth(httpx.Auth):
    """Injects a fresh bearer token on every request."""

    def __init__(self, token_provider: Callable[[], str]):
        self._get_token = token_provider

    def auth_flow(self, request: httpx.Request):
        request.headers["Authorization"] = f"Bearer {self._get_token()}"
        yield request


async def main():
    credential = DefaultAzureCredential()

    # Token for the Foundry toolbox MCP endpoint. The toolbox proxies the call to
    # the Azure AI Search knowledge base using the agent's managed identity, which
    # is configured on the `knowledge-base-mcp` connection in agent.manifest.yaml.
    token_provider = get_bearer_token_provider(credential, "https://ai.azure.com/.default")

    toolbox_endpoint = resolve_toolbox_endpoint()

    async with httpx.AsyncClient(
        auth=ToolboxAuth(token_provider),
        headers={"Foundry-Features": "Toolboxes=V1Preview"},
        timeout=120.0,
    ) as http_client:
        toolbox = MCPStreamableHTTPTool(
            name="knowledge_base",
            url=toolbox_endpoint,
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
            instructions=(
                "You are a helpful assistant. Use the knowledge base tool to answer "
                "user questions. If the knowledge base doesn't contain the answer, "
                "respond with 'I don't know'. When you use information from the "
                "knowledge base, include citations to the retrieved sources."
            ),
            tools=toolbox,
            # History will be managed by the hosting infrastructure, thus there
            # is no need to store history by the service. Learn more at:
            # https://developers.openai.com/api/reference/resources/responses/methods/create
            default_options={"store": False},
        )

        server = ResponsesHostServer(agent)
        await server.run_async()


if __name__ == "__main__":
    asyncio.run(main())
