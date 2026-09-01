import os
import sys
from typing import cast

from azure.identity import DefaultAzureCredential
from dotenv import load_dotenv
from langchain.agents import create_agent
from langchain_core.messages import AIMessageChunk
from langchain_azure_ai.chat_models import AzureAIOpenAIApiChatModel
from langchain_azure_ai.tools.builtin import WebSearchTool

load_dotenv()

model = AzureAIOpenAIApiChatModel(
    project_endpoint=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
    credential=DefaultAzureCredential(),
    model=os.environ["MODEL_DEPLOYMENT_NAME"],
)
agent = create_agent(
    model=model,
    tools=[WebSearchTool(search_context_size="low")],
    system_prompt=(
        "Use exactly one web search and consider at most three results. "
        "Cite the sources you use."
    ),
)

print("Starting web search...", file=sys.stderr, flush=True)
for message, _ in agent.stream(
    {
        "messages": [
            {
                "role": "user",
                "content": (
                    "Find today's most important Microsoft Foundry announcements. "
                    "Return only the three most relevant results."
                ),
            }
        ]
    },
    stream_mode="messages",
):
    message_chunk = cast(AIMessageChunk, message)
    if message_chunk.text:
        print(message_chunk.text, end="", flush=True)
print()