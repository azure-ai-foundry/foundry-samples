# Copyright (c) Microsoft. All rights reserved.

"""Multi-turn evaluation driven by simulated conversations (Scenario S4).

DESCRIPTION:
    Creates an eval group with the four built-in conversation-level evaluators
    (customer_satisfaction, groundedness, coherence, task_completion), then
    drives an eval run that *simulates* multi-turn conversations against the
    deployed hosted agent using ``azure_ai_target_completions`` with
    ``item_generation_params.type = "conversation_gen_preview"``.

    Use this when you do NOT yet have multi-turn traces / a conversation
    dataset and want the service to generate scenarios on the fly.

USAGE:
    python evaluate_multiturn_simulation.py

    Prerequisites:
      * Deployed hosted agent — `EVAL_AGENT_NAME` + `EVAL_AGENT_VERSION`.
      * Seed scenarios live in ``data/test-scenarios.jsonl`` (loaded inline).
"""

# <imports_and_includes>
import json
import time
from pathlib import Path
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


SCENARIOS_FILE = Path(__file__).parent / "data" / "test-scenarios.jsonl"


def load_scenarios() -> list[dict]:
    with SCENARIOS_FILE.open(encoding="utf-8") as fp:
        return [json.loads(line) for line in fp if line.strip()]


# <build_eval>
def build_conversation_evaluators(model: str) -> list[dict]:
    """The 4 built-in conversation-level evaluators used across the
    multi-turn flows in this sample.
    """
    common = {
        "type": "azure_ai_evaluator",
        "initialization_parameters": {"deployment_name": model},
        "data_mapping": {"messages": "{{item.messages}}"},
    }
    return [
        {**common, "name": "customer_satisfaction",
         "evaluator_name": "builtin.customer_satisfaction"},
        {**common, "name": "groundedness",
         "evaluator_name": "builtin.groundedness"},
        {**common, "name": "coherence",
         "evaluator_name": "builtin.coherence"},
        {**common, "name": "task_completion",
         "evaluator_name": "builtin.task_completion"},
    ]
# </build_eval>


# <run_simulation_eval>
def main() -> None:
    load_dotenv()
    print(f"Using API version: {API_VERSION}")
    print(f"Project: {project_endpoint()}")
    print(f"Target agent: {target_agent()}\n")

    scenarios = load_scenarios()
    print(f"Loaded {len(scenarios)} seed scenarios from {SCENARIOS_FILE.name}")

    model = model_deployment_name()
    sim_model = model

    with (
        DefaultAzureCredential() as credential,
        AIProjectClient(endpoint=project_endpoint(), credential=credential) as project_client,
        project_client.get_openai_client() as openai_client,
    ):
        eval_object = openai_client.evals.create(
            name="hosted-agent-multiturn-sim",
            data_source_config={
                "type": "custom",
                "item_schema": {
                    "type": "object",
                    "properties": {"messages": {"type": "array"}},
                    "required": ["messages"],
                },
                "include_sample_schema": False,
            },
            testing_criteria=build_conversation_evaluators(model),
        )
        print(f"Eval created: {eval_object.id}")

        run: Union[RunCreateResponse, RunRetrieveResponse] = openai_client.evals.runs.create(
            eval_id=eval_object.id,
            name=f"sim-{target_agent()['name']}",
            evaluation_level="conversation",
            data_source={
                "type": "azure_ai_target_completions",
                "source": {
                    "type": "file_content",
                    "content": [{"item": s} for s in scenarios],
                },
                "target": target_agent(),
                "input_messages": {
                    "type": "template",
                    "template": [
                        {
                            "type": "message",
                            "role": "user",
                            "content": {
                                "type": "input_text",
                                "text": "{{item.test_case_description}}",
                            },
                        }
                    ],
                },
                "item_generation_params": {
                    "type": "conversation_gen_preview",
                    "model": sim_model,
                    "num_conversations": 1,
                    "max_turns": 4,
                    "sampling_params": {
                        "temperature": 0.7,
                        "top_p": 1.0,
                        "max_completion_tokens": 800,
                    },
                    "data_mapping": {
                        "test_case_description": "test_case_description",
                        "id": "id",
                        "desired_num_turns": "desired_num_turns",
                    },
                },
            },
        )
        print(f"Eval run created: {run.id}")
        print("Simulation runs can take several minutes per conversation …")

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
# </run_simulation_eval>


if __name__ == "__main__":
    main()
