# Copyright (c) Microsoft. All rights reserved.

import logging
import os

from agent_framework import Agent
from agent_framework.foundry import FoundryChatClient
from agent_framework_foundry_hosting import ResponsesHostServer
from azure.identity import DefaultAzureCredential
from dotenv import load_dotenv

from tools import ALL_TOOLS

load_dotenv()

logger = logging.getLogger(__name__)


def main():
    if not os.environ.get("APPLICATIONINSIGHTS_CONNECTION_STRING"):
        logger.warning(
            "APPLICATIONINSIGHTS_CONNECTION_STRING not set — traces will not be sent "
            "to Application Insights. Set it to enable local telemetry."
        )

    client = FoundryChatClient(
        project_endpoint=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
        model=os.environ["AZURE_AI_MODEL_DEPLOYMENT_NAME"],
        credential=DefaultAzureCredential(),
    )

    agent = Agent(
        client=client,
        instructions=(
            "You are an assistant that performs data-plane operations on two "
            "Azure services on the user's behalf using your per-agent Microsoft "
            "Entra identity:\n"
            "  - Azure Blob Storage  (storage_* tools)\n"
            "  - Azure Service Bus   (servicebus_* tools)\n"
            "Pick the tool that matches the service the user named. Confirm each "
            "action briefly when complete."
        ),
        tools=ALL_TOOLS,
        default_options={"store": False},
    )

    server = ResponsesHostServer(agent)
    server.run()


if __name__ == "__main__":
    main()
