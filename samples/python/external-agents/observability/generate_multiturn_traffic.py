"""Drive multi-turn conversations against the local weather agent.

Each conversation is wrapped in a single root span, and the W3C
``traceparent`` for that span is injected into every turn's HTTP request.
Every turn of a conversation therefore lands in the same distributed
trace, which is how Foundry groups an external agent's multi-turn
interactions.
"""

from __future__ import annotations

import os
import sys
import uuid
from pathlib import Path

from dotenv import load_dotenv

load_dotenv(Path(__file__).with_name(".env"), override=True)
os.environ.setdefault("AZURE_EXPERIMENTAL_ENABLE_GENAI_TRACING", "true")
os.environ.setdefault("OTEL_SEMCONV_STABILITY_OPT_IN", "gen_ai_latest_experimental")

from microsoft.opentelemetry import use_microsoft_opentelemetry  # type: ignore

use_microsoft_opentelemetry(
    enable_azure_monitor=True,
    azure_monitor_connection_string=os.environ["APPLICATIONINSIGHTS_CONNECTION_STRING"],
    sampling_ratio=1.0,
    instrumentation_options={"httpx": {"enabled": False}},
)

import httpx
from opentelemetry import trace
from opentelemetry.trace.propagation.tracecontext import TraceContextTextMapPropagator

AGENT_NAME = os.environ.get("AGENT_NAME", "weather-agent")
tracer = trace.get_tracer("multiturn-traffic")

CONVERSATIONS: list[list[str]] = [
    [
        "What's the weather in Seattle right now?",
        "How about Tokyo?",
        "Which of those two would be better for a picnic tomorrow?",
        "Give me the 3-day forecast for the one you recommended.",
    ],
    [
        "I'm flying to London this week, what's it like there?",
        "Should I pack an umbrella?",
        "What's the 5-day forecast?",
    ],
    [
        "Compare the current weather in New York and Seattle.",
        "Which one is warmer?",
        "Book me a flight to the warmer one.",
    ],
]


def run_conversation(base_url: str, turns: list[str]) -> None:
    thread_id = f"thread-{uuid.uuid4().hex[:12]}"
    with tracer.start_as_current_span(f"conversation {thread_id}") as root:
        root.set_attribute("gen_ai.conversation.id", thread_id)
        root.set_attribute("gen_ai.agent.name", AGENT_NAME)

        headers: dict[str, str] = {}
        TraceContextTextMapPropagator().inject(headers)
        print(f"\n=== {thread_id}  traceparent={headers.get('traceparent')}")

        with httpx.Client(timeout=120) as client:
            for turn_number, question in enumerate(turns, start=1):
                print(f"  [{turn_number}] user: {question}")
                resp = client.post(
                    f"{base_url}/chat",
                    json={"thread_id": thread_id, "question": question},
                    headers=headers,
                )
                resp.raise_for_status()
                print(f"      agent: {resp.json()['answer']}")


def main() -> None:
    base_url = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:8000"
    for turns in CONVERSATIONS:
        run_conversation(base_url, turns)
    print("\nDone. Allow a minute or two for Application Insights ingestion.")


if __name__ == "__main__":
    main()
