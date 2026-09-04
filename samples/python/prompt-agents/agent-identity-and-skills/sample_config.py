"""Shared, sample-owned identifiers for the investment-planner sample.

The sample provisions everything it needs, so the toolbox name and its project
connection are **constants owned by the sample** (not user-supplied env vars). The only
input required to address the connection is the Foundry project's ARM id
(``PROJECT_RESOURCE_ID``), from which the connection's ARM id is derived deterministically.
"""
import shutil
import subprocess

AGENT_NAME = "investment-planner"

# Toolbox the skills are attached to. Created on first `provision_skills.py` run
# (POST /toolboxes/{name}/versions creates the toolbox if it does not exist).
TOOLBOX_NAME = "investment-skills"

# Project connection (category RemoteTool, AgenticIdentityToken auth) that fronts the toolbox's
# MCP endpoint. The prompt agent references the toolbox only through this connection.
TOOLBOX_CONNECTION_NAME = "investment-skills-toolbox"


def run_az(args):
    """Run the Azure CLI robustly across platforms.

    On Windows the CLI is ``az.CMD``, which ``subprocess`` cannot launch by the bare name
    without a shell — and ``shell=True`` with a *list* is broken (only the first item reaches
    the shell). Resolving the real executable via ``shutil.which`` sidesteps both, with no
    shell quoting to mangle JSON/paths. Returns the completed process (stdout/stderr captured).
    """
    exe = shutil.which("az") or "az"
    return subprocess.run([exe, *args], capture_output=True, text=True)


def toolbox_mcp_url(project_endpoint: str) -> str:
    """The toolbox's data-plane MCP endpoint — the connection's ``target``."""
    return f"{project_endpoint.rstrip('/')}/toolboxes/{TOOLBOX_NAME}/mcp"


def toolbox_connection_id(project_resource_id: str) -> str:
    """Full ARM id of the toolbox connection, derived from the project resource id."""
    return f"{project_resource_id.rstrip('/')}/connections/{TOOLBOX_CONNECTION_NAME}"
