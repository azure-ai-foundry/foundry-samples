# Copyright (c) Microsoft. All rights reserved.

"""Generate an evaluation dataset from existing agent traces, then evaluate.

DESCRIPTION:
    Submits a Foundry data-generation job that materializes recent
    Application-Insights traces emitted by your deployed hosted agent into a
    structured evaluation dataset (rows of ``{query, response, ...}``). When
    the LRO completes, the generated dataset is referenced as
    ``source.type = "azure_ai_dataset"`` in a follow-up eval run scored with
    the built-in single-turn evaluators.

    NOTE: This script grades the ``response`` rows that came out of your
    traces — i.e. answers your agent already gave. That's exactly the right
    pattern for "how is my agent performing on real traffic?" If you want
    to re-run the *same questions* through the agent's *current* version
    (e.g. after a prompt change), wrap the data source with
    ``azure_ai_target_completions`` the same way ``evaluate_basic.py`` does.

    Use this when you want to evaluate against *real production traffic*
    without hand-curating a dataset.

USAGE:
    python generate_dataset_from_traces.py

    Prerequisites:
      * Deployed hosted agent with tracing enabled (this sample turns it on
        by default).
      * Some recent traffic in the trace window so traces exist.
      * Service requires ``max_samples >= 15`` (data-generation API minimum).
"""

# <imports_and_includes>
import os
import time
from datetime import datetime, timedelta, timezone
from pprint import pprint

from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential
from dotenv import load_dotenv

from eval_common import (
    API_VERSION,
    model_deployment_name,
    poll_lro,
    print_friendly_output,
    project_endpoint,
    rest_get,
    rest_post,
    target_agent,
)
from evaluate_basic import build_testing_criteria
# </imports_and_includes>


# <submit_datagen_job>
def submit_traces_datagen_job(dataset_name: str) -> dict:
    """Submit a data-generation job of type ``traces`` / scenario ``evaluation``.

    The body is wrapped in ``inputs`` per the DataGenerationJobInputs TypeSpec.
    ``start_time`` is **epoch seconds** (10-digit integer); the service
    rejects milliseconds or ISO strings. ``end_time`` is omitted, so it
    defaults to "now".
    """
    agent = target_agent()
    start_time = int((datetime.now(timezone.utc) - timedelta(days=7)).timestamp())
    body = {
        "inputs": {
            "name": f"datagen-{dataset_name}",
            "scenario": "evaluation",
            "options": {
                "type": "traces",
                "max_samples": max(int(os.environ.get("EVAL_MAX_SAMPLES", "15")), 15),
            },
            "output_options": {"name": dataset_name},
            "sources": [
                {
                    "type": "traces",
                    "agent_name": agent["name"],
                    "start_time": start_time,
                }
            ],
        }
    }
    print(f"Submitting data-generation job for dataset '{dataset_name}' …")
    return rest_post("/data_generation_jobs", body)
# </submit_datagen_job>


# <eval_against_dataset>
def eval_against_dataset(dataset_name: str, dataset_version: str) -> None:
    model = model_deployment_name()
    with (
        DefaultAzureCredential() as credential,
        AIProjectClient(endpoint=project_endpoint(), credential=credential) as project_client,
        project_client.get_openai_client() as openai_client,
    ):
        eval_object = openai_client.evals.create(
            name="generated-traces-dataset-eval",
            data_source_config={
                "type": "custom",
                "item_schema": {
                    "type": "object",
                    "properties": {
                        "query": {"type": "string"},
                        "response": {"type": "string"},
                    },
                    "required": ["query"],
                },
                "include_sample_schema": True,
            },
            testing_criteria=build_testing_criteria(model, response_source="item"),
        )
        print(f"Eval created: {eval_object.id}")

        run = openai_client.evals.runs.create(
            eval_id=eval_object.id,
            name="run-against-generated-traces-dataset",
            data_source={
                "type": "jsonl",
                "source": {
                    "type": "azure_ai_dataset",
                    "name": dataset_name,
                    "version": dataset_version,
                },
            },
        )
        print(f"Eval run created: {run.id}")

        while run.status not in {"completed", "failed", "canceled"}:
            time.sleep(5)
            run = openai_client.evals.runs.retrieve(run_id=run.id, eval_id=eval_object.id)
            print(f"  status: {run.status}")
        print(f"\nFinal status: {run.status}; report: {run.report_url}")
        pprint(run.result_counts)
        if run.status == "completed":
            items = list(
                openai_client.evals.runs.output_items.list(run_id=run.id, eval_id=eval_object.id)
            )
            print_friendly_output(items, limit=3)
# </eval_against_dataset>


def main() -> None:
    load_dotenv()
    print(f"Using API version: {API_VERSION}")
    print(f"Project: {project_endpoint()}")
    print(f"Target agent: {target_agent()}\n")

    dataset_name = os.environ.get("EVAL_DATASET_NAME", "hosted-agent-traces-eval")
    job = submit_traces_datagen_job(dataset_name)
    job = poll_lro(
        lambda: rest_get(f"/data_generation_jobs/{job['id']}"),
        description=f"datagen/{job['id']}",
        max_seconds=900.0,
    )

    if job.get("status") != "succeeded":
        raise RuntimeError(f"Data-generation job did not succeed: {job}")

    outputs = job.get("result", {}).get("outputs", [])
    if not outputs:
        raise RuntimeError(f"Generated dataset output missing from job result: {job}")
    generated = outputs[0]
    dataset_name_out = generated.get("name", dataset_name)
    dataset_version_out = str(generated.get("version", "1"))
    print(f"\nGenerated dataset: {dataset_name_out}:{dataset_version_out}")

    eval_against_dataset(dataset_name_out, dataset_version_out)


if __name__ == "__main__":
    main()
