# Copyright (c) Microsoft. All rights reserved.

"""Single-turn evaluation with built-in evaluators.

DESCRIPTION:
    Runs an OpenAI eval against the deployed hosted agent using a few of the
    built-in Azure AI evaluators — task adherence, fluency, and relevance —
    over a small inline dataset. This is the canonical single-turn baseline
    you can copy into any project: change the dataset, change the evaluators,
    and you have a working eval-run.

USAGE:
    python evaluate_basic.py

    Prerequisites: see README.md.
"""

# <imports_and_includes>
import time
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
# </imports_and_includes>


# <configure_evaluation>
def build_testing_criteria(model: str, response_source: str = "sample") -> list[dict]:
    """Built-in single-turn evaluators applied to ``{query, response}`` pairs.

    ``response_source`` controls where the response comes from:

    * ``"sample"`` (default) — ``{{sample.output_text}}``. Use this when the
      eval run drives the deployed agent via
      ``azure_ai_target_completions`` (``evaluate_basic.py`` does this).
    * ``"item"`` — ``{{item.response}}``. Use this when the dataset already
      contains the response column (e.g. trace-generated rows, or
      synthetic dataset-only mode).

    Swap any of these for other ``builtin.*`` evaluator names (intent
    resolution, tool call accuracy, retrieval, …) by editing this list.
    """
    response_expr = "{{item.response}}" if response_source == "item" else "{{sample.output_text}}"
    mapping = {"query": "{{item.query}}", "response": response_expr}
    return [
        {
            "type": "azure_ai_evaluator",
            "name": "task_adherence",
            "evaluator_name": "builtin.task_adherence",
            "initialization_parameters": {"deployment_name": model},
            "data_mapping": dict(mapping),
        },
        {
            "type": "azure_ai_evaluator",
            "name": "fluency",
            "evaluator_name": "builtin.fluency",
            "initialization_parameters": {"deployment_name": model},
            "data_mapping": dict(mapping),
        },
        {
            "type": "azure_ai_evaluator",
            "name": "relevance",
            "evaluator_name": "builtin.relevance",
            "initialization_parameters": {"deployment_name": model},
            "data_mapping": dict(mapping),
        },
    ]
# </configure_evaluation>


# <run_eval>
def main() -> None:
    load_dotenv()
    print(f"Using API version: {API_VERSION}")
    print(f"Project: {project_endpoint()}")
    print(f"Target agent: {target_agent()}\n")

    model = model_deployment_name()

    with (
        DefaultAzureCredential() as credential,
        AIProjectClient(endpoint=project_endpoint(), credential=credential) as project_client,
        project_client.get_openai_client() as openai_client,
    ):
        eval_object = openai_client.evals.create(
            name="hosted-agent-single-turn",
            data_source_config={
                "type": "custom",
                "item_schema": {
                    "type": "object",
                    "properties": {"query": {"type": "string"}},
                    "required": ["query"],
                },
                "include_sample_schema": True,
            },
            testing_criteria=build_testing_criteria(model),
        )
        print(f"Eval created: {eval_object.id}")

        run: Union[RunCreateResponse, RunRetrieveResponse] = openai_client.evals.runs.create(
            eval_id=eval_object.id,
            name=f"single-turn-{target_agent()['name']}",
            data_source={
                "type": "azure_ai_target_completions",
                "source": {
                    "type": "file_content",
                    "content": [
                        {"item": {"query": "What's the capital of France?"}},
                        {"item": {"query": "Briefly explain what a hosted agent is."}},
                        {"item": {"query": "What's 17 times 23?"}},
                        {"item": {"query": "Recommend a single book about distributed systems."}},
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
# </run_eval>


if __name__ == "__main__":
    main()
