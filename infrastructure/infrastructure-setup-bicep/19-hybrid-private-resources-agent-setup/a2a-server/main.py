"""
Minimal A2A (Agent-to-Agent) Protocol Server

A lightweight implementation of the A2A protocol specification (https://a2a-protocol.org)
for testing Azure AI Foundry's RemoteA2AConnector through the Data Proxy.

Endpoints:
  GET  /.well-known/agent.json       - Agent card (A2A spec standard)
  GET  /.well-known/agent-card.json   - Agent card (Azure SDK default path)
  POST /                              - JSON-RPC 2.0 task endpoint
  POST /a2a                           - JSON-RPC 2.0 task endpoint (DataProxy-compatible path)
  GET  /healthz                       - Container health check
"""

import json
import logging
import os
import uuid

from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = FastAPI(title="A2A Calculator Agent")

# ============================================================================
# Agent Card — describes this agent's identity and capabilities
# ============================================================================

AGENT_CARD = {
    "name": "Calculator Agent",
    "description": "A simple calculator agent for A2A protocol testing. "
    "Performs basic arithmetic (add, subtract, multiply, divide).",
    "url": "/",
    "version": "1.0.0",
    "protocolVersion": "0.2.6",
    "preferredTransport": "jsonrpc",
    "capabilities": {
        "streaming": False,
        "pushNotifications": False,
    },
    "skills": [
        {
            "id": "calculate",
            "name": "Calculate",
            "description": "Performs basic arithmetic operations (add, subtract, multiply, divide)",
            "tags": ["math", "calculator", "arithmetic"],
            "examples": ["add 5 and 3", "multiply 7 by 8"],
        }
    ],
    "defaultInputModes": ["text"],
    "defaultOutputModes": ["text"],
}


# ============================================================================
# Agent Card Endpoints
# ============================================================================


@app.get("/.well-known/agent.json")
@app.get("/.well-known/agent-card.json")
async def get_agent_card(request: Request):
    """Serves agent card at both A2A spec and Azure SDK default paths.
    Dynamically sets the url field to the absolute URL of this server."""
    logger.info("Agent card requested")
    # Build absolute URL from the incoming request
    base_url = os.environ.get("A2A_BASE_URL", "")
    if not base_url:
        scheme = request.headers.get("x-forwarded-proto", request.url.scheme)
        host = request.headers.get("x-forwarded-host", request.headers.get("host", ""))
        base_url = f"{scheme}://{host}"
    card = dict(AGENT_CARD)
    # Use /a2a path instead of root "/" to ensure compatibility with the
    # Foundry DataProxy, which requires a non-empty path after the hostname
    # in its /v1/https/{serviceName}/{remainder} route.
    card["url"] = base_url + "/a2a"
    return card


# ============================================================================
# JSON-RPC 2.0 Task Endpoint
# ============================================================================


def _jsonrpc_error(request_id: str, code: int, message: str, status: int = 200):
    return JSONResponse(
        status_code=status,
        content={
            "jsonrpc": "2.0",
            "id": request_id,
            "error": {"code": code, "message": message},
        },
    )


@app.get("/")
async def root_get():
    """GET on root — return basic info (some A2A clients probe this)."""
    return {"name": "Calculator Agent", "protocol": "a2a", "version": "1.0.0"}


def _extract_user_text(params: dict) -> str:
    """Extract text content from an A2A message's parts."""
    message = params.get("message", {})
    parts = message.get("parts", [])
    for part in parts:
        if part.get("kind") == "text" or part.get("type") == "text":
            return part.get("text", "")
    return ""


def _process_message(text: str) -> str:
    """Simple calculator logic — returns a text response."""
    lower = text.lower().strip()

    # Try to detect arithmetic from natural language
    import re

    # Match patterns like "add 5 and 3", "multiply 4 by 7", "15 + 3"
    ops = {
        "add": "+",
        "plus": "+",
        "sum": "+",
        "subtract": "-",
        "minus": "-",
        "multiply": "*",
        "times": "*",
        "divide": "/",
        "divided": "/",
    }

    # Check for "X op Y" patterns
    num_pattern = r"(-?\d+(?:\.\d+)?)"
    for word, op in ops.items():
        pattern = rf"{word}\s+{num_pattern}\s+(?:and|by|from|with)?\s*{num_pattern}"
        match = re.search(pattern, lower)
        if match:
            a, b = float(match.group(1)), float(match.group(2))
            return _compute(op, a, b)

    # Check for "X + Y" style
    arith_match = re.search(
        rf"{num_pattern}\s*([+\-*/])\s*{num_pattern}", text
    )
    if arith_match:
        a = float(arith_match.group(1))
        op = arith_match.group(2)
        b = float(arith_match.group(3))
        return _compute(op, a, b)

    # Capability question
    if "what" in lower and ("do" in lower or "can" in lower or "capabilities" in lower):
        return (
            "I'm a calculator agent. I can perform basic arithmetic: "
            "add, subtract, multiply, and divide. "
            "Try asking me something like 'add 5 and 3' or 'multiply 7 by 8'."
        )

    return f"I received your message: '{text}'. I'm a calculator agent — ask me to do math!"


def _compute(op: str, a: float, b: float) -> str:
    if op == "+":
        return f"{a} + {b} = {a + b}"
    elif op == "-":
        return f"{a} - {b} = {a - b}"
    elif op == "*":
        return f"{a} * {b} = {a * b}"
    elif op == "/":
        if b == 0:
            return "Error: Division by zero"
        return f"{a} / {b} = {a / b}"
    return "Unknown operation"


@app.post("/")
@app.post("/a2a")
async def handle_jsonrpc(request: Request):
    """Handle A2A JSON-RPC 2.0 requests."""
    try:
        body = await request.json()
    except json.JSONDecodeError:
        return _jsonrpc_error("unknown", -32700, "Parse error", status=400)

    request_id = body.get("id", str(uuid.uuid4()))

    if body.get("jsonrpc") != "2.0":
        return _jsonrpc_error(request_id, -32600, "Invalid Request: jsonrpc must be '2.0'")

    method = body.get("method", "")
    # Support both A2A v1.0 (message/send) and older (tasks/send)
    supported_methods = {"message/send", "tasks/send", "message/stream"}

    if method not in supported_methods:
        return _jsonrpc_error(request_id, -32601, f"Method not found: {method}")

    params = body.get("params", {})
    task_id = params.get("id", str(uuid.uuid4()))
    user_text = _extract_user_text(params)

    logger.info(f"A2A {method}: task={task_id}, text='{user_text[:100]}'")

    response_text = _process_message(user_text)

    # Build A2A response as a direct Message (with kind discriminator)
    # The A2A SDK uses "kind" to distinguish Message vs Task responses
    result = {
        "kind": "message",
        "messageId": str(uuid.uuid4()),
        "role": "agent",
        "parts": [{"kind": "text", "text": response_text}],
    }

    return JSONResponse(
        content={"jsonrpc": "2.0", "id": request_id, "result": result}
    )


# ============================================================================
# Health Check
# ============================================================================


@app.get("/healthz")
async def healthz():
    return {"status": "ok"}


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="0.0.0.0", port=8080)
