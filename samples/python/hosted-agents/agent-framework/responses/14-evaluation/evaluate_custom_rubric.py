# Copyright (c) Microsoft. All rights reserved.

"""Custom Rubric Evaluator — the default starting point for real projects.

WHAT THIS SCRIPT DOES (in plain English):
    You describe your agent in a short paragraph ("a customer-support agent
    for an outdoor-gear retailer; it should be friendly, cite return policy
    when relevant, and never invent product SKUs"). Foundry turns that
    description into a multi-dimensional **rubric** — say 5-7 named
    dimensions like *factuality*, *tone*, *policy_citation*. The rubric
    becomes a reusable evaluator that grades every agent response on every
    dimension, with a per-dimension score and a written rationale.

    The script:
      1. Submits the description to ``POST /evaluator_generation_jobs`` and
         waits for the rubric to be generated.
      2. Prints the auto-generated dimensions so you can see what Foundry
         decided "good" looks like.
      3. (Optional) If you set ``EVAL_RUBRIC_REGENERATE=true``, it bumps the
         weight of one dimension and re-generates a refined version — the
         human-in-the-loop (HITL) workflow.
      4. Runs an eval against the deployed hosted agent using the saved
         rubric as the testing criterion, and polls it to completion.

WHY THIS BEATS THE BUILT-IN EVALUATORS:
    Built-ins like ``builtin.fluency`` ask generic questions ("is the
    response grammatically correct?"). A Custom Rubric evaluator asks
    questions tailored to *your* agent's job — that's the difference
    between knowing your agent reads well and knowing your agent does
    what it's supposed to do.

USAGE:
    # ▼ CHANGE THE PROMPT IN submit_generation_job() FIRST — see below.
    python evaluate_custom_rubric.py

    # Optional: also run the HITL "edit + regenerate" flow.
    EVAL_RUBRIC_REGENERATE=true python evaluate_custom_rubric.py

    Prerequisites (see README.md for the full list):
      * pip install -r requirements.txt
      * az login
      * FOUNDRY_PROJECT_ENDPOINT, AZURE_AI_MODEL_DEPLOYMENT_NAME set
      * EVAL_AGENT_NAME / EVAL_AGENT_VERSION set to your deployed agent
        (`azd up` registers the agent under the name in
        agent.manifest.yaml).
"""

# <imports_and_includes>
import os
import time
from typing import Any

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
# </imports_and_includes>


# <generate_rubric>
def submit_generation_job(evaluator_name: str, model: str) -> dict[str, Any]:
    """Submit an evaluator generation job using a ``Prompt`` source.

    A Prompt source is the simplest seed — just describe the agent in plain
    English. The service runs a multi-stage LLM pipeline to derive 5-7 rubric
    dimensions tailored to that description.
    """
    # ┌─────────────────────────────────────────────────────────────────────┐
    # │  ▶ CHANGE THIS FIRST: describe what YOUR agent is supposed to do.   │
    # │                                                                     │
    # │  A good prompt names the agent's task, who it serves, the success   │
    # │  criteria, the expected tone, any required citations or sources,    │
    # │  and the common failure modes you want the rubric to penalize.      │
    # │                                                                     │
    # │  The generic prompt below produces a generic rubric — copy-pasting  │
    # │  this for your own agent will give you generic scores.              │
    # └─────────────────────────────────────────────────────────────────────┘
    agent_description = (
        "A friendly general-purpose assistant deployed on Azure AI Foundry "
        "that answers user questions briefly and factually. It should keep "
        "responses concise, admit when it does not know an answer, and avoid "
        "making up facts."
    )
    print("\n--- Generating rubric from this description: ---")
    print(agent_description)
    print("--- (edit submit_generation_job() in this file to change it) ---\n")

    body = {
        "name": evaluator_name,
        "category": "quality",
        "model": model,
        "sources": [{"type": "prompt", "prompt": agent_description}],
    }
    print(f"Submitting evaluator generation job for '{evaluator_name}' …")
    return rest_post("/evaluator_generation_jobs", body)


def wait_for_generation(job_id: str) -> dict[str, Any]:
    return poll_lro(
        lambda: rest_get(f"/evaluator_generation_jobs/{job_id}"),
        description=f"generation/{job_id}",
    )
# </generate_rubric>


# <inspect_dimensions>
def inspect_dimensions(evaluator_name: str, version: int | str) -> dict[str, Any]:
    evaluator = rest_get(f"/evaluators/{evaluator_name}/versions/{version}")
    dims = evaluator.get("dimensions") or evaluator.get("rubric", {}).get("dimensions", [])
    print(f"\nEvaluator '{evaluator_name}' v{version} has {len(dims)} dimensions:")
    for dim in dims:
        weight = dim.get("weight", "?")
        always = " (always_applicable)" if dim.get("always_applicable") else ""
        print(f"  - {dim.get('id'):30s} weight={weight}{always}")
        if dim.get("description"):
            print(f"      {dim['description']}")
    return evaluator


def edit_and_regenerate(evaluator_name: str, base_version: int | str, model: str) -> dict[str, Any]:
    """HITL flow: bump the weight of the first non-``general_quality`` dimension
    to 9, save as a new version, then submit a regeneration job that uses the
    new version as additional context.
    """
    current = rest_get(f"/evaluators/{evaluator_name}/versions/{base_version}")
    dims = list(current.get("dimensions") or current.get("rubric", {}).get("dimensions", []))
    if not dims:
        print("No dimensions to edit; skipping HITL step.")
        return current

    for dim in dims:
        if dim.get("id") != "general_quality":
            print(f"\nBumping '{dim['id']}' weight 9 (was {dim.get('weight')}).")
            dim["weight"] = 9
            break

    new_version = rest_post(
        f"/evaluators/{evaluator_name}/versions",
        {"dimensions": dims, "category": "quality"},
    )
    saved_version = new_version.get("version") or new_version.get("id")
    print(f"Saved as new version: {saved_version}")

    print("Submitting regeneration job …")
    regen = rest_post(
        "/evaluator_generation_jobs",
        {
            "name": evaluator_name,
            "category": "quality",
            "model": model,
            "sources": [{"type": "prompt", "prompt": "Refine the existing rubric."}],
        },
    )
    return poll_lro(
        lambda: rest_get(f"/evaluator_generation_jobs/{regen['id']}"),
        description=f"regeneration/{regen['id']}",
    )
# </inspect_dimensions>


# <run_eval_with_rubric>
def run_eval_with_rubric(evaluator_name: str, evaluator_version: int | str) -> None:
    endpoint = project_endpoint()
    model = model_deployment_name()

    with (
        DefaultAzureCredential() as credential,
        AIProjectClient(endpoint=endpoint, credential=credential) as project_client,
        project_client.get_openai_client() as openai_client,
    ):
        eval_object = openai_client.evals.create(
            name=f"custom-rubric-{evaluator_name}",
            data_source_config={
                "type": "custom",
                "item_schema": {
                    "type": "object",
                    "properties": {"query": {"type": "string"}},
                    "required": ["query"],
                },
                "include_sample_schema": True,
            },
            testing_criteria=[
                {
                    "type": "azure_ai_evaluator",
                    "name": "custom_rubric",
                    "evaluator_name": f"{evaluator_name}:{evaluator_version}",
                    "initialization_parameters": {"deployment_name": model},
                    "data_mapping": {
                        "query": "{{item.query}}",
                        "response": "{{sample.output_text}}",
                    },
                }
            ],
        )
        print(f"Eval created: {eval_object.id}")

        run = openai_client.evals.runs.create(
            eval_id=eval_object.id,
            name=f"run-against-{target_agent()['name']}",
            data_source={
                "type": "azure_ai_target_completions",
                # ┌─────────────────────────────────────────────────────────┐
                # │  ▶ CHANGE THIS TOO: use questions that match the rubric │
                # │     prompt you wrote in submit_generation_job().        │
                # │                                                         │
                # │  A rubric tailored to your domain scored against generic│
                # │  trivia (France, arithmetic, …) gives confusing results.│
                # │  Use questions a real user would ask your agent.        │
                # └─────────────────────────────────────────────────────────┘
                "source": {
                    "type": "file_content",
                    "content": [
                        {"item": {"query": "What's the capital of France?"}},
                        {"item": {"query": "Briefly explain what a hosted agent is."}},
                        {"item": {"query": "What's 17 times 23?"}},
                    ],
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
                "target": target_agent(),
            },
        )
        print(f"Eval run created: {run.id}")
        print("Polling — this typically takes 30–120 seconds.")

        while run.status not in {"completed", "failed", "canceled"}:
            time.sleep(5)
            run = openai_client.evals.runs.retrieve(run_id=run.id, eval_id=eval_object.id)
            print(f"  status: {run.status}")

        if run.status == "completed":
            print("\n✓ Eval run completed.")
            print(f"Result counts: {run.result_counts}")
            print(f"Report URL: {run.report_url}")
            output_items = list(
                openai_client.evals.runs.output_items.list(run_id=run.id, eval_id=eval_object.id)
            )
            print_friendly_output(output_items, limit=3)
        else:
            print(f"\n✗ Eval run did not complete: status={run.status}")
# </run_eval_with_rubric>


def main() -> None:
    load_dotenv()
    print(f"Using API version: {API_VERSION}")
    print(f"Project: {project_endpoint()}")
    print(f"Target agent: {target_agent()}\n")

    evaluator_name = os.environ.get("EVAL_RUBRIC_NAME", "hosted-agent-rubric")
    model = model_deployment_name()

    job = submit_generation_job(evaluator_name, model)
    job = wait_for_generation(job["id"])
    if job.get("status") != "succeeded":
        raise RuntimeError(f"Generation job did not succeed: {job}")

    result = job.get("result") or {}
    version = result.get("version") or 1
    inspect_dimensions(evaluator_name, version)

    if os.environ.get("EVAL_RUBRIC_REGENERATE", "").lower() in {"1", "true", "yes"}:
        regen = edit_and_regenerate(evaluator_name, version, model)
        version = (regen.get("result") or {}).get("version") or version
        inspect_dimensions(evaluator_name, version)

    run_eval_with_rubric(evaluator_name, version)


if __name__ == "__main__":
    main()
