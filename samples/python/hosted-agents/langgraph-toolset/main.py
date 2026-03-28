import asyncio
import os
import subprocess
import sys
from importlib.metadata import version
import re
from urllib.parse import urlparse as _urlparse

from setup import AGENT_NAME, logger

from langchain_openai import AzureChatOpenAI
from langchain_core.tools import tool
from azure.ai.agentserver.langgraph import from_langgraph
from azure.identity import DefaultAzureCredential, get_bearer_token_provider
from langchain_mcp_adapters.client import MultiServerMCPClient

# ── LLM (Chat Completions API via Azure OpenAI endpoint) ────────────────────

PROJECT_ENDPOINT = os.getenv("AZURE_AI_PROJECT_ENDPOINT")
if not PROJECT_ENDPOINT:
    raise ValueError("AZURE_AI_PROJECT_ENDPOINT environment variable must be set")

MODEL_DEPLOYMENT_NAME = os.getenv("MODEL_DEPLOYMENT_NAME", "gpt-4o")

_parsed = _urlparse(PROJECT_ENDPOINT)
azure_openai_endpoint = os.environ.get(
    "AZURE_OPENAI_ENDPOINT",
    f"{_parsed.scheme}://{_parsed.netloc}",
)

token_provider = get_bearer_token_provider(
    DefaultAzureCredential(),
    "https://cognitiveservices.azure.com/.default",
)

llm = AzureChatOpenAI(
    model=MODEL_DEPLOYMENT_NAME,
    azure_endpoint=azure_openai_endpoint,
    azure_ad_token_provider=token_provider,
    api_version=os.environ.get("OPENAI_API_VERSION", "2025-03-01-preview"),
)

# ── Toolset MCP helpers ────────────────────────────────────────────────────

TOOLSET_ENDPOINT = os.getenv("AZURE_AI_TOOLSET_ENDPOINT")

def _get_toolset_token() -> str:
    """Get bearer token for Toolset MCP endpoint."""
    try:
        credential = DefaultAzureCredential()
        token = credential.get_token("https://ai.azure.com/.default")
        return token.token
    except Exception:
        # Fall back to az CLI
        az_cmd = "az.cmd" if sys.platform == "win32" else "az"
        result = subprocess.run(
            [az_cmd, "account", "get-access-token", "--resource", "https://ai.azure.com", "--query", "accessToken", "-o", "tsv"],
            capture_output=True, text=True, timeout=30,
        )
        if result.returncode != 0:
            raise RuntimeError(f"Failed to get token: {result.stderr}")
        return result.stdout.strip()

def _get_toolset_headers(token: str) -> dict:
    """Get required headers for Toolset MCP calls."""
    return {
        "Authorization": f"Bearer {token}",
        "Foundry-Features": "Toolsets=V1Preview",
    }

# ── Agent creation ──────────────────────────────────────────────────────────

def create_agent(model, tools):
    # for different langgraph versions
    langgraph_version = version("langgraph")
    if langgraph_version < "1.0.0":
        from langgraph.prebuilt import create_react_agent

        return create_react_agent(model, tools)
    else:
        from langchain.agents import create_agent

        return create_agent(model, tools)

async def quickstart():
    """Build and return a LangGraph agent wired to an MCP client.

    Resolution order for toolset endpoint:
      1) AZURE_AI_TOOLSET_ENDPOINT (explicit)
      2) AZURE_AI_TOOLSET_PROFILE=noauth|keyauth
      3) fallback to Microsoft Learn MCP
    """
    resolved_toolset_endpoint = TOOLSET_ENDPOINT
    if not resolved_toolset_endpoint:
        profile = os.getenv("AZURE_AI_TOOLSET_PROFILE", "").strip().lower()
        if profile == "noauth":
            resolved_toolset_endpoint = os.getenv("AZURE_AI_TOOLSET_NOAUTH_ENDPOINT") or f"{PROJECT_ENDPOINT.rstrip('/')}/toolsets/gitmcp-noauth-test/mcp?api-version=v1"
        elif profile == "keyauth":
            resolved_toolset_endpoint = os.getenv("AZURE_AI_TOOLSET_KEYAUTH_ENDPOINT") or f"{PROJECT_ENDPOINT.rstrip('/')}/toolsets/github-keyauth-test/mcp?api-version=v1"

    if resolved_toolset_endpoint:
        # Connect to Azure AI Foundry Toolset MCP endpoint
        logger.info(f"Connecting to toolset: {resolved_toolset_endpoint}")
        token = _get_toolset_token()
        headers = _get_toolset_headers(token)
        
        client = MultiServerMCPClient(
            {
                "toolset": {
                    "url": resolved_toolset_endpoint,
                    "transport": "streamable_http",
                    "headers": headers,
                }
            }
        )
    else:
        # Default: connect to MS Learn MCP server
        client = MultiServerMCPClient(
            {
                "mslearn": {
                    "url": "https://learn.microsoft.com/api/mcp",
                    "transport": "streamable_http",
                }
            }
        )
    
    tools = await client.get_tools()
    logger.info(f"Loaded {len(tools)} tools from MCP")
    return create_agent(llm, tools)

async def main():  # pragma: no cover - sample entrypoint
    agent = await quickstart()
    await from_langgraph(agent).run_async()


if __name__ == "__main__":
    asyncio.run(main())
