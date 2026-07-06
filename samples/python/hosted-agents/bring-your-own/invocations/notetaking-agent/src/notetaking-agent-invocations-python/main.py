# Copyright (c) Microsoft. All rights reserved.

"""Note-taking agent using azure-ai-agentserver-invocations with Azure OpenAI.

Uses the Azure OpenAI Responses API with function calling to understand user
intent (save/get notes) and streams responses as SSE via the Invocations
protocol. Notes are persisted per session in JSONL files accessible via the
Session Files API.

Required environment variables:
    FOUNDRY_PROJECT_ENDPOINT: Foundry project endpoint (auto-injected by the
        platform, e.g., https://account.services.ai.azure.com/api/projects/proj)
    AZURE_AI_MODEL_DEPLOYMENT_NAME: Model deployment name (e.g., gpt-4o)

Usage::

    # Set environment variables
    export FOUNDRY_PROJECT_ENDPOINT="https://account.services.ai.azure.com/api/projects/proj"
    export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4o"

    # Start the agent
    python main.py

    # Save a note
    curl -N -X POST "http://localhost:8088/invocations?agent_session_id=my-session" \\
        -H "Content-Type: application/json" \\
        -d '{"message": "save a note - book reservation for dinner"}'

    # Get all notes
    curl -N -X POST "http://localhost:8088/invocations?agent_session_id=my-session" \\
        -H "Content-Type: application/json" \\
        -d '{"message": "get all my notes"}'
"""

import asyncio
import json
import logging
import os

from starlette.requests import Request
from starlette.responses import JSONResponse, StreamingResponse

from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential

from azure.ai.agentserver.invocations import InvocationAgentServerHost

import note_store

logger = logging.getLogger(__name__)

if not os.environ.get("APPLICATIONINSIGHTS_CONNECTION_STRING"):
    logger.warning(
        "APPLICATIONINSIGHTS_CONNECTION_STRING not set — traces will not be sent to "
        "Application Insights. Set it to enable local telemetry. "
        "(This variable is auto-injected in hosted Foundry containers — do not declare it in agent.manifest.yaml.)"
    )

# ── Configuration ─────────────────────────────────────────────────────────────

FOUNDRY_PROJECT_ENDPOINT = os.environ.get("FOUNDRY_PROJECT_ENDPOINT")
if not FOUNDRY_PROJECT_ENDPOINT:
    raise EnvironmentError(
        "FOUNDRY_PROJECT_ENDPOINT environment variable is not set. "
        "Set it to your Foundry project endpoint, or use 'azd ai agent run'."
    )

AZURE_AI_MODEL_DEPLOYMENT_NAME = os.environ.get(
    "AZURE_AI_MODEL_DEPLOYMENT_NAME")
if not AZURE_AI_MODEL_DEPLOYMENT_NAME:
    raise EnvironmentError(
        "AZURE_AI_MODEL_DEPLOYMENT_NAME environment variable is not set. "
        "Set it to your model deployment name as declared in agent.manifest.yaml."
    )

_credential = DefaultAzureCredential()
_project_client = AIProjectClient(
    endpoint=FOUNDRY_PROJECT_ENDPOINT, credential=_credential)

# Use the Responses API — not chat.completions (Chat Completions API is legacy).
_openai_client = _project_client.get_openai_client()

# Tool definitions for Azure OpenAI Responses API
TOOLS = [
    {
        "type": "function",
        "name": "save_note",
        "description": "Save a note with the current timestamp. Use this when the user asks to save, add, or create a note.",
        "parameters": {
            "type": "object",
            "properties": {
                "note": {
                    "type": "string",
                    "description": "The note text to save",
                }
            },
            "required": ["note"],
        },
    },
    {
        "type": "function",
        "name": "get_notes",
        "description": "Retrieve all saved notes. Use this when the user asks to get, list, show, or view their notes.",
        "parameters": {"type": "object", "properties": {}, "required": []},
    },
]

SYSTEM_PROMPT = (
    "You are a helpful note-taking assistant. You can save notes and retrieve them. "
    "When the user asks to save a note, extract the note content and call save_note. "
    "When the user asks to see their notes, call get_notes. "
    "Always respond in a friendly, concise manner."
)


def _execute_tool_call(function_name: str, arguments: str, session_id: str) -> str:
    """Execute a tool call and return the result as JSON."""
    try:
        args = json.loads(arguments)
    except json.JSONDecodeError as e:
        return json.dumps({"error": f"Invalid tool arguments: {e}"})

    if function_name == "save_note":
        note_text = args.get("note")
        if not note_text:
            return json.dumps({"error": "Missing required 'note' argument"})
        entry = note_store.save_note(session_id, note_text)
        return json.dumps({"status": "saved", "note": entry.note, "timestamp": entry.timestamp})
    elif function_name == "get_notes":
        notes = note_store.get_notes(session_id)
        return json.dumps({
            "count": len(notes),
            "notes": [{"note": n.note, "timestamp": n.timestamp} for n in notes],
        })
    return json.dumps({"error": f"Unknown function: {function_name}"})


async def _stream_response(follow_up_input: list, session_id: str, invocation_id: str):
    """Stream the final LLM response as SSE events (after tool calls resolved)."""
    full_text = ""

    try:
        loop = asyncio.get_event_loop()
        openai_stream = await loop.run_in_executor(
            None,
            lambda: _openai_client.responses.create(
                model=AZURE_AI_MODEL_DEPLOYMENT_NAME,
                instructions=SYSTEM_PROMPT,
                input=follow_up_input,
                stream=True,
            ),
        )

        for event in openai_stream:
            if event.type == "response.output_text.delta":
                full_text += event.delta
                sse = json.dumps({"type": "token", "content": event.delta})
                yield f"data: {sse}\n\n"

    except Exception as e:
        error_msg = f"Error calling Azure OpenAI: {e}"
        full_text = error_msg
        sse = json.dumps({"type": "token", "content": error_msg})
        yield f"data: {sse}\n\n"

    done_event = json.dumps({
        "type": "done",
        "invocation_id": invocation_id,
        "session_id": session_id,
        "full_text": full_text,
    })
    yield f"data: {done_event}\n\n"


async def _stream_direct(text: str, session_id: str, invocation_id: str):
    """Stream a pre-computed response (no tool calls needed)."""
    if text:
        event = json.dumps({"type": "token", "content": text})
        yield f"data: {event}\n\n"

    done_event = json.dumps({
        "type": "done",
        "invocation_id": invocation_id,
        "session_id": session_id,
        "full_text": text,
    })
    yield f"data: {done_event}\n\n"


app = InvocationAgentServerHost()


@app.invoke_handler
async def handle_invoke(request: Request):
    """Handle note-taking requests with Azure OpenAI Responses API."""
    try:
        data = await request.json()
        if not isinstance(data, dict):
            raise ValueError("body is not a JSON object")
        user_message = data.get("message")
        if not isinstance(user_message, str) or not user_message.strip():
            raise ValueError('missing or empty "message" field')
    except (json.JSONDecodeError, ValueError):
        return JSONResponse(
            status_code=400,
            content={
                "error": "invalid_request",
                "message": (
                    'Request body must be a JSON object with a non-empty "message" string, '
                    'e.g. {"message": "save a note - book reservation for dinner"}'
                ),
            },
        )

    session_id = request.state.session_id
    invocation_id = request.state.invocation_id

    try:
        loop = asyncio.get_event_loop()
        response = await loop.run_in_executor(
            None,
            lambda: _openai_client.responses.create(
                model=AZURE_AI_MODEL_DEPLOYMENT_NAME,
                instructions=SYSTEM_PROMPT,
                input=user_message,
                tools=TOOLS,
            ),
        )
    except Exception as e:
        return StreamingResponse(
            _stream_direct(
                f"Error calling Azure OpenAI: {e}", session_id, invocation_id),
            media_type="text/event-stream",
            headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"},
        )

    # Check if there are function_call output items
    function_calls = [
        item for item in response.output
        if item.type == "function_call"
    ]

    if function_calls:
        # Execute tool calls, then stream follow-up response
        follow_up_input = []
        for fc in function_calls:
            follow_up_input.append(fc)
            result = _execute_tool_call(fc.name, fc.arguments, session_id)
            follow_up_input.append({
                "type": "function_call_output",
                "call_id": fc.call_id,
                "output": result,
            })

        return StreamingResponse(
            _stream_response(follow_up_input, session_id, invocation_id),
            media_type="text/event-stream",
            headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"},
        )
    else:
        # No tool calls — extract text from the first response directly
        direct_text = ""
        for item in response.output:
            if item.type == "message":
                for part in item.content:
                    if part.type == "output_text":
                        direct_text += part.text
        return StreamingResponse(
            _stream_direct(direct_text, session_id, invocation_id),
            media_type="text/event-stream",
            headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"},
        )


if __name__ == "__main__":
    app.run()
