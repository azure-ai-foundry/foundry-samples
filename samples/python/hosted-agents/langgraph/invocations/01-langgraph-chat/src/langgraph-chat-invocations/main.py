# Copyright (c) Microsoft. All rights reserved.

"""LangGraph multi-turn chat agent with local tools (Invocations protocol).

Hosts a LangGraph agent built with `langchain.agents.create_agent` on
Foundry over the Invocations protocol, using
`langchain_azure_ai.agents.hosting.InvocationsHostServer`.

Conversation state is persisted server-side by a LangGraph MemorySaver
checkpointer keyed by `agent_session_id` (wired by the host into
`RunnableConfig.configurable.thread_id`). Replace MemorySaver with a
durable checkpointer (Redis, Cosmos DB, etc.) for production.
"""

from __future__ import annotations

import os
from datetime import datetime, timezone
from typing import Annotated

from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential, get_bearer_token_provider
from dotenv import load_dotenv
from langchain.agents import create_agent
from langchain_core.tools import tool
from langchain_openai import ChatOpenAI
from langgraph.checkpoint.memory import MemorySaver

from langchain_azure_ai.agents.hosting import InvocationsHostServer

load_dotenv()

_AZURE_AI_SCOPE = "https://ai.azure.com/.default"


# ── Tools ────────────────────────────────────────────────────────────
@tool
def get_current_time() -> str:
    """Return the current UTC date and time."""
    return datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M:%S UTC")


@tool
def calculator(
    expression: Annotated[str, "A math expression to evaluate, e.g. '42 * 17'."],
) -> str:
    """Evaluate a simple math expression and return the result."""
    try:
        return str(eval(expression, {"__builtins__": {}}))  # noqa: S307
    except Exception as exc:
        return f"Error: {exc}"


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


# ── Entrypoint ───────────────────────────────────────────────────────
def main() -> None:

    graph = create_agent(
        _build_chat_model(),
        tools=[get_current_time, calculator],
        checkpointer=MemorySaver(),
    )

    port = int(os.environ.get("PORT", "8088"))
    InvocationsHostServer(graph).run(port=port)


if __name__ == "__main__":
    main()
