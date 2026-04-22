# Copyright (c) Microsoft. All rights reserved.

import os

import httpx
from agent_framework import Agent, MCPStreamableHTTPTool
from agent_framework.foundry import FoundryChatClient
from agent_framework_foundry_hosting import ResponsesHostServer
from azure.identity import DefaultAzureCredential
from dotenv import load_dotenv

# Load environment variables from .env file
load_dotenv()


class ToolboxAuth(httpx.Auth):
    """httpx Auth that injects a fresh bearer token on every request."""

    def auth_flow(self, request: httpx.Request):
        credential = DefaultAzureCredential()
        token = credential.get_token("https://ai.azure.com/.default").token
        request.headers["Authorization"] = f"Bearer {token}"
        yield request


def main():
    client = FoundryChatClient(
        project_endpoint=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
        model=os.environ["MODEL_DEPLOYMENT_NAME"],
        credential=DefaultAzureCredential(),
    )

    # Foundry Toolbox as a MCP tool
    project_endpoint = os.environ["FOUNDRY_PROJECT_ENDPOINT"]
    toolbox_name = os.environ["TOOLBOX_NAME"]
    toolbox_endpoint = f"{project_endpoint.rstrip('/')}/toolboxes/{toolbox_name}/mcp?api-version=v1"
    http_client = httpx.AsyncClient(auth=ToolboxAuth(), headers={"Foundry-Features": "Toolboxes=V1Preview"})
    foundry_mcp_tool = MCPStreamableHTTPTool(
        name="toolbox",
        url=toolbox_endpoint,
        http_client=http_client,
        load_prompts=False,
    )

    agent = Agent(
        client=client,
        instructions="You are a friendly assistant. Keep your answers brief.",
        tools=[foundry_mcp_tool],
        # History will be managed by the hosting infrastructure, thus there
        # is no need to store history by the service. Learn more at:
        # https://developers.openai.com/api/reference/resources/responses/methods/create
        default_options={"store": False},
    )

    server = ResponsesHostServer(agent)
    server.run()


if __name__ == "__main__":
    main()
