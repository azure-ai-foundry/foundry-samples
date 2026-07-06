# Copyright (c) Microsoft. All rights reserved.

import asyncio
import logging
import os

from agent_framework import Agent
from agent_framework.azure import AzureAISearchContextProvider
from agent_framework.foundry import FoundryChatClient
from agent_framework_foundry_hosting import ResponsesHostServer
from azure.identity import DefaultAzureCredential
from dotenv import load_dotenv

# Load environment variables from .env file
load_dotenv()

logger = logging.getLogger(__name__)


def _resolved_env(name: str) -> str:
    """Return an env var value, treating un-substituted ``${VAR}`` / ``{{VAR}}`` placeholders as empty.

    Hosted-agent runtimes that perform template substitution on ``agent.yaml`` /
    ``agent.manifest.yaml`` may leave the literal ``${VAR}`` or ``{{VAR}}`` text
    when ``VAR`` is undefined at deploy time (e.g. CI smoke runs that don't
    provision an Azure Search index). The sample should treat that case the
    same as "unset" so the agent still starts and responds — just without the
    optional RAG capability.
    """
    value = os.environ.get(name, "").strip()
    if (value.startswith("${") and value.endswith("}")) or (
        value.startswith("{{") and value.endswith("}}")
    ):
        return ""
    return value


async def main():
    credential = DefaultAzureCredential()

    client = FoundryChatClient(
        project_endpoint=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
        model=os.environ["AZURE_AI_MODEL_DEPLOYMENT_NAME"],
        credential=credential,
    )

    search_endpoint = _resolved_env("AZURE_SEARCH_ENDPOINT")
    search_index_name = _resolved_env("AZURE_SEARCH_INDEX_NAME")
    context_providers = []
    if not (search_endpoint and search_index_name):
        logger.warning(
            "Azure Search environment variables are not fully set. "
            "The agent will start, but search functionality will be unavailable."
        )
    else:
        # Connect to a pre-provisioned Azure AI Search index. The index is expected to
        # exist and contain documents with the schema described in README.md
        # (id / content / sourceName / sourceLink). The context provider runs a search
        # against this index before each model invocation and injects the matching
        # documents into the model context.
        search_provider = AzureAISearchContextProvider(
            source_id="azure_search_rag",
            endpoint=search_endpoint,
            index_name=search_index_name,
            credential=credential,
            mode="semantic",
            top_k=3,
        )
        context_providers.append(search_provider)


    agent = Agent(
        client=client,
        instructions=(
            "You are a helpful support specialist for Contoso Outdoors. "
            "Answer questions using the provided context and cite the source "
            "document when available."
        ),
        context_providers=context_providers,
        # History will be managed by the hosting infrastructure, thus there
        # is no need to store history by the service. Learn more at:
        # https://developers.openai.com/api/reference/resources/responses/methods/create
        default_options={"store": False},
    )
    server = ResponsesHostServer(agent)
    await server.run_async()
    if context_providers:
        await context_providers[0].close()

if __name__ == "__main__":
    asyncio.run(main())
