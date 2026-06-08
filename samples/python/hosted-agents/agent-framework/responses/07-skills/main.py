# Copyright (c) Microsoft. All rights reserved.

from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path
from typing import Any

from agent_framework import (
    Agent,
    FileSkill,
    FileSkillScript,
    Skill,
    SkillScript,
    SkillsProvider,
)
from agent_framework.foundry import FoundryChatClient
from agent_framework_foundry_hosting import ResponsesHostServer
from azure.identity import DefaultAzureCredential
from dotenv import load_dotenv

# Load environment variables from .env file
load_dotenv()


def run_local_skill_script(
    skill: Skill, script: SkillScript, args: dict[str, Any] | list[str] | None = None
) -> str:
    """Run a trusted file-based skill script with positional CLI arguments."""
    if not isinstance(skill, FileSkill) or not isinstance(script, FileSkillScript):
        return "Error: only file-based skill scripts can be run by this runner."

    skill_path = Path(skill.path).resolve()
    script_path = Path(script.full_path).resolve()
    if skill_path != script_path and skill_path not in script_path.parents:
        return f"Error: script '{script.name}' resolves outside the skill directory."

    command = [sys.executable, str(script_path)]
    if isinstance(args, list):
        for item in args:
            if not isinstance(item, str):
                return (
                    f"Error: script '{script.name}' only accepts string CLI arguments, "
                    f"but received a {type(item).__name__}."
                )
        command.extend(args)
    elif args is not None:
        return (
            f"Error: script '{script.name}' expects positional CLI arguments as a list "
            f"of strings, but received {type(args).__name__}."
        )

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
        return f"Error: script '{script.name}' timed out after 60 seconds."

    stdout = completed.stdout.strip()
    stderr = completed.stderr.strip()
    if completed.returncode != 0:
        details = stderr or stdout or "no error output was produced."
        return f"Error: script '{script.name}' failed with exit code {completed.returncode}: {details}"

    return stdout or f"Script '{script.name}' completed successfully."


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
