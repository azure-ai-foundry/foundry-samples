# Copyright (c) Microsoft. All rights reserved.

"""Multi-turn evaluation over existing agent traces (Scenarios S2 + S3).

DESCRIPTION:
    Evaluates real multi-turn conversations that already exist as
    Application-Insights traces emitted by your deployed hosted agent. Three
    variants are supported:

      * ``agent_filter`` (default) — pull recent traces for the agent and
        evaluate the most recent ``MAX_TRACES``.
      * ``conversation_id_source`` — evaluate specific Foundry conversation
        IDs (set ``EVAL_CONVERSATION_IDS`` as a comma-separated list).
      * ``trace_id_source`` — evaluate specific W3C trace IDs
        (set ``EVAL_TRACE_IDS`` as a comma-separated list).

    The 4 built-in conversation-level evaluators are scored against the
    reconstructed message arrays.

USAGE:
    python evaluate_multiturn_traces.py

    Prerequisites:
      * Deployed hosted agent with tracing enabled (this sample turns it on
        by default — see README → "Tracing is on by default").
      * Some recent traffic so traces exist. Hit the agent once or twice
        before running this script.
"""

# <imports_and_includes>
import os
import time
from datetime import datetime, timedelta, timezone
from typing import Union

from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential
from dotenv import load_dotenv
from openai.types.evals.run_create_response import RunCreateResponse
from openai.types.evals.run_retrieve_response import RunRetrieveResponse

from eval_common import (
    API_VERSION,
    model_deployment_name,
    print_friendly_output,
    project_endpoint,
    target_agent,
)
from evaluate_multiturn_simulation import build_conversation_evaluators
# </imports_and_includes>


# <pick_trace_source>
def pick_trace_source() -> dict:
    """Choose a trace source from env vars.

    Priority:
      1. ``EVAL_CONVERSATION_IDS`` — comma-separated Foundry conversation IDs.
      2. ``EVAL_TRACE_IDS`` — comma-separated W3C trace IDs.
      3. (default) ``agent_filter`` over the last 24h.
    """
    if conv_ids := os.environ.get("EVAL_CONVERSATION_IDS"):
        ids = [s.strip() for s in conv_ids.split(",") if s.strip()]
        print(f"Using conversation_id_source with {len(ids)} IDs.")
        return {"type": "conversation_id_source", "conversation_ids": ids}

    if trace_ids := os.environ.get("EVAL_TRACE_IDS"):
        ids = [s.strip() for s in trace_ids.split(",") if s.strip()]
        print(f"Using trace_id_source with {len(ids)} IDs.")
        return {"type": "trace_id_source", "trace_ids": ids}

    # Default: agent_filter over last 24h, padded 10 min into the future to
    # avoid an ingestion-delay edge from the agent_filter pipeline.
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
# </pick_trace_source>


# <run_trace_eval>
def main() -> None:
    load_dotenv()
    print(f"Using API version: {API_VERSION}")
    print(f"Project: {project_endpoint()}")

    model = model_deployment_name()
    trace_source = pick_trace_source()

    with (
        DefaultAzureCredential() as credential,
        AIProjectClient(endpoint=project_endpoint(), credential=credential) as project_client,
        project_client.get_openai_client() as openai_client,
    ):
        eval_object = openai_client.evals.create(
            name="hosted-agent-multiturn-traces",
            data_source_config={
                "type": "azure_ai_source",
                "scenario": "traces",
            },
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
            print_friendly_output(items, limit=3)
# </run_trace_eval>


if __name__ == "__main__":
    main()
