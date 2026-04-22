"""Copilot SDK + Foundry Toolbox agent.

Uses the GitHub Copilot SDK (CopilotClient) with Foundry toolbox MCP
tools, served via the Foundry responses protocol.
"""

import os
import pathlib
import sys

from dotenv import load_dotenv
load_dotenv(override=False)

from agent import CopilotToolboxAgent
from server import CopilotFoundryAdapter


def _discover_skill_directories() -> list[str]:
    """Return the project root if any child folder contains SKILL.md."""
    root = pathlib.Path(__file__).parent
    if any(root.glob("*/SKILL.md")):
        return [str(root.resolve())]
    return []


def _resolve_toolbox_endpoint() -> str | None:
    """Return the toolbox MCP endpoint from TOOLBOX_ENDPOINT."""
    return os.environ.get("TOOLBOX_ENDPOINT")


def create_agent() -> CopilotToolboxAgent:
    return CopilotToolboxAgent(
        skill_directories=_discover_skill_directories(),
        toolbox_endpoint=_resolve_toolbox_endpoint(),
    )


def _resolve_port() -> int | None:
    raw = os.environ.get("PORT")
    if not raw:
        return None
    try:
        return int(raw)
    except ValueError:
        print(f"Invalid PORT value {raw!r}; defaulting to framework port.")
        return None


if __name__ == "__main__":
    if not os.environ.get("GITHUB_TOKEN"):
        print("Missing GitHub Token. Make sure the .env file has one. See README for details.")
        sys.exit(1)
    adapter = CopilotFoundryAdapter(create_agent())
    adapter.run(port=_resolve_port())
