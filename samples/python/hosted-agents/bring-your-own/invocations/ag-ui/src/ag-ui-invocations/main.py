# Copyright (c) Microsoft. All rights reserved.

"""Getting-started: AG-UI protocol over Foundry invocations using Pydantic AI."""

import logging
import os
from urllib.parse import urlparse as _urlparse

from starlette.requests import Request
from starlette.responses import Response

from azure.identity import DefaultAzureCredential, get_bearer_token_provider
from openai import AsyncAzureOpenAI

from azure.ai.agentserver.invocations import InvocationAgentServerHost
from pydantic_ai import Agent
from pydantic_ai.models.openai import OpenAIResponsesModel
from pydantic_ai.providers.openai import OpenAIProvider
from pydantic_ai.ag_ui import handle_ag_ui_request

logger = logging.getLogger(__name__)

if not os.environ.get("APPLICATIONINSIGHTS_CONNECTION_STRING"):
    logger.warning(
        "APPLICATIONINSIGHTS_CONNECTION_STRING not set — traces will not be sent to "
        "Application Insights. Set it to enable local telemetry. "
        "(This variable is auto-injected in hosted Foundry containers — do not declare it in agent.manifest.yaml.)"
    )

# FOUNDRY_PROJECT_ENDPOINT is auto-injected in hosted Foundry containers.
# Locally, set it manually or use 'azd ai agent run' which sets it automatically.
_endpoint = os.environ.get("FOUNDRY_PROJECT_ENDPOINT")
if not _endpoint:
    raise EnvironmentError(
        "FOUNDRY_PROJECT_ENDPOINT environment variable is not set. "
        "Set it to your Foundry project endpoint, or use 'azd ai agent run' "
        "which sets it automatically."
    )

_deployment = os.environ.get("AZURE_AI_MODEL_DEPLOYMENT_NAME")
if not _deployment:
    raise EnvironmentError(
        "AZURE_AI_MODEL_DEPLOYMENT_NAME environment variable is not set. "
        "Set it to your model deployment name as declared in agent.manifest.yaml."
    )

# Azure OpenAI via managed identity (DefaultAzureCredential)
_credential = DefaultAzureCredential()
_token_provider = get_bearer_token_provider(
    _credential, "https://ai.azure.com/.default")

# Derive the Azure OpenAI endpoint from the Foundry project endpoint by
# stripping the path (e.g. /api/projects/proj) to get the base URL.
_parsed = _urlparse(_endpoint)
_azure_openai_endpoint = f"{_parsed.scheme}://{_parsed.netloc}"

_client = AsyncAzureOpenAI(
    azure_endpoint=_azure_openai_endpoint,
    azure_deployment=_deployment,
    azure_ad_token_provider=_token_provider,
    api_version="2025-04-01-preview",
)

model = OpenAIResponsesModel(
    _deployment, provider=OpenAIProvider(openai_client=_client))

agent = Agent(model, instructions="You are a helpful assistant.")

app = InvocationAgentServerHost()


@app.invoke_handler
async def handle_invoke(request: Request) -> Response:
    return await handle_ag_ui_request(agent, request)


if __name__ == "__main__":
    app.run()
