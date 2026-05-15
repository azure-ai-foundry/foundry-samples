# Copyright (c) Microsoft. All rights reserved.

import asyncio
import httpx
import os

from agent_framework import Agent, MCPStreamableHTTPTool
from agent_framework.foundry import FoundryChatClient
from agent_framework_foundry_hosting import ResponsesHostServer
from azure.identity import DefaultAzureCredential, get_bearer_token_provider
from dotenv import load_dotenv

# Load environment variables from .env file
load_dotenv()

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


async def main():
    credential = DefaultAzureCredential()

    # Create the toolbox
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

    # Create the chat client
    client = FoundryChatClient(
        project_endpoint=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
        model=os.environ["AZURE_AI_MODEL_DEPLOYMENT_NAME"],
        credential=credential,
    )

    async with Agent(
        client=client,
        instructions="You are a friendly assistant. Keep your answers brief.",
        tools=toolbox,
        # History will be managed by the hosting infrastructure, thus there
        # is no need to store history by the service. Learn more at:
        # https://developers.openai.com/api/reference/resources/responses/methods/create
        default_options={"store": False},
    ) as agent:
        server = ResponsesHostServer(agent)
        await server.run_async()


if __name__ == "__main__":
    asyncio.run(main())
