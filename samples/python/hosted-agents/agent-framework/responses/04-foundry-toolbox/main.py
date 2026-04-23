# Copyright (c) Microsoft. All rights reserved.

import asyncio
import os

from agent_framework import Agent
from agent_framework.foundry import FoundryChatClient, select_toolbox_tools
from agent_framework_foundry_hosting import ResponsesHostServer
from azure.identity import DefaultAzureCredential
from dotenv import load_dotenv

# Load environment variables from .env file
load_dotenv()


def main():
    client = FoundryChatClient(
        project_endpoint=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
        model=os.environ["MODEL_DEPLOYMENT_NAME"],
        credential=DefaultAzureCredential(),
    )

    # Load the named toolbox from the Foundry project. Omitting `version`
    # resolves the toolbox's current default version at runtime.
    toolbox = asyncio.run(client.get_toolbox(os.environ["TOOLBOX_NAME"]))

    # Filter the toolbox to a subset of tool types before handing it to the
    # agent — the toolbox may bundle many tools (e.g., web_search,
    # code_interpreter), but this agent only needs the code interpreter.
    selected_tools = select_toolbox_tools(toolbox, include_types=["code_interpreter"])

    agent = Agent(
        client=client,
        instructions="You are a friendly assistant. Keep your answers brief.",
        tools=selected_tools,
        # History will be managed by the hosting infrastructure, thus there
        # is no need to store history by the service. Learn more at:
        # https://developers.openai.com/api/reference/resources/responses/methods/create
        default_options={"store": False},
    )

    server = ResponsesHostServer(agent)
    server.run()


if __name__ == "__main__":
    main()
