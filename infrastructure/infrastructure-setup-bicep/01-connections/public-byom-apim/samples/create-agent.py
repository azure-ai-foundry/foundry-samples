#!/usr/bin/env python3
"""
Step 6 of the public BYOM golden path: create an agent that uses the model served
through the Azure API Management (APIM) AI Gateway.

IMPORTANT
---------
A BYOM (gateway-connected) model works ONLY with a *prompt agent* invoked through the
Responses API. The classic Assistants API (create_agent + threads + runs) CANNOT resolve
a "<connection>/<model>" reference and fails with:

    invalid_engine_error: Failed to resolve model info for: ai-gateway/gpt-4o

This script uses the correct path: agents.create_version(PromptAgentDefinition(...))
followed by responses.create(..., agent_reference=...).

Prerequisites
-------------
- The public-byom-apim.bicep template has been deployed (APIM + role assignment + the
  "<connectionName>" BYOM connection on the project).
- You are logged in with an identity that can call the project data plane:
    az login
- Packages:
    pip install "azure-ai-projects>=2.0.0" azure-identity

Usage
-----
    python create-agent.py \
        --endpoint https://<account>.services.ai.azure.com/api/projects/<project> \
        --model    ai-gateway/gpt-4o \
        --prompt   "Say hello in five words."

Arguments can also be supplied via environment variables:
    PROJECT_ENDPOINT, BYOM_MODEL
"""
import argparse
import os

from azure.identity import DefaultAzureCredential
from azure.ai.projects import AIProjectClient
from azure.ai.projects.models import PromptAgentDefinition


def main() -> None:
    parser = argparse.ArgumentParser(description="Create a BYOM prompt agent and call it.")
    parser.add_argument(
        "--endpoint",
        default=os.environ.get("PROJECT_ENDPOINT"),
        help="Project endpoint, e.g. https://<account>.services.ai.azure.com/api/projects/<project>",
    )
    parser.add_argument(
        "--model",
        default=os.environ.get("BYOM_MODEL", "ai-gateway/gpt-4o"),
        help="Model reference as <connectionName>/<deploymentName> (default: ai-gateway/gpt-4o)",
    )
    parser.add_argument(
        "--agent-name",
        default="byom-gateway-agent",
        help="Name for the prompt agent (default: byom-gateway-agent)",
    )
    parser.add_argument(
        "--instructions",
        default="You are a helpful assistant.",
        help="System instructions for the agent.",
    )
    parser.add_argument(
        "--prompt",
        default="Say hello in five words.",
        help="User message to send to the agent.",
    )
    args = parser.parse_args()

    if not args.endpoint:
        parser.error("--endpoint is required (or set PROJECT_ENDPOINT).")

    project = AIProjectClient(endpoint=args.endpoint, credential=DefaultAzureCredential())

    # 1) Create a PROMPT agent version bound to the gateway model.
    agent = project.agents.create_version(
        agent_name=args.agent_name,
        definition=PromptAgentDefinition(
            model=args.model,
            instructions=args.instructions,
        ),
    )
    print(f"Created prompt agent '{agent.name}' (version {getattr(agent, 'version', '?')}) "
          f"using model '{args.model}'.")

    # 2) Invoke it through the Responses API (NOT threads/runs).
    client = project.get_openai_client()
    conversation = client.conversations.create()
    response = client.responses.create(
        conversation=conversation.id,
        input=args.prompt,
        extra_body={"agent_reference": {"name": agent.name, "type": "agent_reference"}},
    )

    print("-" * 60)
    print(response.output_text)
    print("-" * 60)


if __name__ == "__main__":
    main()
