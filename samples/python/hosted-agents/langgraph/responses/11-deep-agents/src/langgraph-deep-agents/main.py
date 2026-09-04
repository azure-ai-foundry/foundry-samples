# Copyright (c) Microsoft. All rights reserved.

"""Host the deep research agent on Microsoft Foundry over Responses."""

from __future__ import annotations

import asyncio
import os

from dotenv import load_dotenv
from langchain_azure_ai.agents.hosting import (
    FoundryCheckpointSaver,
    ResponsesHostServer,
)

from agent import build_agent
from research_agent.tools import load_tools
from utils import build_chat_model

load_dotenv()


async def main() -> None:
    async with FoundryCheckpointSaver() as checkpointer:
        tools = await load_tools()
        agent = build_agent(build_chat_model(), checkpointer, tools)
        port = int(os.environ.get("PORT", "8088"))
        await ResponsesHostServer(agent).run_async(port=port)


if __name__ == "__main__":
    asyncio.run(main())
