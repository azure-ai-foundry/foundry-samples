#!/usr/bin/env python3
"""
TC01 step 5 — consume the gateway-fronted model end to end.

This proves that inference actually flows through the APIM AI Gateway and that the
token-limit policy (TC01 step 4) is enforced.

IMPORTANT
---------
A BYOM (gateway-connected) model works ONLY with a *prompt agent* invoked through the
Responses API. The classic Assistants API (create_agent + threads + runs) CANNOT resolve
a "<connection>/<model>" reference and fails with:

    invalid_engine_error: Failed to resolve model info for: ai-gateway/gpt-5.4

A direct/user-issued call to the APIM endpoint also fails, by design: APIM's
validate-azure-ad-token policy only accepts the project's managed identity. The
prompt agent runs server-side AS the project identity, which is exactly the token
APIM expects — so this is the supported consume path.

Prerequisites
-------------
- main.bicep has been deployed (account + project + model + APIM + token-limit + the
  "<connectionName>" BYOM connection).
- You are logged in with an identity that can call the project data plane
  (Azure AI User or higher on the project):
    az login
- Packages:
    pip install "azure-ai-projects>=2.0.0" azure-identity

Usage
-----
    # single call — verifies the gateway path works
    python consume-model.py \
        --endpoint https://<account>.services.ai.azure.com/api/projects/<project> \
        --model    ai-gateway/gpt-5.4 \
        --prompt   "Say hello in five words."

    # burst — exercises the token-limit policy (expect HTTP 429 after the budget)
    python consume-model.py \
        --endpoint https://<account>.services.ai.azure.com/api/projects/<project> \
        --model    ai-gateway/gpt-5.4 \
        --repeat   8

The --endpoint and --model values are printed as `projectEndpoint` and
`modelReference` in the deployment outputs. They can also be supplied via the
PROJECT_ENDPOINT and BYOM_MODEL environment variables.
"""
import argparse
import os

from azure.identity import DefaultAzureCredential
from azure.ai.projects import AIProjectClient
from azure.ai.projects.models import PromptAgentDefinition


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Consume a BYOM gateway model end to end."
    )
    parser.add_argument(
        "--endpoint",
        default=os.environ.get("PROJECT_ENDPOINT"),
        help="Project endpoint, e.g. https://<account>.services.ai.azure.com/api/projects/<project>",
    )
    parser.add_argument(
        "--model",
        default=os.environ.get("BYOM_MODEL", "ai-gateway/gpt-5.4"),
        help="Model reference as <connectionName>/<modelName> (default: ai-gateway/gpt-5.4)",
    )
    parser.add_argument(
        "--agent-name",
        default="tc01-gateway-agent",
        help="Name for the prompt agent (default: tc01-gateway-agent)",
    )
    parser.add_argument(
        "--instructions",
        default="You are a helpful assistant.",
        help="System instructions for the agent.",
    )
    parser.add_argument(
        "--prompt",
        default="Say hello in five words.",
        help="User message to send.",
    )
    parser.add_argument(
        "--repeat",
        type=int,
        default=1,
        help="Send the prompt N times to exercise the gateway token-limit policy (default: 1).",
    )
    args = parser.parse_args()

    if not args.endpoint:
        parser.error("--endpoint is required (or set PROJECT_ENDPOINT).")

    project = AIProjectClient(
        endpoint=args.endpoint, credential=DefaultAzureCredential()
    )

    # 1) Create a PROMPT agent version bound to the gateway model.
    agent = project.agents.create_version(
        agent_name=args.agent_name,
        definition=PromptAgentDefinition(
            model=args.model,
            instructions=args.instructions,
        ),
    )
    print(f"Created prompt agent '{agent.name}' using model '{args.model}'.")

    # 2) Invoke it through the Responses API (NOT threads/runs).
    client = project.get_openai_client()
    throttled = 0
    for i in range(1, args.repeat + 1):
        try:
            conversation = client.conversations.create()
            response = client.responses.create(
                conversation=conversation.id,
                input=args.prompt,
                extra_body={
                    "agent_reference": {"name": agent.name, "type": "agent_reference"}
                },
            )
            print(f"[{i}/{args.repeat}] OK: {response.output_text}")
        except Exception as exc:  # surface any gateway error
            message = str(exc)
            first_line = message.splitlines()[0] if message else message
            if (
                "429" in message
                or "Too Many Requests" in message
                or "rate limit" in message.lower()
            ):
                throttled += 1
                print(
                    f"[{i}/{args.repeat}] THROTTLED by AI Gateway token-limit policy: {first_line}"
                )
            else:
                print(f"[{i}/{args.repeat}] ERROR: {first_line}")

    if args.repeat > 1:
        print(
            f"\nDone. {throttled} of {args.repeat} calls were throttled by the AI Gateway token-limit policy."
        )


if __name__ == "__main__":
    main()
