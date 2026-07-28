# Copyright (c) Microsoft. All rights reserved.

"""A simple personal assistant on the Activity protocol.

A minimal Teams agent that chats through the **GitHub Copilot SDK** (which runs
its own model + tool-calling loop) and exposes a few custom tools: a to-do list
and reading files the user shares in the chat.

Hosted by ``azure-ai-agentserver-activity`` for the Foundry platform contract
and bridged to the M365 Agents SDK for activity processing and outbound channel
delivery (e.g. Microsoft Teams).
"""

import logging
from os import environ

_LOG_LEVEL = environ.get("LOG_LEVEL", "INFO").upper()
logging.basicConfig(
    level=getattr(logging, _LOG_LEVEL, logging.INFO),
    format="%(asctime)s %(levelname)s %(name)s:%(lineno)d | %(message)s",
)
logger = logging.getLogger("github-copilot")

from azure.ai.agentserver.activity import ActivityAgentServerHost

import client as copilot_client
import files

# Simple Teams agent auth model is the default.
host = ActivityAgentServerHost()
app = host.agent_app


@app.activity("message")
async def on_message(context, _state):
    """Chat turn: fold in any shared files, then answer via the Copilot SDK."""
    activity = context.activity
    conversation_id = activity.conversation.id if activity.conversation else "unknown"
    user_text = (activity.text or "").strip()

    file_text = await files.read_shared_files(activity)
    if file_text:
        request = user_text or "Please read the shared file(s) and give me the key insights and a short summary."
        prompt = f"{request}\n\nShared files:\n{file_text}"
    else:
        prompt = user_text
    if not prompt:
        return

    try:
        reply = await copilot_client.ask(conversation_id, prompt)
    except Exception as exc:  # pylint: disable=broad-exception-caught
        logger.error("ask failed: %s", exc, exc_info=True)
        reply = "Sorry, I hit a problem answering that."

    try:
        await context.send_activity(reply)
    except Exception as exc:  # pylint: disable=broad-exception-caught
        logger.warning("Could not send reply: %s", exc)


@app.activity("conversationUpdate")
async def on_members_added(context, _state):
    """Welcome new members."""
    members = context.activity.members_added or []
    for member in members:
        if member.id != context.activity.recipient.id:
            try:
                await context.send_activity(
                    "Hi, I'm your simple Teams assistant. Ask me anything, "
                    "or tell me to add a task."
                )
            except Exception as exc:  # pylint: disable=broad-exception-caught
                logger.warning("Could not send welcome: %s", exc)


@app.error
async def on_error(context, error):
    """Handle unhandled errors."""
    logger.error("Handler error | error=%s", error, exc_info=True)
    try:
        await context.send_activity(f"Sorry, something went wrong: {error}")
    except Exception:  # pylint: disable=broad-exception-caught
        pass


if __name__ == "__main__":
    print("Starting github-copilot agent...")
    host.run()
