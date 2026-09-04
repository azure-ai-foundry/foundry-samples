"""Hello World Teams agent.

A minimal digital worker agent: it listens for Teams messages on
``/activity/messages``, feeds each message to an Azure OpenAI model, and replies
with the model's response.
"""

from __future__ import annotations

import logging
import os

from aiohttp.web import Application, Request, Response, run_app
from azure.ai.projects.aio import AIProjectClient
from azure.identity.aio import DefaultAzureCredential
from microsoft_agents.authentication.msal import MsalConnectionManager
from microsoft_agents.hosting.aiohttp import (
    CloudAdapter,
    start_agent_process,
)
from microsoft_agents.hosting.core import (
    AgentApplication,
    MemoryStorage,
    RouteRank,
    TurnContext,
    TurnState,
)
from microsoft_agents.hosting.core.authorization import (
    AgentAuthConfiguration,
    AuthTypes,
)

from .activity_routing import (
    selector,
    teams_direct_message,
    teams_group_chat_message,
    teams_tagged_channel_message,
)
from .observability import configure_hosting_observability
from .traceback_suppression import install_traceback_suppression

logger = logging.getLogger(__name__)


def build_agent(connection_manager: MsalConnectionManager) -> AgentApplication[TurnState]:
    """Create the agent application and register its surface-specific routes."""
    adapter = CloudAdapter(connection_manager=connection_manager)
    configure_hosting_observability(adapter.middleware_set)
    agent = AgentApplication[TurnState](
        storage=MemoryStorage(),
        adapter=adapter,
        connection_manager=connection_manager,
    )

    async def respond(context: TurnContext, surface: str) -> None:
        logger.info(
            "Received %s: %r",
            surface,
            context.activity.text,
            extra={"surface": surface},
        )
        project = AIProjectClient(
            endpoint=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
            credential=DefaultAzureCredential(),
        )
        if context.activity.text:
            response = await project.get_openai_client().responses.create(
                model=os.environ["AZURE_AI_MODEL_DEPLOYMENT_NAME"],
                input=context.activity.text,
            )
            await context.send_activity(response.output_text)

    async def on_teams_direct_message(
        context: TurnContext, _state: TurnState
    ) -> None:
        await respond(context, "Teams direct message")

    async def on_teams_group_chat(
        context: TurnContext, _state: TurnState
    ) -> None:
        await respond(context, "Teams group chat message")

    async def on_teams_tagged_channel_message(
        context: TurnContext, _state: TurnState
    ) -> None:
        await respond(context, "Teams tagged channel message")

    async def on_unhandled(context: TurnContext, _state: TurnState) -> None:
        logger.info(
            "Ignoring unsupported activity type=%r channel=%r",
            context.activity.type,
            context.activity.channel_id,
        )

    agent.add_route(selector(teams_direct_message), on_teams_direct_message)
    agent.add_route(selector(teams_group_chat_message), on_teams_group_chat)
    agent.add_route(
        selector(teams_tagged_channel_message),
        on_teams_tagged_channel_message,
    )
    agent.add_route(
        lambda _context: True,
        on_unhandled,
        rank=RouteRank.LAST,
    )

    return agent


def build_app() -> Application:
    """Wire the agent into an aiohttp app that serves ``/activity/messages``."""
    tenant_id = os.environ["FOUNDRY_AGENT_TENANT_ID"]
    connection = AgentAuthConfiguration(
        auth_type=AuthTypes.identity_proxy_manager,
        client_id=os.environ["FOUNDRY_AGENT_BLUEPRINT_CLIENT_ID"],
        tenant_id=tenant_id,
        authority=f"https://login.microsoftonline.com/{tenant_id}",
        scopes=["5a807f24-c9de-44ee-a3a7-329e88a00ffc/.default"],
        connection_name="SERVICE_CONNECTION",
    )
    connection_manager = MsalConnectionManager(
        connections_configurations={"SERVICE_CONNECTION": connection},
        connections_map=[{"SERVICEURL": "*", "CONNECTION": "SERVICE_CONNECTION"}],
    )
    agent = build_agent(connection_manager)
    install_traceback_suppression(agent.adapter)

    async def messages(request: Request) -> Response:
        return await start_agent_process(request, agent, agent.adapter)

    async def health(_request: Request) -> Response:
        return Response(text="Agent running!")

    app = Application()
    app.router.add_post("/activity/messages", messages)
    app.router.add_get("/readiness", health)
    return app


def main() -> None:
    logger.info("Starting agent...")
    run_app(build_app(), host="0.0.0.0", port=8088)
