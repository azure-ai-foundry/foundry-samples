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


def _setup_telemetry():
    try:
        from azure.identity import DefaultAzureCredential
        from azure.ai.projects import AIProjectClient

        endpoint = os.environ.get("AZURE_AI_PROJECT_ENDPOINT")
        if not endpoint:
            return

        credential = DefaultAzureCredential()
        client = AIProjectClient(credential=credential, endpoint=endpoint)
        conn_str = client.telemetry.get_application_insights_connection_string()
        if not conn_str:
            return
        os.environ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = conn_str

        from azure.monitor.opentelemetry import configure_azure_monitor

        configure_azure_monitor(
            credential=credential,
            logger_name="",
            logging_level=logging.INFO,
        )
    except Exception as e:
        print(f"Telemetry setup skipped: {e}")


_setup_telemetry()

logger = logging.getLogger("langgraph_calculator")
logger.setLevel(logging.INFO)
