# Copyright (c) Microsoft. All rights reserved.

"""Multi-turn evaluation over existing agent traces.

DESCRIPTION:
    Companion to ``main.py``. Evaluates real multi-turn conversations that
    already exist as Application-Insights traces emitted by the deployed
    ``01-basic`` agent. Three trace-source variants are supported:

      * ``agent_filter`` (default) — recent traces for the agent.
      * ``conversation_id_source`` — pass ``EVAL_CONVERSATION_IDS`` as a
        comma-separated list.
      * ``trace_id_source`` — pass ``EVAL_TRACE_IDS`` as a comma-separated
        list.

    Scored with the same 4 built-in conversation-level evaluators as
    ``evaluate_multiturn_simulation.py``.

    For the full evaluation learning path see
    [``14-evaluation/``](../14-evaluation/).

USAGE:
    python evaluate_multiturn_traces.py

    Prerequisites:
      * Deployed ``01-basic`` agent.
      * **Tracing must be enabled on the deployment.** ``01-basic`` does not
        enable it by default — copy ``ENABLE_INSTRUMENTATION=true`` and
        ``ENABLE_SENSITIVE_DATA=true`` from
        [``../08-observability/agent.yaml``](../08-observability/agent.yaml)
        onto your deployment, or fall back to
        ``evaluate_multiturn_simulation.py`` (no traces required).
"""

import os
import time
from datetime import datetime, timedelta, timezone
from pprint import pprint
from typing import Union

from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential
from dotenv import load_dotenv
from openai.types.evals.run_create_response import RunCreateResponse
from openai.types.evals.run_retrieve_response import RunRetrieveResponse

from evaluate_multiturn_simulation import (
    API_VERSION,
    build_conversation_evaluators,
    target_agent,
)


def _env(name: str, default: str | None = None) -> str:
    value = os.environ.get(name, default)
    if not value:
        raise RuntimeError(f"{name} is not set (see .env.example).")
    return value


def pick_trace_source() -> dict:
    if conv_ids := os.environ.get("EVAL_CONVERSATION_IDS"):
        ids = [s.strip() for s in conv_ids.split(",") if s.strip()]
        print(f"Using conversation_id_source with {len(ids)} IDs.")
        return {"type": "conversation_id_source", "conversation_ids": ids}

    if trace_ids := os.environ.get("EVAL_TRACE_IDS"):
        ids = [s.strip() for s in trace_ids.split(",") if s.strip()]
        print(f"Using trace_id_source with {len(ids)} IDs.")
        return {"type": "trace_id_source", "trace_ids": ids}

    now = datetime.now(timezone.utc)
    end = int((now + timedelta(minutes=10)).timestamp())
    start = int((now - timedelta(hours=24)).timestamp())
    agent = target_agent()
    max_traces = int(os.environ.get("EVAL_MAX_TRACES", "5"))
    print(
        f"Using agent_filter (last 24h, max_traces={max_traces}) for agent "
        f"'{agent['name']}' v{agent['version']}."
    )
    return {
        "type": "agent_filter",
        "agent_name": agent["name"],
        "agent_version": str(agent["version"]),
        "start_time": start,
        "end_time": end,
        "max_traces": max_traces,
    }


def main() -> None:
    load_dotenv()
    endpoint = _env("FOUNDRY_PROJECT_ENDPOINT").rstrip("/")
    model = os.environ.get("AZURE_AI_MODEL_DEPLOYMENT_NAME", "gpt-4.1-mini")
    print(f"Using API version: {API_VERSION}")
    print(f"Project: {endpoint}")
    print(f"Target agent: {target_agent()}\n")

    trace_source = pick_trace_source()

    with (
        DefaultAzureCredential() as credential,
        AIProjectClient(endpoint=endpoint, credential=credential) as project_client,
        project_client.get_openai_client() as openai_client,
    ):
        eval_object = openai_client.evals.create(
            name="basic-agent-multiturn-traces",
            data_source_config={"type": "azure_ai_source", "scenario": "traces"},
            testing_criteria=build_conversation_evaluators(model),
        )
        print(f"Eval created: {eval_object.id}")

        run: Union[RunCreateResponse, RunRetrieveResponse] = openai_client.evals.runs.create(
            eval_id=eval_object.id,
            name=f"traces-{target_agent()['name']}",
            evaluation_level="conversation",
            data_source={
                "type": "azure_ai_trace_data_source_preview",
                "trace_source": trace_source,
            },
        )
        print(f"Eval run created: {run.id}")
        print("Trace-based runs typically take 1-2 minutes …")

        while run.status not in {"completed", "failed", "canceled"}:
            time.sleep(10)
            run = openai_client.evals.runs.retrieve(run_id=run.id, eval_id=eval_object.id)
            print(f"  status: {run.status}")

        print(f"\nFinal status: {run.status}")
        print(f"Result counts: {run.result_counts}")
        print(f"Report URL: {run.report_url}")
        if run.status == "completed":
            items = list(
                openai_client.evals.runs.output_items.list(run_id=run.id, eval_id=eval_object.id)
            )
            print(f"\nFirst of {len(items)} output items:")
            if items:
                pprint(items[0])


if __name__ == "__main__":
    main()
