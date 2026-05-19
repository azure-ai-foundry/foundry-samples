"""Run a one-off trace-based evaluation over the registered external
weather agent and print the per-criterion scores.

The trace-eval surface is currently exposed only via the OpenAI-compatible
``evals`` API (``azure_ai_source`` / ``azure_ai_traces_preview`` data sources),
so we go through ``project_client.get_openai_client()`` per the spec.
"""

from __future__ import annotations

import os
import time

from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential

AGENT_NAME = os.environ.get("AGENT_NAME", "weather-agent")
EVAL_DEPLOYMENT = os.environ.get(
    "EVAL_MODEL_DEPLOYMENT",
    os.environ.get("AZURE_OPENAI_DEPLOYMENT", "gpt-4o-mini"),
)
LOOKBACK_HOURS = int(os.environ.get("LOOKBACK_HOURS", "24"))
POLL_TIMEOUT_SECS = int(os.environ.get("EVAL_POLL_TIMEOUT_SECS", "900"))


def main() -> None:
    project_client = AIProjectClient(
        endpoint=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
        credential=DefaultAzureCredential(),
        allow_preview=True,
    )

    # Resolve otel_agent_id from the registration so we evaluate exactly
    # the spans Foundry attributes to this agent.
    agent = project_client.agents.get(agent_name=AGENT_NAME)
    otel_agent_id = agent.versions.latest.definition.otel_agent_id
    print(f"Evaluating traces for agent_id={otel_agent_id} (last {LOOKBACK_HOURS}h)")

    openai_client = project_client.get_openai_client()

    # 1. Eval group -- defines what we measure.
    eval_group = openai_client.evals.create(
        name=f"{AGENT_NAME}-trace-eval",
        data_source_config={"type": "azure_ai_source", "scenario": "traces_preview"},
        testing_criteria=[
            {
                "type": "azure_ai_evaluator",
                "name": "intent_resolution",
                "evaluator_name": "builtin.intent_resolution",
                "data_mapping": {
                    "query": "{{query}}",
                    "response": "{{response}}",
                    "tool_definitions": "{{tool_definitions}}",
                },
                "initialization_parameters": {"deployment_name": EVAL_DEPLOYMENT},
            },
            {
                "type": "azure_ai_evaluator",
                "name": "task_adherence",
                "evaluator_name": "builtin.task_adherence",
                "data_mapping": {
                    "query": "{{query}}",
                    "response": "{{response}}",
                    "tool_definitions": "{{tool_definitions}}",
                },
                "initialization_parameters": {"deployment_name": EVAL_DEPLOYMENT},
            },
        ],
    )

    # 2. One-off run -- scoped to this agent's traces over the lookback.
    run = openai_client.evals.runs.create(
        eval_id=eval_group.id,
        name=f"{AGENT_NAME}-trace-run",
        data_source={
            "type": "azure_ai_traces_preview",
            "agent_id": otel_agent_id,
            "lookback_hours": LOOKBACK_HOURS,
        },
    )
    print(f"Created eval run {run.id}; polling for completion...")

    # 3. Poll until terminal.
    deadline = time.time() + POLL_TIMEOUT_SECS
    terminal = {"completed", "failed", "canceled"}
    while time.time() < deadline:
        run = openai_client.evals.runs.retrieve(run_id=run.id, eval_id=eval_group.id)
        if run.status in terminal:
            break
        print(f"  status={run.status} ...")
        time.sleep(15)
    else:
        raise TimeoutError(f"Eval run did not finish within {POLL_TIMEOUT_SECS}s")

    print(f"\nEval run finished: status={run.status}")
    if run.status != "completed":
        print(run)
        return

    # 4. Print per-criterion aggregate scores.
    print("\nResult counts:")
    print(f"  passed : {getattr(run.result_counts, 'passed', 'n/a')}")
    print(f"  failed : {getattr(run.result_counts, 'failed', 'n/a')}")
    print(f"  errored: {getattr(run.result_counts, 'errored', 'n/a')}")
    print(f"  total  : {getattr(run.result_counts, 'total', 'n/a')}")

    print("\nPer-criterion scores:")
    for tc in getattr(run, "per_testing_criteria_results", []) or []:
        name = getattr(tc, "testing_criteria", None) or getattr(tc, "name", "?")
        passed = getattr(tc, "passed", "?")
        failed = getattr(tc, "failed", "?")
        print(f"  - {name}: passed={passed} failed={failed}")


if __name__ == "__main__":
    main()
