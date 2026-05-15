# Copyright (c) Microsoft. All rights reserved.

from __future__ import annotations

import json
import os
import subprocess
import sys
from pathlib import Path
from typing import Any

from agent_framework import Agent, Skill, SkillScript, SkillsProvider
from agent_framework.foundry import FoundryChatClient
from agent_framework_foundry_hosting import ResponsesHostServer
from azure.identity import DefaultAzureCredential
from dotenv import load_dotenv

# Load environment variables from .env file
load_dotenv()


def run_local_skill_script(skill: Skill, script: SkillScript, args: dict[str, Any] | None = None) -> str:
    """Run a trusted file-based skill script with simple CLI arguments."""
    if skill.path is None or script.path is None:
        return "Error: only file-based skill scripts can be run by this runner."

    skill_path = Path(skill.path).resolve()
    script_path = (skill_path / script.path).resolve()
    if skill_path != script_path and skill_path not in script_path.parents:
        return f"Error: script '{script.path}' resolves outside the skill directory."

    command = [sys.executable, str(script_path)]
    for key, value in (args or {}).items():
        if value is None:
            continue

        option = f"--{key.replace('_', '-')}"
        if isinstance(value, bool):
            if value:
                command.append(option)
            continue

        if isinstance(value, list | tuple):
            value = ",".join(str(item) for item in value)
        elif isinstance(value, dict):
            value = json.dumps(value)

        command.extend([option, str(value)])

    try:
        completed = subprocess.run(
            command,
            cwd=skill_path,
            capture_output=True,
            check=False,
            text=True,
            timeout=60,
        )
    except subprocess.TimeoutExpired:
        return f"Error: script '{script.path}' timed out after 60 seconds."

    stdout = completed.stdout.strip()
    stderr = completed.stderr.strip()
    if completed.returncode != 0:
        details = stderr or stdout or "no error output was produced."
        return f"Error: script '{script.path}' failed with exit code {completed.returncode}: {details}"

    return stdout or f"Script '{script.path}' completed successfully."


def main():
    client = FoundryChatClient(
        project_endpoint=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
        model=os.environ["AZURE_AI_MODEL_DEPLOYMENT_NAME"],
        credential=DefaultAzureCredential(),
    )

    skills_provider = SkillsProvider.from_paths(
        skill_paths=Path(__file__).parent / "skills",
        script_runner=run_local_skill_script,
    )

    agent = Agent(
        client=client,
        instructions=(
            "You are a helpful travel planning assistant. When a user asks for a PDF "
            "travel guide, city guide, itinerary, or trip-planning document, use the "
            "travel-guide skill. After creating a guide, tell the user where the PDF "
            "was saved and summarize what it contains."
        ),
        context_providers=[skills_provider],
        # History will be managed by the hosting infrastructure, thus there
        # is no need to store history by the service. Learn more at:
        # https://developers.openai.com/api/reference/resources/responses/methods/create
        default_options={"store": False},
    )

    server = ResponsesHostServer(agent)
    server.run()


if __name__ == "__main__":
    main()
