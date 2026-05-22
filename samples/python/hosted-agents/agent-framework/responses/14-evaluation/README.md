# Evaluating a hosted agent

This sample is the **evaluation learning path** for the Python hosted-agent
samples. New to evaluation? Read the next two sections first — they explain
the *what* and *why* before any code.

## What is evaluation?

Once your agent is deployed, **evaluation** is how you answer the question
*"is my agent actually good?"* You run the agent against a set of test
inputs and let one or more **evaluators** — small judges that emit a
**score and a short rationale** for each response — grade each turn.
Typical reasons to run an evaluation:

* **Catch regressions** before your users do — re-run after every prompt or
  model-deployment change.
* **Compare agent versions** numerically (v1 averaged 3.2 on task
  completion; v2 averages 4.1).
* **Decide if the agent is ready to ship** — block a release until a
  baseline of evaluators passes.
* **Probe for unsafe behavior** under adversarial input (red-teaming).

The output of every evaluation is a row of scores per input + a portal page
where you can drill into per-row scores and rationales. *Nothing in this
sample needs you to write your own evaluator from scratch* — Foundry ships
built-in evaluators, and the **Custom Rubric Evaluator** ⭐ generates a
tailored one for your agent from a short prompt.

### Heads-up: scores don't all use the same scale

Different evaluator families use different scoring shapes. The intuition is
always *"open the report URL and read the rationale"*, but the numbers
mean different things:

| Evaluator family | Scale | Direction |
|---|---|---|
| **Quality** (`builtin.fluency`, `builtin.relevance`, `builtin.coherence`, `builtin.groundedness`) | **1-5** | **Higher is better.** A 5 means "great"; a 1 means "broken". |
| **Agent task** (`builtin.task_adherence`, `builtin.task_completion`, `builtin.customer_satisfaction`) | **Pass / Fail** + numeric score where the evaluator returns one | Trust **`passed`** + the rationale first; the numeric score (when present) is a secondary signal. |
| **Safety / content** (`builtin.violence`, `builtin.self_harm`, `builtin.hate_unfairness`, `builtin.sexual`) | **0-7 severity** | **Higher is worse.** 0 = safe; 4+ = concerning; 6-7 = severe. Default pass threshold is severity ≤ 3. |
| **Attack detection** (`builtin.indirect_attack`) | **Detected / Not detected** | A "detected" result means the agent appears to have been manipulated by a prompt-injection-style attack (bad). |
| **Custom Rubric** (your generated rubric) | **1-5 per dimension**, weighted | Higher is better; the rubric weights each dimension. |

A "passed" row in a quality eval means *score ≥ pass-threshold*; in a
safety eval it means *severity ≤ pass-threshold*. The `result_counts`
summary every script prints reflects this — you don't have to do the math
yourself, just remember the **direction**.

## Concepts at a glance

| Term | What it means |
|---|---|
| **Evaluator** | The judge that scores one row. Three flavors: *built-in* (`builtin.fluency`, `builtin.task_adherence`, …), *custom rubric* (auto-generated from your prompt), or *code-based* (yours). |
| **Dataset** | The rows you evaluate against. Either inline `{query: ...}` items, a registered Foundry dataset, or generated from traces. |
| **Trace** | A recording of one real agent invocation (request, tool calls, response, latencies) sent to Application Insights by the agent runtime. |
| **Eval group** | A reusable "test suite" definition — schema + evaluators. Created once, run many times. |
| **Eval run** | One execution of an eval group against a specific dataset / agent / time window. Has a status, a result-counts summary, and a `report_url`. |
| **Single-turn vs. multi-turn** | Single-turn evaluators score one `{query, response}` pair. Multi-turn evaluators score a whole `messages: [...]` conversation. |
| **Score shape** | See the table above — quality is 1-5 (higher better), safety is 0-7 severity (higher worse), some are boolean. |

> The tags you may see in the Python scripts (`<imports_and_includes>`,
> `<run_eval>`, …) are documentation extraction markers used by the docs
> pipeline. They are inert in Python — feel free to ignore them when
> reading or copying code.

## Your first run

This folder contains *both* a tiny demo agent (`main.py`, `agent.yaml`) **and**
the eval scripts. The flow is:

```
                      ┌───────────────────────────┐
                      │  this sample's tiny agent │
                      │ (deploy once via Foundry) │
                      └─────────────┬─────────────┘
                                    │
                                    ▼
   evaluate_*.py ──── eval run ──── scores ──── report_url in Foundry portal
```

```bash
# 1. Deploy the tiny demo agent (one time).
#    These commands follow the same pattern as every other Python sample
#    in samples/python/hosted-agents/ — see the parent README for details.
mkdir hosted-agent-evaluation && cd hosted-agent-evaluation
azd ai agent init -m ../path/to/foundry-samples/samples/python/hosted-agents/agent-framework/responses/14-evaluation/agent.manifest.yaml
azd up

# After `azd up` succeeds, copy the project endpoint it prints (or grab it
# from your Foundry project's Overview page) into the env var below.

# 2. Set env + install eval deps locally.
az login
export FOUNDRY_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4.1-mini"
pip install -r requirements.txt

# 3. Run your first evaluation. Start with the simplest one.
python evaluate_basic.py
```

> **Windows / PowerShell?** Replace `export FOO=bar` with `$env:FOO = "bar"`.

What you'll see (trimmed):

```
Using API version: 2025-11-15-preview
Project: https://<account>.services.ai.azure.com/api/projects/<project>
Target agent: {'type': 'azure_ai_agent', 'name': 'agent-framework-agent-evaluation-responses', 'version': '1'}

Eval created: eval_abc123…
Eval run created: evalrun_def456…
  status: queued
  status: in_progress
  status: completed

✓ Eval run completed.
Result counts: {'passed': 4, 'failed': 0, 'errored': 0, 'total': 4}
Report URL: https://ai.azure.com/.../evaluations/evalrun_def456…

Showing 3 of 4 output items:
(set EVAL_DEBUG=1 to also see the raw payload.)

  [1] Question: What's the capital of France?
      Answer:   Paris.
    task_adherence            score=n/a    PASS
        rationale: Directly answered the question with the correct city.
    fluency                   score=5      PASS
    relevance                 score=5      PASS
```

Open the **Report URL** in the Foundry portal to see every row, every
evaluator's score and rationale, and an aggregate chart.

> Once you've seen the basic flow work, the *real* recommended starting
> point for your own agent is **[`evaluate_custom_rubric.py`](./evaluate_custom_rubric.py)** ⭐
> — it generates a rubric tailored to *what your agent is supposed to do*,
> not a generic fluency score.

## If a score is low, what next?

A low score is information, not a verdict. Walk this checklist:

1. **Open the `report_url`** the script prints. The portal shows the
   evaluator's *rationale* per row — the words "why" are almost always
   more useful than the number.
2. **Read 3-5 failing rows in full.** Patterns emerge fast:
   * Same evaluator failing across many rows → the agent has a systemic
     weakness (e.g. always loses context after turn 2).
   * One row failing across many evaluators → that single input is hard
     (or the dataset row is malformed).
3. **Edit one thing at a time.** Change the agent's instructions in
   `agent.yaml`, re-deploy (`azd up`), re-run the eval, and
   compare `result_counts` to the previous run. If you change three
   things at once, you can't tell which one helped.
4. **Promote real failure cases into the dataset.** If a row failed and
   you can hand-correct the prompt or expected behavior, add it to the
   eval dataset so the next regression on that case is caught
   automatically.

If `result_counts.errored > 0`, the eval *itself* failed on those rows
(not the agent) — check the portal for the per-row error message
(rate limits, auth, missing fields, etc.).

## Cost and data usage

Most scripts in this folder cost only a small amount of model usage (a
handful of inference calls + an LLM judge per row). A few flows are
heavier — be deliberate before running them:

| Script | What it consumes | Heads-up |
|---|---|---|
| `evaluate_basic.py` | A few agent calls + a few judge calls | Cheapest. Safe default. |
| `evaluate_custom_rubric.py` | One generation LRO + the same eval-run cost | Generation is a multi-stage LLM job; budget a few minutes the first time. |
| `evaluate_multiturn_simulation.py` | Up to *N seeds × turns-per-conversation* agent calls + judge | Costs scale with how many seeds you load — start small. |
| `evaluate_multiturn_traces.py` | Judge calls **over existing traced conversations** — no live agent calls | Trim the trace time window or `agent_filter` to control judge cost and result volume. |
| `generate_dataset_*.py` | Generation LRO (service requires `max_samples ≥ 15`) + eval cost | Each run **registers a new dataset** in your project — clean up old ones in the portal if you iterate a lot. |
| `evaluate_scheduled.py` | One eval row **per new agent response** (event-triggered) | ⚠ **Continues running after the script exits.** Use the portal (or the delete snippet at the bottom of the script) to pause or remove the schedule when you're done. |
| `evaluate_redteam.py` | One agent call per adversarial prompt + judge | ⚠ See the privacy callout below — adversarial prompts + agent responses are *logged*. |

If you're on a sandbox project with cost alerts, set them up before
running the multi-turn / scheduled / red-team flows.

## Pick the right flow

| You want to … | Use this script |
|---|---|
| See what an end-to-end evaluation looks like for the first time | [`evaluate_basic.py`](./evaluate_basic.py) |
| Get a tailored evaluator for **your** agent without writing one by hand ⭐ | [`evaluate_custom_rubric.py`](./evaluate_custom_rubric.py) |
| Evaluate multi-turn behavior **without** any existing dataset (the service generates conversations for you) | [`evaluate_multiturn_simulation.py`](./evaluate_multiturn_simulation.py) |
| Evaluate multi-turn behavior over **your own live traces** | [`evaluate_multiturn_traces.py`](./evaluate_multiturn_traces.py) |
| Turn recent agent **traces** into a reusable evaluation dataset | [`generate_dataset_from_traces.py`](./generate_dataset_from_traces.py) |
| Bootstrap an evaluation dataset from a few **topic seeds** | [`generate_dataset_synthetic.py`](./generate_dataset_synthetic.py) |
| Score every new agent response **continuously** (or on a schedule) | [`evaluate_scheduled.py`](./evaluate_scheduled.py) |
| Probe the agent against **adversarial / red-team** prompts | [`evaluate_redteam.py`](./evaluate_redteam.py) |

## The scripts (recommended reading order)

1. **Built-in evaluators, single-turn** —
   [`evaluate_basic.py`](./evaluate_basic.py)
   * Runs your deployed agent against 4 inline questions and scores each
     answer for *task adherence* (did it answer what was asked?),
     *fluency* (does it read well?), and *relevance* (does it stay on
     topic?). Finishes in under a minute. **The easiest first script.**
2. **Custom Rubric Evaluator** ⭐ —
   [`evaluate_custom_rubric.py`](./evaluate_custom_rubric.py)
   * Generates a 5-7 dimension rubric tailored to *your* agent's job
     (e.g., "tone", "completeness", "did it cite a source?") from a short
     prompt, then evaluates the deployed agent against it. **Edit the
     prompt at the top of `submit_generation_job()` first** — the default
     is a generic placeholder. Optionally also re-edit the auto-generated
     dimensions and regenerate (`EVAL_RUBRIC_REGENERATE=true`). **This is
     what you'd use for a real project**, once you've seen
     `evaluate_basic.py` work.
3. **Built-in evaluators, multi-turn (simulation)** —
   [`evaluate_multiturn_simulation.py`](./evaluate_multiturn_simulation.py)
   * Foundry simulates full multi-turn conversations against your agent
     from a handful of seed scenarios (e.g., *"User asks about weather,
     then a follow-up about an umbrella"*), then scores each conversation
     for `customer_satisfaction`, `groundedness`, `coherence`, and
     `task_completion`. **Run this before you have real traffic.**
4. **Built-in evaluators, multi-turn (over traces)** —
   [`evaluate_multiturn_traces.py`](./evaluate_multiturn_traces.py)
   * Same 4 evaluators, scored against **real conversations recorded as
     traces**. Supports `agent_filter` (recent traces — default),
     `conversation_id_source`, and `trace_id_source` variants via env
     vars. **Run this once your agent is receiving real traffic.**
5. **Generate eval datasets** —
   * [`generate_dataset_from_traces.py`](./generate_dataset_from_traces.py)
     — Materializes recent traces into a reusable, registered Foundry
     dataset, then evaluates the rows. The rows already *contain* the
     agent's historical answers — so this scores past production
     behaviour. To re-run the same questions through the *current* agent,
     wrap the data source in `azure_ai_target_completions` (see
     `evaluate_basic.py`).
   * [`generate_dataset_synthetic.py`](./generate_dataset_synthetic.py) —
     Bootstrap a domain-relevant dataset from short topic seeds when you
     don't have traffic yet. **By default this then runs the questions
     through your deployed agent and scores the agent's answers.** Set
     `EVAL_AGAINST_DATASET_ONLY=true` if you'd rather just sanity-check
     the synthetic rows themselves.
6. **Scheduled / continuous evaluation** —
   [`evaluate_scheduled.py`](./evaluate_scheduled.py)
   * Configures Foundry to score *every new agent response* automatically
     (event-triggered) — or every hour over recent traces if you set
     `EVAL_SCHEDULE_INTERVAL=1h`. Use this in production so regressions
     surface in the portal without you re-running anything.
     ⚠ **The schedule keeps running after the script exits.** See the
     "Cost and data usage" table above for cleanup pointers.
7. **Red-team / safety evaluation** —
   [`evaluate_redteam.py`](./evaluate_redteam.py)
   * Sends adversarial prompts (violence, self-harm, hate, sexual) to your
     agent and scores responses with the built-in safety evaluators on a
     **0-7 severity** scale (higher is worse — see the score-shape table
     above). High severity on `violence` / `self_harm` /
     `hate_unfairness` / `sexual` means the agent produced unsafe content
     for that prompt. ⚠ **This intentionally writes adversarial prompts +
     the agent's responses to your traces** — run it in a non-production
     project, and expect the traces to contain the adversarial content
     for as long as the workspace retains them.

## Prerequisites

1. **A deployed hosted agent.** This folder ships its own tiny demo agent
   (`main.py`, `agent.yaml`). Deploy it once with the `azd ai agent init`
   + `azd up` flow in "Your first run" above (the same pattern as every
   other Python sample in `samples/python/hosted-agents/`). The eval
   scripts target the deployed agent identified by `EVAL_AGENT_NAME`
   (default `agent-framework-agent-evaluation-responses`) and
   `EVAL_AGENT_VERSION` (default `1`) — change those env vars to evaluate
   any other deployed agent.
2. **A Foundry project endpoint** in `FOUNDRY_PROJECT_ENDPOINT`. After
   `azd up` finishes, copy the value from the deploy output or from the
   Foundry portal **Overview** page (form
   `https://<account>.services.ai.azure.com/api/projects/<project>`).
3. **AAD credentials** — `az login`, or any other source the
   `DefaultAzureCredential` chain understands.
4. **Python deps** — `pip install -r requirements.txt`.

### Tracing — and a privacy callout

This sample's agent sets `ENABLE_INSTRUMENTATION=true` and
`ENABLE_SENSITIVE_DATA=true` in [`agent.yaml`](./agent.yaml),
[`agent.manifest.yaml`](./agent.manifest.yaml), and
[`.env.example`](./.env.example). **Tracing** means the Foundry runtime
records every agent request, tool call, and response to Application
Insights (see [`08-observability/`](../08-observability/) for the full
story). The trace-based and continuous eval scripts
([`evaluate_multiturn_traces.py`](./evaluate_multiturn_traces.py),
[`generate_dataset_from_traces.py`](./generate_dataset_from_traces.py), and
[`evaluate_scheduled.py`](./evaluate_scheduled.py)) read those recordings
— turn tracing on once and every script in this folder works.

⚠ **`ENABLE_SENSITIVE_DATA=true` means user inputs, model prompts, and
model outputs (including any PII the user pasted) are written verbatim to
your Application Insights workspace.** That's necessary for trace-based
evaluation to score the *content*, but it also means your trace storage
is now a copy of every conversation. Keep this **off** in production
unless you have an explicit data-handling policy that allows it, and
treat the App Insights workspace as customer data. For non-production
demos this is usually fine; for anything customer-facing, decide
deliberately.

> The trace-based and continuous scripts also need actual **traffic** in
> the trace window (i.e. someone has to have called the agent recently)
> before they have anything to score.

If you're adapting the scripts for the
[`01-basic/`](../01-basic/) sample (which does **not** enable tracing by
default), copy the env-var pattern from
[`08-observability/`](../08-observability/) onto your `01-basic`
deployment first.

## Where to view results

Every script prints:

* the **eval group ID** and **run ID** (use them to look up the run via
  the SDK), and
* a **report URL** that opens the run in the Foundry portal's
  [Evaluations](https://ai.azure.com/) page.

Trace-based and continuous flows additionally surface results on the
**Traces** page next to the original agent invocation — same UX as
[`08-observability/`](../08-observability/).

The friendly per-row summary the scripts print is a trimmed view. If you
want the full raw output for one item, set `EVAL_DEBUG=1` before running
any script.

## Related samples

* [`01-basic/`](../01-basic/) — also ships **multi-turn evaluation scripts**
  (simulation + traces) co-located with the basic agent for the multi-turn
  learning path. Same patterns as scripts 3-4 above, narrowed to the
  `01-basic` agent.
* [`08-observability/`](../08-observability/) — the canonical tracing
  sample. Trace-driven and continuous evaluation depend on the same
  `ENABLE_INSTRUMENTATION` / `ENABLE_SENSITIVE_DATA` pattern this sample
  turns on.

## Learn more

* [Azure AI Foundry — Evaluation overview](https://learn.microsoft.com/azure/ai-foundry/concepts/evaluation-approach-gen-ai)
* [Built-in evaluators reference](https://learn.microsoft.com/azure/ai-foundry/how-to/develop/evaluate-sdk)
* [Continuous evaluation in Foundry](https://learn.microsoft.com/azure/ai-foundry/how-to/develop/agent-evaluate-sdk)
* [Content-safety severity scale (0-7)](https://learn.microsoft.com/azure/ai-services/content-safety/concepts/harm-categories)

## For maintainers

* Scripts pin **API version `2025-11-15-preview`** in
  [`eval_common.py`](./eval_common.py). Bump in one place when GA lands.
* The evaluator-generation LRO, data-generation LRO, and continuous-eval
  configuration are preview surfaces; some are still exposed as raw REST
  in these scripts (via `requests` + a `DefaultAzureCredential` bearer
  token). When a typed Python surface ships, the calls will collapse to
  the typed client.
* For each script, the prerequisites block in the docstring spells out
  which Foundry resources must already exist (deployed agent, dataset,
  traces).
* Add new scripts by following the `evaluate_*` / `generate_dataset_*`
  naming pattern and wiring them into "Pick the right flow" + "The
  scripts" above.
