"""
Foundry Model Router - Chat Completions API Example

This example demonstrates how to use Azure OpenAI's Chat Completions API
with a Foundry Model Router deployment. Model Router automatically selects
the best underlying LLM for each prompt based on your routing mode
(Balanced, Quality, or Cost).

Prerequisites:
  - An Azure OpenAI resource with a "model-router" deployment
  - A .env file in the repo root with AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_API_KEY,
    and MODEL_DEPLOYMENT_NAME

Usage:
  pip install -r ../requirements.txt
  python model-router-chat-completions.py
"""

import os
from pathlib import Path

from dotenv import load_dotenv
from openai import AzureOpenAI

# Load environment variables from .env in the repo root
load_dotenv(Path(__file__).resolve().parent.parent / ".env", override=True)

endpoint = os.environ["AZURE_OPENAI_ENDPOINT"]
api_key = os.environ["AZURE_OPENAI_API_KEY"]
deployment = os.environ["MODEL_DEPLOYMENT_NAME"]

# <chat_completion>
client = AzureOpenAI(
    azure_endpoint=endpoint,
    api_key=api_key,
    api_version="2024-10-21",
)

response = client.chat.completions.create(
    model=deployment,
    messages=[
        {"role": "system", "content": "You are a helpful assistant."},
        {
            "role": "user",
            "content": "In one sentence, name the most popular tourist destination in Seattle.",
        },
    ],
)
# </chat_completion>

print("--- Chat Completions Response ---")
print(f"Routed to model: {response.model}")
print(f"Response:\n{response.choices[0].message.content}")
print(
    f"\nUsage: {response.usage.prompt_tokens} prompt + {response.usage.completion_tokens} completion = {response.usage.total_tokens} total tokens"
)
