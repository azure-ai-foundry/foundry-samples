# Copyright (c) Microsoft. All rights reserved.

"""Getting-started: Claude Agent SDK with Foundry auth and invocations protocol."""

import os
import json
from dataclasses import asdict
from urllib.parse import urlparse

from starlette.requests import Request
from starlette.responses import PlainTextResponse, Response, StreamingResponse

from azure.ai.agentserver.invocations import InvocationAgentServerHost
from claude_agent_sdk import ClaudeAgentOptions, query

project_endpoint = os.environ["FOUNDRY_PROJECT_ENDPOINT"]
resource_name = urlparse(project_endpoint).netloc.split(".")[0]
os.environ["ANTHROPIC_FOUNDRY_BASE_URL"] = f"https://{resource_name}.services.ai.azure.com/anthropic"

app = InvocationAgentServerHost()


@app.invoke_handler
async def handle_invoke(request: Request) -> Response:
    try:
        input_text = (await request.body()).decode("utf-8").strip()
        if not input_text:
            raise ValueError("empty request body")
    except (UnicodeDecodeError, ValueError):
        return PlainTextResponse(
            status_code=400,
            content="Request body must be a non-empty plain text string.",
        )

    async def event_generator():
        prompt = input_text
        options = ClaudeAgentOptions(
            permission_mode="dontAsk",
            model=os.environ["ANTHROPIC_MODEL"],
            include_partial_messages=True,
            system_prompt=(
                "You are a helpful coding assistant running in a hosted invocations endpoint. "
                "Prefer concise, actionable responses."
            ),
        )

        try:
            async for message in query(prompt=prompt, options=options):
                yield f"data: {json.dumps(asdict(message))}\n\n".encode("utf-8")
        except Exception as ex:
            error_payload = json.dumps({"error": str(ex)})
            yield f"data: {error_payload}\n\n".encode("utf-8")
            return

    return StreamingResponse(
        event_generator(),
        media_type="text/event-stream",
        headers={
            "Cache-Control": "no-cache",
            "Connection": "keep-alive",
            "X-Accel-Buffering": "no",
        },
    )


if __name__ == "__main__":
    app.run()
