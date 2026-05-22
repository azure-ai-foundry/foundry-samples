# Copyright (c) Microsoft. All rights reserved.

"""Multi-turn evaluation driven by simulated conversations.

DESCRIPTION:
    Companion to ``main.py``. Once you've deployed the basic agent (``azd up``
    or via the Foundry Toolkit), this script runs an *eval run* that simulates
    multi-turn conversations against the deployed agent and scores them with
    the 4 built-in conversation-level evaluators
    (``customer_satisfaction``, ``groundedness``, ``coherence``,
    ``task_completion``).

    For the full evaluation learning path — Custom Rubric Evaluator, dataset
    generation, scheduled / continuous eval, red-team — see
    [``14-evaluation/``](../14-evaluation/).

USAGE:
    python evaluate_multiturn_simulation.py

    Prerequisites:
      * Deployed ``01-basic`` agent (``EVAL_AGENT_NAME``,
        ``EVAL_AGENT_VERSION`` default to ``agent-framework-agent-basic-responses:1``).
      * ``FOUNDRY_PROJECT_ENDPOINT`` set (see ``.env.example``).
"""

import json
import os
import time
from pathlib import Path
from pprint import pprint
from typing import Union

from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential
from dotenv import load_dotenv
from openai.types.evals.run_create_response import RunCreateResponse
from openai.types.evals.run_retrieve_response import RunRetrieveResponse

API_VERSION = "2025-11-15-preview"
SCENARIOS_FILE = Path(__file__).parent / "data" / "test-scenarios.jsonl"


def _env(name: str, default: str | None = None) -> str:
    value = os.environ.get(name, default)
    if not value:
        raise RuntimeError(f"{name} is not set (see .env.example).")
    return value


def target_agent() -> dict[str, str]:
    return {
        "type": "azure_ai_agent",
        "name": os.environ.get("EVAL_AGENT_NAME", "agent-framework-agent-basic-responses"),
        "version": os.environ.get("EVAL_AGENT_VERSION", "1"),
    }


def build_conversation_evaluators(model: str) -> list[dict]:
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


def load_scenarios() -> list[dict]:
    with SCENARIOS_FILE.open(encoding="utf-8") as fp:
        return [json.loads(line) for line in fp if line.strip()]


def main() -> None:
    load_dotenv()
    endpoint = _env("FOUNDRY_PROJECT_ENDPOINT").rstrip("/")
    model = os.environ.get("AZURE_AI_MODEL_DEPLOYMENT_NAME", "gpt-4.1-mini")
    print(f"Using API version: {API_VERSION}")
    print(f"Project: {endpoint}")
    print(f"Target agent: {target_agent()}\n")

    scenarios = load_scenarios()
    print(f"Loaded {len(scenarios)} seed scenarios from {SCENARIOS_FILE.name}")

    with (
        DefaultAzureCredential() as credential,
        AIProjectClient(endpoint=endpoint, credential=credential) as project_client,
        project_client.get_openai_client() as openai_client,
    ):
        eval_object = openai_client.evals.create(
            name="basic-agent-multiturn-sim",
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
                    "model": model,
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
            print(f"\nFirst of {len(items)} output items:")
            if items:
                pprint(items[0])


if __name__ == "__main__":
    main()
