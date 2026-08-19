"""Weather agent runtime instrumented with the Microsoft OpenTelemetry distro.

This runtime is hosted outside Foundry. Foundry stores the external-agent
registration and reads the spans emitted by this process.

Reference for the LangChain + distro setup:
https://github.com/microsoft/opentelemetry-distro-python/blob/main/samples/langchain/sample_langchain_instrumentation.py

The Microsoft distro is configured with an explicit LangChain
``agent_id`` so emitted spans line up with the Foundry external-agent
registration.
"""

from __future__ import annotations

import os
from contextlib import asynccontextmanager
from pathlib import Path

from dotenv import load_dotenv

load_dotenv(Path(__file__).with_name(".env"), override=True)
os.environ.setdefault("AZURE_EXPERIMENTAL_ENABLE_GENAI_TRACING", "true")
os.environ.setdefault("OTEL_SEMCONV_STABILITY_OPT_IN", "gen_ai_latest_experimental")
os.environ.setdefault("OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT", "SPAN_AND_EVENT")

# Configure OpenTelemetry before importing instrumented libraries.
from microsoft.opentelemetry import use_microsoft_opentelemetry  # type: ignore

AGENT_NAME = os.environ.get("AGENT_NAME", "weather-agent")
AGENT_ID = f"{AGENT_NAME}-v1"

use_microsoft_opentelemetry(
    enable_azure_monitor=True,
    azure_monitor_connection_string=os.environ["APPLICATIONINSIGHTS_CONNECTION_STRING"],
    sampling_ratio=1.0,
    instrumentation_options={
        "fastapi": {"enabled": False},
        "langchain": {
            "enabled": True,
            "agent_id": AGENT_ID,
            "agent_name": AGENT_NAME,
        },
    },
)

from fastapi import FastAPI, Request
from langchain.agents import create_agent
from langchain_core.messages import HumanMessage, SystemMessage
from langchain_core.tools import tool
from langchain_openai import AzureChatOpenAI
from opentelemetry import context as otel_context
from opentelemetry import trace
from opentelemetry.trace.propagation.tracecontext import TraceContextTextMapPropagator
from pydantic import BaseModel, SecretStr


@tool
def get_current_weather(city: str) -> str:
    """Return the current weather for the given city.

    This is a stub that returns deterministic fake data so the sample is
    runnable without a third-party weather API key.
    """
    fake = {
        "seattle": "59F and raining",
        "new york": "72F and partly cloudy",
        "tokyo": "68F and clear",
        "london": "55F and overcast",
    }
    return fake.get(city.lower(), f"70F and sunny in {city}")


@tool
def get_forecast(city: str, days: int = 3) -> str:
    """Return a short multi-day forecast for the given city."""
    return f"{days}-day forecast for {city}: mild temperatures, occasional showers."


def build_agent():
    llm = AzureChatOpenAI(
        azure_endpoint=os.environ["AZURE_OPENAI_ENDPOINT"],
        azure_deployment=os.environ["AZURE_OPENAI_DEPLOYMENT"],
        api_version=os.environ.get("AZURE_OPENAI_API_VERSION", "2024-10-21"),
        api_key=SecretStr(os.environ["AZURE_OPENAI_API_KEY"]),
    )
    return create_agent(
        model=llm,
        tools=[get_current_weather, get_forecast],
        system_prompt=SystemMessage(
            content=(
                "You are a helpful weather assistant. Use the provided "
                "tools to answer questions about current weather and "
                "short-term forecasts. Be concise."
            )
        ),
    )


class AskRequest(BaseModel):
    question: str


class ChatRequest(BaseModel):
    thread_id: str
    question: str


@asynccontextmanager
async def lifespan(app: FastAPI):
    app.state.agent = build_agent()
    app.state.threads = {}
    yield


app = FastAPI(title=AGENT_NAME, lifespan=lifespan)


@app.get("/healthz")
def healthz():
    return {"status": "ok", "agent": AGENT_NAME}


@app.post("/ask")
def ask(req: AskRequest):
    agent = app.state.agent
    result = agent.invoke({"messages": [HumanMessage(content=req.question)]})
    final = result["messages"][-1].content
    return {"agent": AGENT_NAME, "answer": final}


@app.post("/chat")
def chat(req: ChatRequest, request: Request):
    """Multi-turn endpoint.

    Turns are grouped into a single distributed trace via the W3C
    ``traceparent`` header supplied by the caller, so every turn of a
    conversation shares one ``trace_id``. The thread id is additionally
    surfaced on spans as ``gen_ai.conversation.id`` through the LangChain
    run metadata the Microsoft distro reads.
    """
    agent = app.state.agent
    history = app.state.threads.setdefault(req.thread_id, [])
    # Build this turn's input without mutating stored history yet, so a failed
    # invocation doesn't leave an unanswered user message in the thread.
    messages = list(history) + [HumanMessage(content=req.question)]

    # Continue the caller's trace when a traceparent is present.
    ctx = TraceContextTextMapPropagator().extract(dict(request.headers))
    token = None
    if trace.get_current_span(ctx).get_span_context().is_valid:
        token = otel_context.attach(ctx)
    try:
        result = agent.invoke(
            {"messages": messages},
            config={"metadata": {"thread_id": req.thread_id}},
        )
    finally:
        if token is not None:
            otel_context.detach(token)

    # Commit the full turn (user + agent messages) only after a successful run.
    history[:] = result["messages"]
    final = result["messages"][-1].content
    return {"agent": AGENT_NAME, "thread_id": req.thread_id, "answer": final}


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(
        "weather_agent:app",
        host="0.0.0.0",
        port=int(os.environ.get("PORT", "8000")),
    )
