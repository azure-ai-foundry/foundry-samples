# Copyright (c) Microsoft. All rights reserved.

"""LangGraph agent with remote MCP tools (Responses protocol).

Hosts a LangGraph agent built with `langchain.agents.create_agent` on
Foundry over the Responses protocol, using
`langchain_azure_ai.agents.hosting.ResponsesHostServer`. Tools
are loaded at startup from a remote MCP server (default: GitHub Copilot
MCP) via `langchain_mcp_adapters.client.MultiServerMCPClient`.
"""

from __future__ import annotations

import asyncio
import os

from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential, get_bearer_token_provider
from dotenv import load_dotenv
from langchain.agents import create_agent
from langchain_core.tools import BaseTool
from langchain_mcp_adapters.client import MultiServerMCPClient
from langchain_openai import ChatOpenAI

from langchain_azure_ai.agents.hosting import ResponsesHostServer

load_dotenv()

_DEFAULT_MCP_URL = "https://api.githubcopilot.com/mcp/"
_AZURE_AI_SCOPE = "https://ai.azure.com/.default"


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
    )


# ── MCP tools ────────────────────────────────────────────────────────
async def _load_mcp_tools() -> list[BaseTool]:
    pat = os.environ.get("GITHUB_PAT")
    if not pat:
        print("GITHUB_PAT not set — starting agent without MCP tools.")
        return []
    mcp_url = os.environ.get("MCP_SERVER_URL", _DEFAULT_MCP_URL)
    client = MultiServerMCPClient(
        {
            "github": {
                "transport": "http",
                "url": mcp_url,
                "headers": {"Authorization": f"Bearer {pat}"},
            }
        }
    )
    tools = await client.get_tools()
    print(f"Loaded {len(tools)} tool(s) from MCP server '{mcp_url}':")
    for t in tools:
        print(f"  - {t.name}")
    return tools


# ── Entrypoint ───────────────────────────────────────────────────────
def main() -> None:
    tools = asyncio.run(_load_mcp_tools())
    graph = create_agent(_build_chat_model(), tools=tools)

    port = int(os.environ.get("PORT", "8088"))
    ResponsesHostServer(graph).run(port=port)


if __name__ == "__main__":
    main()
