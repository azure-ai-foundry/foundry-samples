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
import outfiles
import cards
import tools as _tools


async def _deliver_ui(context, conversation_id, stream) -> None:
    """Drain model-requested UI (task board, generated files) and attach it.

    Tools can't send activities, so they queue requests (see tools.queue_ui);
    we render them here. On a streaming turn the attachments ride the final
    chunk; otherwise they go as separate messages.
    """
    from microsoft_agents.activity import Activity, ActivityTypes

    actions = _tools.drain_ui(conversation_id)
    for action in actions:
        try:
            if action.get("type") == "task_board":
                tasks = _tools.get_tasks(conversation_id)
                att, lead = cards.tasks_card_attachment(conversation_id, tasks), ""
            elif action.get("type") == "file":
                # The model created the file itself (via its shell/python tools)
                # and handed us the path; read the bytes and offer them for
                # download. See tools._deliver_file.
                import os
                path = action.get("path") or ""
                with open(path, "rb") as fh:
                    data = fh.read()
                att, lead = outfiles.build_file_consent(os.path.basename(path) or "document.txt", data)
            else:
                continue
        except Exception as exc:  # pylint: disable=broad-exception-caught
            logger.error("UI render failed: %s", exc, exc_info=True)
            continue

        if stream is not None:
            if lead:
                stream.queue_text_chunk(lead)
            stream.add_attachment(att)
        else:
            if lead:
                await context.send_activity(lead)
            await context.send_activity(Activity(type=ActivityTypes.message, attachments=[att]))


# Simple Teams agent auth model is the default.
host = ActivityAgentServerHost()
app = host.agent_app


@app.activity("message")
async def on_message(context, _state):
    """Chat turn: fold in any shared files, then answer via the Copilot SDK."""
    activity = context.activity
    conversation_id = activity.conversation.id if activity.conversation else "unknown"
    user_text = (activity.text or "").strip()

    # Try to start a streaming response; not all channels support it.
    stream = None
    try:
        stream = context.streaming_response
    except Exception:  # pylint: disable=broad-exception-caught
        stream = None

    def _progress(msg: str) -> None:
        if stream is not None:
            try:
                stream.queue_informative_update(msg)
            except Exception:  # pylint: disable=broad-exception-caught
                pass

    # Download any shared files verbatim (no extraction) so we can hand their
    # raw paths to the model as attachments — it reads/analyzes them itself.
    # The authenticated connector (needed to fetch Copilot inline images) lives
    # on the turn state.
    connector = None
    try:
        connector = context.turn_state.get("ConnectorClient")
    except Exception:  # pylint: disable=broad-exception-caught
        connector = None
    shared_files = await files.download_shared_files(
        activity, conversation_id, on_progress=_progress, connector=connector
    )
    if shared_files:
        names = ", ".join(f["name"] for f in shared_files)
        prompt = user_text or f"Please read the shared file(s) ({names}) and give me the key insights and a short summary."
    else:
        prompt = user_text

    if not prompt or stream is None:
        if stream is not None:
            try:
                await stream.end_stream()
            except Exception:  # pylint: disable=broad-exception-caught
                pass
        return

    # Streaming path: forward progress + text chunks, then finalize.
    try:
        async for kind, chunk in copilot_client.ask_stream(conversation_id, prompt, shared_files):
            if kind == "progress":
                stream.queue_informative_update(chunk)
            elif kind in ("delta", "final") and chunk:
                stream.queue_text_chunk(chunk)
        # Attach any model-requested UI (task board, generated file) to the same
        # stream before finalizing.
        await _deliver_ui(context, conversation_id, stream)
    except Exception as exc:  # pylint: disable=broad-exception-caught
        logger.error("stream failed: %s", exc, exc_info=True)
        try:
            stream.queue_text_chunk("Sorry, I hit a problem answering that.")
        except Exception:  # pylint: disable=broad-exception-caught
            pass
    finally:
        try:
            await stream.end_stream()
        except Exception as exc:  # pylint: disable=broad-exception-caught
            logger.warning("Could not end stream: %s", exc)


@app.activity("invoke")
async def on_invoke(context, _state):
    """Handle invoke activities — file-consent responses and card actions."""
    activity = context.activity
    name = getattr(activity, "name", None)

    # Teams file-consent (outbound file download).
    if outfiles.is_file_consent_invoke(activity):
        try:
            await outfiles.handle_file_consent_invoke(context)
        except Exception as exc:  # pylint: disable=broad-exception-caught
            logger.error("file consent handling failed: %s", exc, exc_info=True)
        return

    # Adaptive Card Universal Action (task board buttons).
    if name == cards.ADAPTIVE_ACTION_INVOKE:
        conversation_id = activity.conversation.id if activity.conversation else "unknown"
        parsed = cards.parse_action(activity)
        if parsed:
            verb, data = parsed
            task_id = data.get("taskId")
            try:
                if verb == "complete_task" and task_id:
                    _tools.complete_task_direct(conversation_id, task_id)
                elif verb == "delete_task" and task_id:
                    _tools.delete_task_direct(conversation_id, task_id)
                elif verb == "add_task":
                    # New task text comes from the card's Input.Text ('newTask').
                    val = getattr(activity, "value", None) or {}
                    if not isinstance(val, dict):
                        dump = getattr(val, "model_dump", None)
                        val = dump(by_alias=True) if callable(dump) else {}
                    action = val.get("action") or {}
                    if not isinstance(action, dict):
                        dump = getattr(action, "model_dump", None)
                        action = dump(by_alias=True) if callable(dump) else {}
                    inputs = action.get("data") or {}
                    new_title = (inputs.get("newTask") or "").strip() if isinstance(inputs, dict) else ""
                    if new_title:
                        _tools.add_task_direct(conversation_id, new_title)
                # refresh_tasks falls through to just re-render.
            except Exception as exc:  # pylint: disable=broad-exception-caught
                logger.error("card action failed: %s", exc, exc_info=True)

            # Return a refreshed card as an IN-PLACE update via the invoke
            # response. Do NOT send a new card message — that would re-render a
            # card and (if it had an auto-refresh) loop. The invoke response
            # replaces the existing card in the chat.
            tasks = _tools.get_tasks(conversation_id)
            card = cards.build_tasks_card(conversation_id, tasks)
            resp = cards.card_invoke_response(card)
            try:
                from microsoft_agents.activity import Activity, ActivityTypes, InvokeResponse
                await context.send_activity(Activity(
                    type=ActivityTypes.invoke_response,
                    value=InvokeResponse(status=200, body=resp),
                ))
            except Exception as exc:  # pylint: disable=broad-exception-caught
                logger.warning("could not send invoke response: %s", exc)
        return

    return


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
    print("Starting the GitHub Copilot SDK agent ...")
    host.run()

