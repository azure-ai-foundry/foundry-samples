# Copyright (c) Microsoft. All rights reserved.

"""Simple Echo Agent using the Activity protocol (bring-your-own).

A minimal Activity protocol agent that echoes the user's message back. Hosted by
``azure-ai-agentserver-activity`` for the Foundry platform contract and bridged
to the M365 Agents SDK for activity processing and outbound channel delivery
(e.g. Microsoft Teams).

This sample uses the package's **default** auth model: the *simple* Teams agent.
The agent instance identity mints the Bot Connector token directly via the
Managed Identity Client (``UserManagedIdentity`` + the
``https://api.botframework.com/.default`` scope). This is the right model for a
single-tenant Teams bot whose ``msaAppId`` is the agent instance identity.

Demonstrates:
- The canonical ``ActivityAgentServerHost`` setup (simple Teams agent by default)
- Routing by activity type (``message``, ``conversationUpdate``)
- Structured logging
"""

import logging

# Configure logging first
logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s | %(message)s")
logger = logging.getLogger("simple-echo-agent")

from azure.ai.agentserver.activity import ActivityAgentServerHost

# Simple Teams agent auth model is the default.
host = ActivityAgentServerHost()
app = host.agent_app
@app.activity("message")
async def on_message(context, state):
    """Echo the user's message back."""
    user_text = (context.activity.text or "").strip()

    if user_text:
        reply = f"Echo : {user_text}"
        # Outbound delivery goes to the Bot Connector (serviceUrl). Guard it so a
        # transient delivery failure is logged instead of surfacing as a 500 on
        # the inbound webhook (which would make the Bot Connector retry).
        try:
            await context.send_activity(reply)
        except Exception as exc:  # pylint: disable=broad-exception-caught
            logger.warning("[ERROR] Could not send echo reply: %s", exc)


@app.activity("conversationUpdate")
async def on_members_added(context, state):
    """Welcome new members."""
    members = context.activity.members_added or []
    logger.info("[MEMBERS] CONVERSATION UPDATE | members_added=%d", len(members))

    for member in members:
        if member.id != context.activity.recipient.id:
            member_name = getattr(member, "name", "Guest")
            logger.info("[WAVE] MEMBER ADDED | name=%s | id=%s", member_name, getattr(member, "id", "?"))
            try:
                await context.send_activity(f"Welcome Buddy!")
                logger.info("[OK] Welcome message sent")
            except Exception as exc:
                logger.warning("[ERROR] Could not send welcome: %s", exc)


@app.error
async def on_error(context, error):
    """Handle unhandled errors."""
    logger.error("[ERROR] HANDLER ERROR | error=%s", error, exc_info=True)
    await context.send_activity(f"Sorry, something went wrong: {error}")


if __name__ == "__main__":
    print("Starting simple echo agent (bring-your-own) ...")
    host.run()
