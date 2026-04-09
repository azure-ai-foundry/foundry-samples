"""Shared setup: env loading, agent name, telemetry, and OpenAI clients."""

import logging
import os
import pathlib
import re

from dotenv import load_dotenv

load_dotenv(override=False)


# ── Agent name (from agent.yaml) ────────────────────────────────────────────


def _read_agent_name():
    try:
        yaml_text = pathlib.Path("agent.yaml").read_text()
        m = re.search(r"^name:\s*(.+)$", yaml_text, re.MULTILINE)
        return m.group(1).strip() if m else "unknown-agent"
    except Exception:
        return "unknown-agent"


AGENT_NAME = _read_agent_name()


# ── Telemetry (must run BEFORE agent framework imports) ─────────────────────

os.environ["ENABLE_APPLICATION_INSIGHTS_LOGGER"] = "false"

logger = logging.getLogger(AGENT_NAME)
