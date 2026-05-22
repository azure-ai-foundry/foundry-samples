# Copyright (c) Microsoft. All rights reserved.

"""Hosted agent entry point for the 14-evaluation sample.

The agent itself is intentionally simple — a friendly assistant — so the focus
of this sample stays on the ``evaluate_*.py`` scripts next to this file. Both
streaming and non-streaming Responses are supported via ``ResponsesHostServer``.

Tracing is enabled by default via ``ENABLE_INSTRUMENTATION`` /
``ENABLE_SENSITIVE_DATA`` in ``agent.manifest.yaml`` and ``agent.yaml`` so the
trace-driven evaluators (``evaluate_multiturn_traces.py``,
``generate_dataset_from_traces.py``, ``evaluate_scheduled.py``) work out of
the box. See ``README.md`` for the full evaluation walkthrough.
"""

import os

from agent_framework import Agent
from agent_framework.foundry import FoundryChatClient
from agent_framework_foundry_hosting import ResponsesHostServer
from azure.identity import DefaultAzureCredential
from dotenv import load_dotenv

load_dotenv()


def main() -> None:
    client = FoundryChatClient(
        project_endpoint=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
        model=os.environ["AZURE_AI_MODEL_DEPLOYMENT_NAME"],
        credential=DefaultAzureCredential(),
    )

    agent = Agent(
        client=client,
        instructions=(
            "You are a friendly assistant. Keep your answers brief and "
            "factual. If you don't know the answer, say so."
        ),
        # History is managed by the hosting infrastructure, so we don't ask
        # the Responses API to store it. See:
        # https://developers.openai.com/api/reference/resources/responses/methods/create
        default_options={"store": False},
    )

    server = ResponsesHostServer(agent)
    server.run()


if __name__ == "__main__":
    main()
