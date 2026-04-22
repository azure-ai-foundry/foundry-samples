"""Foundry responses protocol adapter — uses ResponseHandler + ResponseEventStream."""

import asyncio
import logging
from collections.abc import AsyncIterable
from typing import Any

from dotenv import load_dotenv
load_dotenv(override=False)

import _telemetry  # noqa: F401  — App Insights connection string discovery

from azure.ai.agentserver.core import AgentServerHost as AgentHost
from azure.ai.agentserver.responses import ResponseContext, ResponseEventStream
from azure.ai.agentserver.responses import get_input_expanded, CreateResponse
from azure.ai.agentserver.responses.hosting import ResponsesAgentServerHost as ResponseHandler

logger = logging.getLogger("foundry_adapter")


def _create_server_and_handler(agent):
    """Create AgentHost + ResponseHandler wired to the CopilotToolboxAgent."""

    server = AgentHost()
    responses = ResponseHandler()

    @server.shutdown_handler
    async def _shutdown():
        logger.info("Stopping CopilotToolboxAgent…")
        await agent.stop()

    @responses.response_handler
    async def handle_response(
        request: CreateResponse,
        context: ResponseContext,
        cancellation_signal: asyncio.Event,
    ) -> AsyncIterable[dict[str, Any]]:
        # Lazy-start the agent in the server's event loop (idempotent)
        await agent.start()

        text = get_input_expanded(request) or ""

        stream = ResponseEventStream(
            response_id=context.response_id,
            model=getattr(request, "model", None),
        )

        yield stream.emit_created()
        yield stream.emit_in_progress()

        message_item = stream.add_output_item_message()
        yield message_item.emit_added()

        text_content = message_item.add_text_content()
        yield text_content.emit_added()

        full_text = ""
        try:
            gen = agent.run(text, stream=True)
            async for update in gen:
                if cancellation_signal.is_set():
                    yield stream.emit_incomplete(reason="cancelled")
                    return

                chunk = getattr(update, "text", None)
                if chunk:
                    full_text += chunk
                    yield text_content.emit_delta(chunk)
        except Exception as exc:
            logger.exception("Agent streaming failed")
            error_msg = f"Error: {exc}"
            full_text += error_msg
            yield text_content.emit_delta(error_msg)

        yield text_content.emit_text_done()
        yield text_content.emit_done()
        yield message_item.emit_done()
        yield stream.emit_completed()

    return server


class CopilotFoundryAdapter:
    """Bridges CopilotToolboxAgent <-> Foundry responses protocol."""

    def __init__(self, agent):
        self._agent = agent
        self._server = _create_server_and_handler(agent)

    def run(self, port: int = None):
        if port is not None:
            self._server.run(port=port)
        else:
            self._server.run()
