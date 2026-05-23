# Copyright (c) Microsoft. All rights reserved.

"""Env Vars Agent — Bring Your Own Responses agent (Python).

Hosted agent that demonstrates Foundry's connection-templated environment-variable
injection. Four example env vars are declared in ``agent.manifest.yaml`` —
covering all four corners of the connection grid (ApiKey x CustomKeys x secret
x non-secret) — and resolved at runtime by the platform's secret resolver.

The agent exposes a single function-calling tool, ``get_env_var(name, kind)``,
that returns the runtime value with a kind-aware safety policy:

* ``metadata`` and ``target`` -> the whole value is returned (these are plain,
  non-secret data — region, endpoint URL, account name, feature flags, …).
* ``credentials`` (the default) -> only a SAFE fingerprint is returned
  (length + first 4 chars + placeholder-resolved check). The raw secret value
  never leaves the agent process.

Required environment variables:
    FOUNDRY_PROJECT_ENDPOINT: Foundry project endpoint (auto-injected by the platform)
    AZURE_AI_MODEL_DEPLOYMENT_NAME: Model deployment name (e.g., gpt-4.1-mini)

Example env vars (resolved from connections in hosted mode; set manually for local runs):
    SECRET_API_KEY     — ${{connections.api-key-conn.credentials.key}}            (ApiKey credentials.key)
    TARGET             — ${{connections.api-key-conn.target}}                     (ApiKey target)
    SECRET_KEY         — ${{connections.custom-keys-conn.credentials.secret_key}} (CustomKeys credentials.<key>)
    NON_SECRET_KEY     — ${{connections.custom-keys-conn.metadata.non_secret_key}} (CustomKeys metadata.<key>)

Usage::

    # Set environment variables
    export FOUNDRY_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
    export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4.1-mini"
    export SECRET_API_KEY="ab12-fake-test-key"
    export TARGET="https://api.example.com"
    export SECRET_KEY="p@ssw0rd-test-value"
    export NON_SECRET_KEY="westus2"

    # Start the agent
    python main.py

    # Read a non-secret env var (whole value)
    curl -N -X POST http://localhost:8088/responses \\
        -H "Content-Type: application/json" \\
        -d '{"input": "what is TARGET? it is the target of an ApiKey connection.", "stream": true}'

    # Verify a secret env var resolved (fingerprint only)
    curl -N -X POST http://localhost:8088/responses \\
        -H "Content-Type: application/json" \\
        -d '{"input": "did SECRET_API_KEY resolve? it is a credentials placeholder.", "stream": true}'
"""

import asyncio
import json
import logging
import os

from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential

from azure.ai.agentserver.responses import (
    CreateResponse,
    ResponseContext,
    ResponseEventStream,
    ResponsesAgentServerHost,
)

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

AZURE_AI_MODEL_DEPLOYMENT_NAME = os.environ.get("AZURE_AI_MODEL_DEPLOYMENT_NAME")
if not AZURE_AI_MODEL_DEPLOYMENT_NAME:
    raise EnvironmentError(
        "AZURE_AI_MODEL_DEPLOYMENT_NAME environment variable is not set. "
        "Set it to your model deployment name as declared in agent.manifest.yaml."
    )

_credential = DefaultAzureCredential()
_project_client = AIProjectClient(
    endpoint=FOUNDRY_PROJECT_ENDPOINT, credential=_credential
)

# Use the Responses API — not chat.completions (Chat Completions API is legacy).
_openai_client = _project_client.get_openai_client()

# ── Tool definitions ──────────────────────────────────────────────────────────

TOOLS = [
    {
        "type": "function",
        "name": "get_env_var",
        "description": (
            "Read an environment variable injected at container start by the "
            "Foundry secret resolver. The 'kind' argument controls what is "
            "returned: 'metadata' and 'target' return the whole value (these "
            "are plain, non-secret); 'credentials' (the default) returns a "
            "safe fingerprint only — never the raw secret. Pick 'kind' from "
            "the placeholder syntax: ${{connections.<name>.credentials.X}} -> "
            "credentials, ${{connections.<name>.target}} -> target, "
            "${{connections.<name>.metadata.X}} -> metadata."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "name": {
                    "type": "string",
                    "description": "The name of the environment variable to read (e.g., TARGET, SECRET_API_KEY).",
                },
                "kind": {
                    "type": "string",
                    "enum": ["metadata", "target", "credentials"],
                    "description": (
                        "Which connection field this env var came from. "
                        "Use 'metadata' for plain custom-key values, 'target' "
                        "for the connection's endpoint URL, and 'credentials' "
                        "for any secret. Defaults to 'credentials' (safe)."
                    ),
                },
            },
            "required": ["name"],
        },
    },
]

SYSTEM_PROMPT = (
    "You are a diagnostic assistant for a Foundry hosted agent. The container "
    "has several environment variables injected by the platform's secret "
    "resolver from connection templates of the form "
    "${{connections.<conn>.credentials.<key>}}, ${{connections.<conn>.target}}, "
    "or ${{connections.<conn>.metadata.<key>}}. To inspect an env var, call "
    "the get_env_var tool with the variable name and a 'kind' that matches "
    "where the value came from on the connection: 'credentials' for secrets "
    "(returns a fingerprint only), 'target' for endpoint URLs, or 'metadata' "
    "for plain non-secret values. When in doubt, default to 'credentials'. "
    "Summarize the tool result clearly. Never repeat raw secret values back "
    "to the user."
)

# ── Tool implementation ───────────────────────────────────────────────────────

_PLACEHOLDER_PREFIX = "${{"


def _read_env_var(name: str, kind: str) -> dict:
    """Inspect an env var and return a kind-appropriate payload."""
    raw = os.environ.get(name)

    if raw is None:
        return {"name": name, "kind": kind, "status": "NOT_SET"}

    if raw == "":
        return {"name": name, "kind": kind, "status": "EMPTY", "length": 0}

    if raw.startswith(_PLACEHOLDER_PREFIX):
        # The platform's secret resolver did not run or failed.
        return {
            "name": name,
            "kind": kind,
            "status": "UNRESOLVED_PLACEHOLDER",
            "length": len(raw),
            "placeholder": raw,
        }

    if kind in ("metadata", "target"):
        # Plain, non-secret values — safe to return verbatim.
        return {
            "name": name,
            "kind": kind,
            "status": "RESOLVED",
            "length": len(raw),
            "value": raw,
        }

    # kind == "credentials" — secret. Return a fingerprint only.
    head = raw[:4]
    return {
        "name": name,
        "kind": kind,
        "status": "RESOLVED",
        "length": len(raw),
        "head": head,
    }


def _execute_tool_call(function_name: str, arguments: str) -> str:
    """Execute a tool call and return the result as JSON."""
    try:
        args = json.loads(arguments)
    except json.JSONDecodeError as e:
        return json.dumps({"error": f"Invalid tool arguments: {e}"})

    if function_name == "get_env_var":
        name = args.get("name")
        if not name:
            return json.dumps({"error": "Missing required 'name' argument"})
        kind = args.get("kind") or "credentials"
        if kind not in ("metadata", "target", "credentials"):
            return json.dumps(
                {"error": f"Invalid 'kind': {kind!r} (expected metadata|target|credentials)"}
            )
        return json.dumps(_read_env_var(name, kind))

    return json.dumps({"error": f"Unknown function: {function_name}"})


# ── Agent server ──────────────────────────────────────────────────────────────

app = ResponsesAgentServerHost()

MAX_TOOL_ROUNDS = 5


@app.response_handler
async def handle_create(
    request: CreateResponse,
    context: ResponseContext,
    cancellation_signal: asyncio.Event,
):
    """Handle env-var inspection requests with the Responses API + function calling."""
    stream = ResponseEventStream(
        response_id=context.response_id,
        request=request,
    )

    yield stream.emit_created()
    yield stream.emit_in_progress()

    user_input = await context.get_input_text() or ""

    # Emit output item structure before streaming content
    message_item = stream.add_output_item_message()
    yield message_item.emit_added()

    text_content = message_item.add_text_content()
    yield text_content.emit_added()

    full_text = ""

    try:
        loop = asyncio.get_event_loop()
        next_input = user_input

        for _ in range(MAX_TOOL_ROUNDS):
            response = await loop.run_in_executor(
                None,
                lambda inp=next_input: _openai_client.responses.create(
                    model=AZURE_AI_MODEL_DEPLOYMENT_NAME,
                    instructions=SYSTEM_PROMPT,
                    input=inp,
                    tools=TOOLS,
                ),
            )

            function_calls = [
                item for item in response.output if item.type == "function_call"
            ]

            if not function_calls:
                # No more tool calls — stream the final response with the same input.
                openai_stream = await loop.run_in_executor(
                    None,
                    lambda inp=next_input: _openai_client.responses.create(
                        model=AZURE_AI_MODEL_DEPLOYMENT_NAME,
                        instructions=SYSTEM_PROMPT,
                        input=inp,
                        stream=True,
                    ),
                )
                for event in openai_stream:
                    if cancellation_signal.is_set():
                        yield stream.emit_incomplete("cancelled")
                        return
                    if event.type == "response.output_text.delta":
                        full_text += event.delta
                        yield text_content.emit_delta(event.delta)
                break

            # Execute tool calls and build follow-up input for the next round.
            follow_up_input = []
            if isinstance(next_input, list):
                follow_up_input.extend(next_input)
            else:
                follow_up_input.append({"role": "user", "content": next_input})

            for fc in function_calls:
                follow_up_input.append(fc)
                result = _execute_tool_call(fc.name, fc.arguments)
                follow_up_input.append({
                    "type": "function_call_output",
                    "call_id": fc.call_id,
                    "output": result,
                })

            next_input = follow_up_input

    except Exception as e:
        if not full_text:
            full_text = f"Error calling Azure OpenAI: {e}"
            yield text_content.emit_delta(full_text)

    yield text_content.emit_text_done()
    yield text_content.emit_done()
    yield message_item.emit_done()

    yield stream.emit_completed()


if __name__ == "__main__":
    app.run()
