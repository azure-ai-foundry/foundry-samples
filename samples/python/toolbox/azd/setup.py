"""Telemetry bootstrap — import before any LangChain/LangGraph code."""

import logging
import os

from dotenv import load_dotenv

load_dotenv(override=False)

logger = logging.getLogger(__name__)


def setup():
    """Discover App Insights connection string.

    Must be called before AgentHost init so the connection string is
    in the environment when TracingHelper configures the exporter.
    """
    conn_str = os.environ.get("APPLICATIONINSIGHTS_CONNECTION_STRING")

    if not conn_str:
        try:
            from azure.ai.projects import AIProjectClient
            from azure.identity import DefaultAzureCredential
            endpoint = (
                os.environ.get("FOUNDRY_PROJECT_ENDPOINT")
                or os.environ.get("AZURE_AI_PROJECT_ENDPOINT")
            )
            if endpoint:
                client = AIProjectClient(credential=DefaultAzureCredential(), endpoint=endpoint)
                conn_str = client.telemetry.get_application_insights_connection_string()
                if conn_str:
                    os.environ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = conn_str
                    logger.info("Discovered App Insights connection string")
        except Exception as e:
            logger.warning("App Insights discovery failed: %s", e)

    return conn_str
