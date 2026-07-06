# Copyright (c) Microsoft. All rights reserved.

"""Foundry Memory hosted agent sample.

This agent uses :class:`FoundryMemoryProvider` to give an otherwise stateless
hosted agent persistent, semantic memory backed by an Azure AI Foundry
Memory Store. The store itself is provisioned once via
``provision_memory_store.py`` and its name is passed in through the
``MEMORY_STORE_NAME`` environment variable.

Unlike the standalone ``azure_ai_foundry_memory.py`` sample, here we construct
the :class:`FoundryChatClient` first and then reuse its underlying
``AIProjectClient`` for the memory provider, so both share a single client
instance and authentication context.
"""

import asyncio
import logging
import os

from agent_framework import Agent
from agent_framework.foundry import FoundryChatClient, FoundryMemoryProvider
from agent_framework_foundry_hosting import ResponsesHostServer
from azure.identity.aio import DefaultAzureCredential
from dotenv import load_dotenv

load_dotenv()

logger = logging.getLogger(__name__)


def _resolved_env(name: str) -> str:
    """Return an env var value, treating un-substituted ``${VAR}`` / ``{{VAR}}`` placeholders as empty.

    Hosted-agent runtimes that perform template substitution on ``agent.yaml`` /
    ``agent.manifest.yaml`` may leave the literal ``${VAR}`` or ``{{VAR}}`` text
    when ``VAR`` is undefined at deploy time (e.g. CI smoke runs that don't
    provision a memory store). The sample should treat that case the same as
    "unset" so the agent still starts and responds — just without the optional
    memory capability.
    """
    value = os.environ.get(name, "").strip()
    if (value.startswith("${") and value.endswith("}")) or (
        value.startswith("{{") and value.endswith("}}")
    ):
        return ""
    return value


async def main() -> None:
    # The chat client owns the AIProjectClient. ``allow_preview=True`` is required
    # so the same client can call the preview ``beta.memory_stores`` API used by
    # FoundryMemoryProvider.
    client = FoundryChatClient(
        project_endpoint=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
        model=os.environ["AZURE_AI_MODEL_DEPLOYMENT_NAME"],
        credential=DefaultAzureCredential(),
        allow_preview=True,
    )

    memory_store_name = _resolved_env("MEMORY_STORE_NAME")
    context_providers = []
    if not memory_store_name:
        logger.warning("MEMORY_STORE_NAME is not set; memory will not be available to the agent.")
    else:
        # Reuse the project_client that FoundryChatClient just created, instead of
        # constructing a second one for the memory provider.
        memory_provider = FoundryMemoryProvider(
            project_client=client.project_client,
            memory_store_name=memory_store_name,
            # Scope memories by user id, so each user that interacts with the agent
            # has their own isolated memories in the store (assuming those users are
            # granted access). `{{userId}}` is a special placeholder that the hosting
            # infrastructure will replace with the actual user id at runtime.
            scope="{{$userId}}",
        )
        context_providers.append(memory_provider)

    agent = Agent(
        client=client,
        instructions=(
            "You are a helpful assistant that remembers facts the user has shared "
            "across conversations. Relevant memories from previous interactions are "
            "automatically provided to you in the system context. Use them when "
            "answering, and acknowledge when you are relying on remembered facts."
        ),
        context_providers=context_providers,
        # History will be managed by the hosting infrastructure, thus there
        # is no need to store history by the service. Learn more at:
        # https://developers.openai.com/api/reference/resources/responses/methods/create
        default_options={"store": False},
    )
    server = ResponsesHostServer(agent)
    await server.run_async()


if __name__ == "__main__":
    asyncio.run(main())
