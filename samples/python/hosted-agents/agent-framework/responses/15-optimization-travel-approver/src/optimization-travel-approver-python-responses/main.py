# Copyright (c) Microsoft. All rights reserved.

"""Travel Request Approver — Optimization Demo Agent.

A deliberately weak travel approval agent deployed to Azure AI Foundry.
The optimizer improves its system prompt via `azd ai agent optimize`.

How optimization config gets applied:
  1. `azd ai agent optimize apply` sets OPTIMIZATION_CANDIDATE_ID env var
  2. On startup, load_config() reads the config and extracts improved instructions
  3. If no config → falls back to the weak SYSTEM_PROMPT below
"""

import json
import logging
import os
from pathlib import Path
from typing import Annotated

from agent_framework import Agent, tool
from agent_framework.foundry import FoundryChatClient
from agent_framework_foundry_hosting import ResponsesHostServer
from azure.identity import DefaultAzureCredential
from pydantic import Field
from azure.ai.agentserver.optimization import load_config, load_skills_from_dir

logger = logging.getLogger(__name__)


# --- Mocked tools (return static data for demo purposes) ---


@tool(approval_mode="never_require")
def lookup_travel_policy() -> str:
    """Look up the company travel policy rules and limits."""
    return json.dumps({
        "company": "Contoso Ltd.",
        "approval_thresholds": {"auto": 1500, "manager": 3000, "director": 7500, "vp": "above 7500"},
        "lodging_per_night": {"domestic": 250, "international": 400},
        "airfare": "economy only; business class if flight > 6 hours",
        "advance_booking_days": 14,
        "emergency_travel": "pre-approval waived; post-travel review required",
    })


@tool(approval_mode="never_require")
def check_department_budget() -> str:
    """Check the remaining travel budget for the employee's department."""
    return json.dumps({
        "department": "Engineering",
        "total_budget": 50000,
        "remaining": 14800,
        "percent_used": 63.6,
        "warning": "Budget 63.6% spent with 6 weeks remaining in fiscal year",
    })


@tool(approval_mode="never_require")
def get_flight_alternatives(
    destination: Annotated[str, Field(description="The travel destination city")],
) -> str:
    """Find cheaper flight alternatives for the given destination."""
    return json.dumps({
        "alternatives": [
            {"option": "Flexible dates (±2 days)", "savings": "$200-800"},
            {"option": "Nearby alternate airport", "savings": "$100-400"},
            {"option": "One-stop instead of direct", "savings": "$150-600"},
        ],
        "tip": "Booking 14+ days out saves 20-40%",
    })


# --- Deliberately WEAK system prompt (this is what optimization improves) ---

SYSTEM_PROMPT = """\
You are a travel assistant for Contoso Ltd.
Help employees with travel. Be nice and approving.
Try to say yes to requests when possible.
If someone asks about policy, just give general advice.
Don't worry too much about specific dollar amounts or limits.
"""


def main():
    config = load_config()

    # If no skills loaded from optimization config, load from local skills/ dir
    if not config.skills and config.skills_dir:
        config.skills.extend(load_skills_from_dir(Path(config.skills_dir)))

    model = config.model or os.environ.get("AZURE_AI_MODEL_DEPLOYMENT_NAME", "gpt-5.4-mini")
    instructions = config.compose_instructions()

    # Apply optimized tool descriptions to @tool-decorated functions
    tools = [lookup_travel_policy, check_department_budget, get_flight_alternatives]
    config.apply_tool_descriptions(tools)

    logger.info(
        "Config source=%s | model=%s | prompt_len=%d | skills=%d | tools_overridden=%d",
        config.source, model, len(instructions), len(config.skills),
        len(config.tool_definitions),
    )

    client = FoundryChatClient(
        project_endpoint=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
        model=model,
        credential=DefaultAzureCredential(),
    )

    agent = Agent(
        client=client,
        instructions=instructions,
        tools=tools,
        default_options={"store": False},
    )

    server = ResponsesHostServer(agent)
    server.run()


if __name__ == "__main__":
    main()
