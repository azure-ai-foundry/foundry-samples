# What this sample demonstrates

An [Agent Framework](https://github.com/microsoft/agent-framework) agent hosted using the **Responses protocol**.

## How It Works

### Model Integration

The agent uses `FoundryChatClient` from the Agent Framework to create a Responses client from the project endpoint and model deployment. The agent supports both streaming (SSE events) and non-streaming (JSON) response modes.

See [main.py](main.py) for the full implementation.

### Agent Hosting

The agent is hosted using the [Agent Framework](https://github.com/microsoft/agent-framework) with the `ResponsesHostServer`, which provisions a REST API endpoint compatible with the OpenAI Responses protocol.

## Interacting with the agent

> Depending on how you run the agent host, you can invoke the agent using `curl` (`Invoke-WebRequest` in PowerShell), `azd`, or the **Agent Inspector** in the Foundry Toolkit VS Code extension. Please refer to the [parent README](../../README.md) for more details. Use this README for sample queries you can send to the agent.

Send a POST request to the server with a JSON body containing a "message" field to interact with the agent. For example:

```bash
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "Hi"}'
```

The server will respond with a JSON object containing the response text and a response ID. You can use this response ID to continue the conversation in subsequent requests.

### Multi-turn conversation

To have a multi-turn conversation with the agent, include the previous response id in the request body. For example:

```bash
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "How are you?", "previous_response_id": "REPLACE_WITH_PREVIOUS_RESPONSE_ID"}'
```

### Test in Agent Inspector

Once the agent is running locally, open **Agent Inspector** in VS Code (Command Palette: **Foundry Toolkit: Open Agent Inspector**) to interactively send messages and view responses.

Type the following message in Inspector:

```
Hi
```

## Deploying the Agent to Foundry

To host the agent on Foundry, follow the instructions in the [Deploying the Agent to Foundry](../../README.md#deploying-the-agent-to-foundry) section of the README in the parent directory.

### Deploying with the Foundry Toolkit VS Code Extension

1. Open the Command Palette (`Ctrl+Shift+P`) and run **Foundry Toolkit: Deploy Hosted Agent**. The extension opens a tab-based **Deploy Hosted Agent** wizard and reads `agent.yaml` to auto-populate what it can.
2. If prompted, complete **Foundry Project Setup** to pick the subscription and Foundry project (or create a new one) to deploy to.
3. On the **Basics** tab, configure the core deployment settings:
   - **Deployment Method**: **Code** (upload as a ZIP) or **Container** (Docker image via ACR).
   - For **Code**, pick a packaging option: **Remote** or **Local**.
   - For **Container**, pick a registry option: default ACR, your own ACR, or a prebuilt ACR image.
   - **Hosted Agent Name**: confirm the name to register with the hosting service.
4. On the **Review + Deploy** tab, finalize the runtime and resources:
   - Confirm the auto-detected runtime details (language, entry point, or Dockerfile).
   - Pick a **CPU and Memory** size.
   - Click **Deploy**. Fields are validated inline, and the extension handles the build/upload, agent version creation, and RBAC role assignment.
5. After deployment, invoke the agent in the Agent Playground and stream live logs from the **Logs** tab.

## Evaluating multi-turn conversations

After your agent is deployed and you've tried it in the Playground, the
next question is *"is it actually any good at multi-turn conversations?"*
**Evaluation** answers that — you run the agent against test conversations
and let built-in evaluators (automated scorers, themselves LLM-backed)
grade each conversation on things like *task completion*, *coherence*,
and *groundedness*. New to evaluation? Skim the **What is evaluation?**
section in [`../14-evaluation/README.md`](../14-evaluation/README.md)
first — this section assumes you've seen it.

Two scripts in this folder let you evaluate multi-turn behavior end-to-end
without leaving the `01-basic` sample:

* **[`evaluate_multiturn_simulation.py`](./evaluate_multiturn_simulation.py)** —
  drives the deployed agent through simulated multi-turn conversations
  seeded from [`data/test-scenarios.jsonl`](./data/test-scenarios.jsonl)
  and scores them with the 4 built-in conversation-level evaluators
  (`customer_satisfaction`, `groundedness`, `coherence`, `task_completion`).
  No traces required — pick this if you haven't enabled tracing yet.
* **[`evaluate_multiturn_traces.py`](./evaluate_multiturn_traces.py)** —
  same 4 evaluators, but scored against **real conversations captured as
  traces**. Use this once your agent is receiving real traffic.

> **Tracing prerequisite for `evaluate_multiturn_traces.py`** — this sample
> does **not** enable tracing by default. Before running the trace-based
> script, copy `ENABLE_INSTRUMENTATION=true` and `ENABLE_SENSITIVE_DATA=true`
> from [`../08-observability/agent.yaml`](../08-observability/agent.yaml) onto
> your `01-basic` deployment, **then redeploy the agent** (changes to
> `agent.yaml` don't take effect until the next `azd up`),
> or just use `evaluate_multiturn_simulation.py` instead.

> ⚠ **About `ENABLE_SENSITIVE_DATA=true`** — that flag means user inputs
> and model outputs (including any PII) are written verbatim to your
> Application Insights workspace, so trace-based evaluation can score the
> content. Fine for dev / demos; for anything customer-facing, decide
> deliberately and treat the trace workspace as customer data.

### Run a script

```bash
pip install -r requirements-eval.txt
az login
# Required:
export FOUNDRY_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4.1-mini"
# Optional overrides (defaults shown):
export EVAL_AGENT_NAME="agent-framework-agent-basic-responses"
export EVAL_AGENT_VERSION="1"

python evaluate_multiturn_simulation.py
# or
python evaluate_multiturn_traces.py
```

> Windows / PowerShell? Replace `export FOO=bar` with `$env:FOO = "bar"`.

Each script prints the eval ID, run ID, a `result_counts` summary, and a
**Foundry portal report URL** — open the URL to drill into per-row scores
and rationales.

### See also

These scripts are co-located here for the **multi-turn learning path**. For
the broader evaluation story — **Custom Rubric Evaluator** ⭐, built-in
single-turn evaluators, dataset generation (traces / synthetic), scheduled /
continuous evaluation, and red-team / safety evaluation — see
[`../14-evaluation/`](../14-evaluation/).
