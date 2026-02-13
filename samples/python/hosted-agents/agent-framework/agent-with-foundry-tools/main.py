import os
from dotenv import load_dotenv
from agent_framework.azure import AzureOpenAIChatClient

from azure.ai.agentserver.agentframework import from_agent_framework, FoundryToolsChatMiddleware
from azure.identity import DefaultAzureCredential, get_bearer_token_provider

# Load environment variables from .env file for local development
# load_dotenv()

# Create a token provider that refreshes tokens automatically for long-running servers
# This avoids 401 errors when the initial token expires (typically after 1 hour)
_credential = DefaultAzureCredential()
_token_provider = get_bearer_token_provider(_credential, "https://cognitiveservices.azure.com/.default")


def main():
    required_env_vars = [
        "AZURE_OPENAI_ENDPOINT",
        "AZURE_OPENAI_CHAT_DEPLOYMENT_NAME",
        "AZURE_AI_PROJECT_ENDPOINT",
    ]
    for env_var in required_env_vars:
        assert env_var in os.environ and os.environ[env_var], (
            f"{env_var} environment variable must be set."
        )

    tools=[{"type": "web_search_preview"}]
    if project_tool_connection_id := os.environ.get("AZURE_AI_PROJECT_TOOL_CONNECTION_ID"):
        tools.append({"type": "mcp", "project_connection_id": project_tool_connection_id})

    # Use token provider for automatic token refresh in long-running servers
    chat_client = AzureOpenAIChatClient(ad_token_provider=_token_provider,
                                        middleware=FoundryToolsChatMiddleware(tools))
    agent = chat_client.create_agent(
        name="FoundryToolAgent",
        instructions="You are a helpful assistant with access to various tools."
    )

    from_agent_framework(agent).run()

if __name__ == "__main__":
    main()
