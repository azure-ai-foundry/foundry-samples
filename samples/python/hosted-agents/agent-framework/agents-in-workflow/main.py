# Copyright (c) Microsoft. All rights reserved.

from agent_framework import ConcurrentBuilder
from agent_framework.azure import AzureOpenAIChatClient
from azure.ai.agentserver.agentframework import from_agent_framework
from azure.identity import DefaultAzureCredential, get_bearer_token_provider  # pyright: ignore[reportUnknownVariableType]

# Create a token provider that refreshes tokens automatically for long-running servers
# This avoids 401 errors when the initial token expires (typically after 1 hour)
_credential = DefaultAzureCredential()
_token_provider = get_bearer_token_provider(_credential, "https://cognitiveservices.azure.com/.default")


def create_workflow_builder():
    # Create agents using token provider for automatic token refresh
    researcher = AzureOpenAIChatClient(ad_token_provider=_token_provider).create_agent(
        instructions=(
            "You're an expert market and product researcher. "
            "Given a prompt, provide concise, factual insights, opportunities, and risks."
        ),
        name="researcher",
    )
    marketer = AzureOpenAIChatClient(ad_token_provider=_token_provider).create_agent(
        instructions=(
            "You're a creative marketing strategist. "
            "Craft compelling value propositions and target messaging aligned to the prompt."
        ),
        name="marketer",
    )
    legal = AzureOpenAIChatClient(ad_token_provider=_token_provider).create_agent(
        instructions=(
            "You're a cautious legal/compliance reviewer. "
            "Highlight constraints, disclaimers, and policy concerns based on the prompt."
        ),
        name="legal",
    )

    # Build a concurrent workflow
    workflow_builder = ConcurrentBuilder().participants([researcher, marketer, legal])

    return workflow_builder

def main():
    # Run the agent as a hosted agent
    from_agent_framework(create_workflow_builder().build).run()


if __name__ == "__main__":
    main()
