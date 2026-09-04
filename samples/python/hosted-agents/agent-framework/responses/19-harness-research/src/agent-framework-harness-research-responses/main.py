# Copyright (c) Microsoft. All rights reserved.

import os
from pathlib import Path

from agent_framework import (
    FileSystemAgentFileStore,
    InMemoryHistoryProvider,
    create_harness_agent,
    todos_remaining,
    todos_remaining_message,
)
from agent_framework.foundry import FoundryChatClient
from agent_framework_foundry_hosting import ResponsesHostServer
from azure.identity import DefaultAzureCredential
from dotenv import load_dotenv

load_dotenv()

RESEARCH_INSTRUCTIONS = """\
## Research Assistant Instructions

You are a research assistant. When given a research topic, research it
thoroughly using web search and web browsing. Use your knowledge to form good
search queries and hypotheses, but always verify claims with the tools
available to you rather than relying on memory alone.

### Research quality

Consult multiple sources when possible and cross-reference key claims.
When sources disagree, note the discrepancy and explain which source you
consider more reliable and why.
If a web page fails to load or a search returns irrelevant results, try
alternative search queries or sources before moving on.
Track your sources — you will need them when presenting results.

### Presenting results

When presenting your final findings:
- Use Markdown formatting for clarity.
- Use clear sections with headings for each major topic or sub-question.
- Cite your sources inline (e.g., "According to [source name](URL), ...").
- End with a brief summary of key takeaways.
- In addition to returning the results to the user, save the final research
  report to file memory so it survives compaction and can be referenced later.
"""

client = FoundryChatClient(
    project_endpoint=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
    model=os.environ["AZURE_AI_MODEL_DEPLOYMENT_NAME"],
    credential=DefaultAzureCredential(),
)

# Foundry maps HOME to durable storage for the hosted session.
session_home = Path(os.environ.get("HOME") or os.getcwd())

agent = create_harness_agent(
    client=client,
    file_memory_store=FileSystemAgentFileStore(session_home / "agent-file-memory"),
    # The host supplies prior Responses turns. Keep the harness provider write-only
    # so its per-service-call pipeline does not create a second history owner.
    history_provider=InMemoryHistoryProvider(load_messages=False),
    max_context_window_tokens=128_000,
    max_output_tokens=16_384,
    name="ResearchAgent",
    description="A research assistant that plans and executes research tasks.",
    agent_instructions=RESEARCH_INSTRUCTIONS,
    loop_should_continue=todos_remaining(looping_modes=["execute"]),
    loop_next_message=todos_remaining_message,
    loop_max_iterations=10,
    # The Responses host translates and persists approval requests; harness auto-approval requires an explicit session.
    disable_tool_auto_approval=True,
)

app = ResponsesHostServer(agent)

if __name__ == "__main__":
    app.run()
