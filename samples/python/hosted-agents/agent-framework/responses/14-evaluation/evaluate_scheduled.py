# Copyright (c) Microsoft. All rights reserved.

"""Continuous / scheduled evaluation against the deployed hosted agent.

DESCRIPTION:
    Configures an evaluation run that fires automatically as new responses
    are produced by your deployed hosted agent. The same eval group can also
    be re-run on a schedule by re-invoking ``runs.create`` — Foundry tracks
    each run independently.

    Two variants are supported:

      * **Event-triggered** (default) — ``data_source.type =
        "azure_ai_responses"``. Each new agent response triggers an eval row
        in near-real time. Preview in ``2025-11-15-preview``.
      * **Recurring schedule** — set ``EVAL_SCHEDULE_INTERVAL`` (e.g.
        ``1h``, ``24h``); the run uses
        ``azure_ai_trace_data_source_preview`` with an ``agent_filter`` and
        a ``schedule`` clause. The Foundry service re-evaluates new traces
        on the chosen cadence.

    The script prints the eval group + run IDs so you can pause / delete
    them later via ``--delete <eval_id>:<run_id>`` or in the Foundry portal.

USAGE:
    python evaluate_scheduled.py
    python evaluate_scheduled.py --delete <eval_id>:<run_id>

    Prerequisites:
      * Deployed hosted agent with tracing enabled (this sample turns it on
        by default).
      * Continuous evaluation is preview today. Treat exact payload shapes
        as subject to change in future API versions.
"""

# <imports_and_includes>
import os
import sys

from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential
from dotenv import load_dotenv

from eval_common import API_VERSION, model_deployment_name, project_endpoint, target_agent
from evaluate_multiturn_simulation import build_conversation_evaluators
# </imports_and_includes>


# <create_continuous_eval>
def main() -> None:
    load_dotenv()
    print(f"Using API version: {API_VERSION}")
    print(f"Project: {project_endpoint()}")

    if len(sys.argv) >= 3 and sys.argv[1] == "--delete":
        delete_continuous_eval(sys.argv[2])
        return

    agent = target_agent()
    model = model_deployment_name()
    interval = os.environ.get("EVAL_SCHEDULE_INTERVAL")

    with (
        DefaultAzureCredential() as credential,
        AIProjectClient(endpoint=project_endpoint(), credential=credential) as project_client,
        project_client.get_openai_client() as openai_client,
    ):
        eval_object = openai_client.evals.create(
            name="hosted-agent-continuous-eval",
            data_source_config={
                "type": "azure_ai_source",
                "scenario": "traces",
            },
            testing_criteria=build_conversation_evaluators(model),
        )
        print(f"Eval group created: {eval_object.id}")

        if interval:
            data_source = {
                "type": "azure_ai_trace_data_source_preview",
                "trace_source": {
                    "type": "agent_filter",
                    "agent_name": agent["name"],
                    "agent_version": str(agent["version"]),
                    "max_traces": int(os.environ.get("EVAL_MAX_TRACES", "10")),
                },
                "schedule": {"interval": interval},
            }
            run_name = f"scheduled-{agent['name']}-{interval}"
            print(f"Creating recurring schedule every {interval} …")
        else:
            data_source = {
                "type": "azure_ai_responses",
                "agent_name": agent["name"],
                "agent_version": str(agent["version"]),
            }
            run_name = f"continuous-{agent['name']}"
            print("Creating response-triggered (continuous) eval run …")

        run = openai_client.evals.runs.create(
            eval_id=eval_object.id,
            name=run_name,
            evaluation_level="conversation",
            data_source=data_source,
        )
        print(f"Continuous / scheduled run created: {run.id}")
        print(f"  Eval ID: {eval_object.id}")
        print(f"  Run ID:  {run.id}")
        print("\nResults will appear in the Foundry portal Evaluations page as "
              "new responses / traces arrive.")
        print("\nTo pause / delete later:")
        print(f"  python evaluate_scheduled.py --delete {eval_object.id}:{run.id}")
# </create_continuous_eval>


# <delete_continuous_eval>
def delete_continuous_eval(spec: str) -> None:
    """``spec`` is ``"<eval_id>:<run_id>"`` (as printed when created)."""
    eval_id, _, run_id = spec.partition(":")
    if not eval_id or not run_id:
        raise SystemExit("Usage: --delete <eval_id>:<run_id>")
    with (
        DefaultAzureCredential() as credential,
        AIProjectClient(endpoint=project_endpoint(), credential=credential) as project_client,
        project_client.get_openai_client() as openai_client,
    ):
        openai_client.evals.runs.cancel(run_id=run_id, eval_id=eval_id)
        openai_client.evals.runs.delete(run_id=run_id, eval_id=eval_id)
    print(f"Cancelled + deleted run {run_id} on eval {eval_id}.")
# </delete_continuous_eval>


if __name__ == "__main__":
    main()
