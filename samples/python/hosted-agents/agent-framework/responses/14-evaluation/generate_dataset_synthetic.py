# Copyright (c) Microsoft. All rights reserved.

"""Generate an evaluation dataset from synthetic seeds, then evaluate.

DESCRIPTION:
    Submits a Foundry data-generation job that synthesizes Q&A rows from a
    set of prompt seeds (``data/synthetic-seeds.jsonl``). The job is
    ``options.type = "simple_qna"`` with ``scenario = "evaluation"``;
    each seed becomes a ``sources[].type = "prompt"`` entry. When the LRO
    completes, the generated dataset is referenced by name in a follow-up
    eval run.

    By default, the eval run **targets your deployed hosted agent** — each
    generated query is sent to the agent and the agent's live response is
    what gets scored. This is usually what you want: "score the agent on a
    representative set of questions for my domain."

    If you'd rather just score the *generated* answers (e.g. you're sanity-
    checking the synthetic dataset itself before using it elsewhere), set
    ``EVAL_AGAINST_DATASET_ONLY=true``. That mode skips the agent and grades
    the reference responses the generator produced.

    Use this script when you don't yet have production traffic but want a
    domain-relevant evaluation dataset to bootstrap your agent.

USAGE:
    python generate_dataset_synthetic.py

    # Score the generated reference answers instead of your agent:
    EVAL_AGAINST_DATASET_ONLY=true python generate_dataset_synthetic.py

    Prerequisites:
      * Default mode: a deployed hosted agent (same as ``evaluate_basic.py``).
      * ``EVAL_AGAINST_DATASET_ONLY=true`` mode: no deployed agent required.
      * Service requires ``max_samples >= 15`` and ``model_options.model``
        for ``simple_qna``.
"""

# <imports_and_includes>
import json
import os
import time
from pathlib import Path
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


SEEDS_FILE = Path(__file__).parent / "data" / "synthetic-seeds.jsonl"


def load_seeds() -> list[dict]:
    with SEEDS_FILE.open(encoding="utf-8") as fp:
        return [json.loads(line) for line in fp if line.strip()]


# <submit_synthetic_datagen>
def submit_synthetic_job(dataset_name: str, seeds: list[dict]) -> dict:
    """Submit a synthetic data-generation job seeded by prompt sources."""
    model = model_deployment_name()
    sources = [
        {
            "type": "prompt",
            "prompt": (
                f"Generate diverse, **self-contained single-turn** evaluation "
                f"questions about: {seed['topic']}. Each question must stand "
                "on its own — do not assume any earlier conversation context."
            ),
            "description": seed.get("id", "synthetic seed"),
        }
        for seed in seeds
    ]
    body = {
        "inputs": {
            "name": f"datagen-{dataset_name}",
            "scenario": "evaluation",
            "options": {
                "type": "simple_qna",
                "max_samples": max(int(os.environ.get("EVAL_MAX_SAMPLES", "15")), 15),
                "model_options": {"model": model},
            },
            "output_options": {
                "name": dataset_name,
                "description": "Synthetic evaluation dataset.",
                "tags": {"source": "synthetic-seeds"},
            },
            "sources": sources,
        }
    }
    print(
        f"Submitting synthetic data-generation job '{dataset_name}' "
        f"with {len(seeds)} prompt seeds …"
    )
    return rest_post("/data_generation_jobs", body)
# </submit_synthetic_datagen>


# <eval_against_dataset>
def eval_against_dataset(dataset_name: str, dataset_version: str) -> None:
    """Evaluate the generated dataset.

    Default: run each generated query through the deployed hosted agent and
    score the *agent's* live response (true end-to-end evaluation).
    Override with ``EVAL_AGAINST_DATASET_ONLY=true`` to instead score the
    reference answers the generator produced (dataset sanity-check mode).
    """
    model = model_deployment_name()
    dataset_only = os.environ.get("EVAL_AGAINST_DATASET_ONLY", "").lower() in {"1", "true", "yes"}
    with (
        DefaultAzureCredential() as credential,
        AIProjectClient(endpoint=project_endpoint(), credential=credential) as project_client,
        project_client.get_openai_client() as openai_client,
    ):
        eval_object = openai_client.evals.create(
            name="synthetic-dataset-eval",
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
            testing_criteria=build_testing_criteria(
                model, response_source=("item" if dataset_only else "sample")
            ),
        )
        print(f"Eval created: {eval_object.id}")

        if dataset_only:
            print(
                "EVAL_AGAINST_DATASET_ONLY=true — scoring the GENERATED rows, "
                "not your deployed agent."
            )
            data_source: dict = {
                "type": "jsonl",
                "source": {
                    "type": "azure_ai_dataset",
                    "name": dataset_name,
                    "version": dataset_version,
                },
            }
        else:
            agent = target_agent()
            print(f"Scoring deployed agent ({agent['name']}:{agent['version']}) against the dataset.")
            data_source = {
                "type": "azure_ai_target_completions",
                "source": {
                    "type": "azure_ai_dataset",
                    "name": dataset_name,
                    "version": dataset_version,
                },
                "input_messages": {
                    "type": "template",
                    "template": [
                        {
                            "type": "message",
                            "role": "user",
                            "content": {"type": "input_text", "text": "{{item.query}}"},
                        }
                    ],
                },
                "target": agent,
            }

        run = openai_client.evals.runs.create(
            eval_id=eval_object.id,
            name="run-against-synthetic-dataset",
            data_source=data_source,
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
    print(f"Project: {project_endpoint()}\n")

    dataset_name = os.environ.get("EVAL_DATASET_NAME", "hosted-agent-synthetic-eval")
    seeds = load_seeds()
    print(f"Loaded {len(seeds)} seeds from {SEEDS_FILE.name}")

    job = submit_synthetic_job(dataset_name, seeds)
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
