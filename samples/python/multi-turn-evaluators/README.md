# Multi-turn evaluators

This sample shows how to evaluate **pre-recorded multi-turn conversations**
with the four Azure AI Foundry multi-turn evaluators:

| Evaluator                  | Builtin name                 | Output       |
|----------------------------|------------------------------|--------------|
| Coherence                  | `builtin.coherence`          | 1-5 score    |
| Task Completion            | `builtin.task_completion`    | Pass / Fail  |
| Customer Satisfaction      | `builtin.customer_satisfaction` | 1-5 score |
| Groundedness               | `builtin.groundedness`       | 1-5 score    |

The notebook ([`multi_turn_evaluators.ipynb`](./multi_turn_evaluators.ipynb)):

1. Loads ~16 small conversation traces from [`data/`](./data) (covering
   low/mid/high expected scores for each evaluator, with both plain-text
   turns and assistant tool calls + tool results).
2. Converts each OpenAI-format `messages` list into the `(query, response,
   tool_definitions)` shape expected by Foundry's cloud evaluators.
3. Creates a single evaluation object that bundles all four evaluators.
4. Submits one run with all traces as inline data via
   `openai_client.evals.runs.create(...)`.
5. Polls until the run completes, retrieves per-item results, and reports
   detailed stats: per-trace score table, score distributions, agreement
   with the human-authored `expected_score`, and a printout of failures.

The data in `data/` is a copy of the curated unit-test traces from the
internal `evaluator_quality_analysis` experiment.

## Prerequisites

* Python 3.10+
* An Azure AI Foundry project with an LLM deployment that can act as the
  judge model (e.g. `gpt-4o-mini`, `gpt-5-mini`).
* `az login` (the notebook authenticates with `DefaultAzureCredential`).

Install the dependencies:

```bash
pip install -r requirements.txt
```

## Environment variables

Either export them in your shell, or copy them into a `.env` file next to
the notebook:

| Variable | Description |
|---|---|
| `FOUNDRY_PROJECT_ENDPOINT` | Project endpoint, e.g. `https://<account>.services.ai.azure.com/api/projects/<project>` |
| `FOUNDRY_MODEL_NAME` | Deployment name of the judge model (e.g. `gpt-4o-mini`) |

## Run

```bash
jupyter notebook multi_turn_evaluators.ipynb
```

Execute the cells top-to-bottom. The evaluation run typically completes in
1-3 minutes depending on the judge model.

## How the sample data is shaped

Each JSON file in `data/` looks like:

```json
{
  "test_id": "csat_score5",
  "evaluator": "customer_satisfaction",
  "expected_score": 5,
  "description": "All requests handled perfectly...",
  "messages": [
    {"role": "user", "content": "..."},
    {"role": "assistant", "content": null,
     "tool_calls": [{"id": "call_1", "type": "function",
                     "function": {"name": "...", "arguments": "{...}"}}]},
    {"role": "tool", "tool_call_id": "call_1", "content": "..."},
    {"role": "assistant", "content": "..."}
  ]
}
```

The notebook contains a small helper that converts this OpenAI message
format into the Foundry conversation schema (`{type: text}` /
`{type: tool_call}` / `{type: tool_result}` content items) and splits the
turns into the `query` (system + user) and `response` (assistant + tool)
arrays expected by the evaluators.

## Extending the sample

* Add more traces under `data/` (same JSON shape) and rerun.
* Add a `tool_definitions` field to a trace's JSON to give the evaluator
  the OpenAI function-calling schemas of the tools the agent had access
  to (improves `task_completion` and `groundedness` accuracy).
* Switch judge model by changing `FOUNDRY_MODEL_NAME`.
* Run the same eval object multiple times (`evals.runs.create(...)`) to
  measure judge reliability across repeats.
