"""Azure Monitor connection string discovery.

Discovers the Application Insights connection string via AIProjectClient
and sets it as an environment variable so FoundryCBAgent.init_tracing()
can pick it up. Must be imported before the server starts.
"""

import os

os.environ["ENABLE_APPLICATION_INSIGHTS_LOGGER"] = "false"


def _discover_appinsights_connection_string():
    """Find and set APPLICATIONINSIGHTS_CONNECTION_STRING from the project endpoint."""
    try:
        from azure.identity import DefaultAzureCredential
        from azure.ai.projects import AIProjectClient

        endpoint = os.environ.get("AZURE_AI_PROJECT_ENDPOINT")
        if not endpoint:
            return

        credential = DefaultAzureCredential()
        client = AIProjectClient(credential=credential, endpoint=endpoint)
        conn_str = client.telemetry.get_application_insights_connection_string()
        if conn_str:
            os.environ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = conn_str
    except Exception as e:
        print(f"Telemetry setup skipped: {e}")
