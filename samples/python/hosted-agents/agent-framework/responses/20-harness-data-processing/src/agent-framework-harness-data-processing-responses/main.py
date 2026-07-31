# Copyright (c) Microsoft. All rights reserved.

import os
from pathlib import Path
from shutil import copyfile

from agent_framework import FileSystemAgentFileStore, InMemoryHistoryProvider, create_harness_agent
from agent_framework.foundry import FoundryChatClient
from agent_framework_foundry_hosting import ResponsesHostServer
from azure.identity import DefaultAzureCredential
from dotenv import load_dotenv

load_dotenv()

DATA_ANALYST_INSTRUCTIONS = """\
You are a data analyst assistant. You have access to a folder of data files via the file_access_* tools.
Input files are under the input/ directory. Write generated files under the output/ directory.

## Getting started
- Start by listing the files under input/ with file_access_ls to see what data is available.
- Read the files to understand their structure and contents.

## Working with data
- When asked to analyze data, read the relevant files first, then perform the analysis.
- Show your analysis clearly with tables, summaries, and key insights.
- When calculations are needed, work through them step by step and show your reasoning.

## Writing output
- When asked to produce output files (e.g., reports, summaries, filtered data), use file_access_write to write them under output/.
- Use appropriate file formats: CSV for tabular data, Markdown for reports.
- Confirm what you wrote and where.

## Important
- Never modify or delete the original input data files unless explicitly asked to do so.
- If asked about data you haven't read yet, read it first before answering.
- Always explain your reasoning and thought process as you work through tasks.
- Always explain what you learned and what you are going to do next between tool calls, so the user can
  follow along with your thought process.
"""

MAX_CONTEXT_WINDOW_TOKENS = 1_050_000
MAX_OUTPUT_TOKENS = 128_000

bundled_working_dir = Path(__file__).parent / "working"
# Foundry maps HOME to durable storage for the hosted session.
session_home = Path(os.environ.get("HOME") or os.getcwd())
working_dir = session_home / "working"
input_dir = working_dir / "input"
output_dir = working_dir / "output"
input_dir.mkdir(parents=True, exist_ok=True)
output_dir.mkdir(parents=True, exist_ok=True)

bundled_sales_data = bundled_working_dir / "sales.csv"
session_sales_data = input_dir / bundled_sales_data.name
if not session_sales_data.exists():
    copyfile(bundled_sales_data, session_sales_data)

client = FoundryChatClient(
    project_endpoint=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
    model=os.environ["AZURE_AI_MODEL_DEPLOYMENT_NAME"],
    credential=DefaultAzureCredential(),
)

agent = create_harness_agent(
    client=client,
    disable_file_memory=True,
    # The host supplies prior Responses turns. Keep the harness provider write-only
    # so its per-service-call pipeline does not create a second history owner.
    history_provider=InMemoryHistoryProvider(load_messages=False),
    max_context_window_tokens=MAX_CONTEXT_WINDOW_TOKENS,
    max_output_tokens=MAX_OUTPUT_TOKENS,
    name="DataAnalyst",
    description="A data analyst assistant that reads, analyzes, and processes data files.",
    agent_instructions=DATA_ANALYST_INSTRUCTIONS,
    file_access_store=FileSystemAgentFileStore(working_dir),
    # Preserve the original policy without session-bound auto-approval middleware: reads run, writes request approval.
    file_access_disable_readonly_tool_approval=True,
    disable_todo=True,
    disable_mode=True,
    disable_web_search=True,
    # ResponsesHostServer translates and persists approval requests; harness auto-approval requires AgentSession.
    disable_tool_auto_approval=True,
)

app = ResponsesHostServer(agent)

if __name__ == "__main__":
    app.run()
