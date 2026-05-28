# External Agent Observability — Local Weather Agent

This sample shows the **end-to-end story for a Foundry "external" agent**:
a third-party agent runtime that lives **outside** Foundry, registered
into Foundry purely so its OpenTelemetry traces and Foundry-side
evaluations light up in the portal.

The runtime here is a tiny [LangChain](https://python.langchain.com/)
weather agent, instrumented with the
[Microsoft OpenTelemetry distro](https://github.com/microsoft/opentelemetry-distro-python)
so its spans flow into the Application Insights connected to your
Foundry project. The sample runs the agent locally to keep the demo
small. You can deploy the same runtime anywhere you host your agents;
just keep the same environment variables and `gen_ai.agent.id` value.

**Preview note.** External agents are gated behind
`Foundry-Features: ExternalAgents=V1Preview` while in public preview.
The SDK calls below opt in via `allow_preview=True`.

**Distro note.** The Microsoft OTel distro now forwards per-library
kwargs from `instrumentation_options` into the instrumentor call
([microsoft/opentelemetry-distro-python#149](https://github.com/microsoft/opentelemetry-distro-python/pull/149)).
This sample passes `agent_id` and `agent_name` through the LangChain
instrumentation options so the emitted span attribute
`gen_ai.agent.id` matches the Foundry external-agent registration.

## Microsoft OpenTelemetry distro — references

To learn more about the distro or to find samples in another language,
start here:

- **Docs:** [Microsoft OpenTelemetry overview](https://learn.microsoft.com/en-us/azure/microsoft-opentelemetry/overview)
- **Samples by language:**
  - .NET — [microsoft/opentelemetry-distro-dotnet](https://github.com/microsoft/opentelemetry-distro-dotnet)
  - Python — [microsoft/opentelemetry-distro-python](https://github.com/microsoft/opentelemetry-distro-python)
  - JavaScript — [microsoft/opentelemetry-distro-javascript](https://github.com/microsoft/opentelemetry-distro-javascript)

## What's in this folder

| File | Purpose |
| --- | --- |
| [weather_agent.py](weather_agent.py) | LangChain weather agent + Microsoft OTel distro, exposed as a FastAPI HTTP service. This is the "external runtime". |
| [.env.example](.env.example) | Placeholder environment template for local configuration. |
| [generate_traffic.py](generate_traffic.py) | Sends a handful of weather questions to the running agent. |
| [register_external_agent.py](register_external_agent.py) | Registers the runtime in Foundry as `kind=external` via the `azure-ai-projects` SDK. |
| [run_trace_eval.py](run_trace_eval.py) | Runs a one-off trace-based eval over the registered agent and prints scores. |
| [requirements.txt](requirements.txt) | Python deps for both the runtime and the helper scripts. |

## Architecture

```text
   ┌──────────────────────────┐       OTel spans         ┌──────────────────────┐
   │ Local weather agent      │ ───────────────────────▶ │ Application Insights │
   │ LangChain + MS distro    │  gen_ai.agent.id =       │ (linked to project)  │
   └──────────────────────────┘  "weather-agent-v1"      └─────────┬────────────┘
                                                                    │
                              register_external_agent.py            │ trace view
                                       │                            ▼
                                       ▼                     ┌─────────────────────┐
                              ┌─────────────────────┐        │   Foundry Portal    │
                              │  Foundry Project    │ ◀────  │  Agents → traces    │
                              │  agent kind=external│        │  Evaluations        │
                              └─────────────────────┘        └─────────────────────┘
```

## Prerequisites

1. **Azure resources**
   - A Foundry project with an Application Insights connection.
   - An Azure OpenAI deployment (for example, `gpt-4o-mini`) for both
     the agent LLM and the eval judge.
2. **Permissions** — permission to create agents in the Foundry project
   (for example, `Azure AI User`).

## Step 1 — Configure environment

Start from [.env.example](.env.example), create a local `.env`, and
fill in your project, Application Insights, and Azure OpenAI values. The
Python scripts load this file automatically, and the local `.env` file is
ignored by git.

```env
FOUNDRY_PROJECT_ENDPOINT=https://<resource>.services.ai.azure.com/api/projects/<project>
APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=...;IngestionEndpoint=...
AZURE_OPENAI_ENDPOINT=https://<aoai>.openai.azure.com
AZURE_OPENAI_DEPLOYMENT=gpt-4o-mini
AZURE_OPENAI_API_VERSION=2024-10-21
AZURE_OPENAI_API_KEY=...
```

The runtime sets the required OpenTelemetry defaults before instrumentation.
Review the message-content capture setting in [weather_agent.py](weather_agent.py)
before using the sample with sensitive prompts or responses.

## Step 2 — Run the external runtime locally

```bash
cd samples/python/external-agents/observability
python -m pip install -r requirements.txt
python weather_agent.py
```

In another terminal, verify the runtime is healthy:

```bash
python -c "import httpx; print(httpx.get('http://localhost:8000/healthz').json())"
```

## Step 3 — Generate local traffic

```bash
python generate_traffic.py http://localhost:8000
```

Wait a minute or two for OpenTelemetry export and Application Insights
ingestion. The agent spans should include
`gen_ai.agent.id = weather-agent-v1`, `gen_ai.input.messages`, and
`gen_ai.output.messages`.

## Step 4 — Register the external agent in Foundry

```bash
python register_external_agent.py
```

This calls `project_client.agents.create_version(...)` with an
`ExternalAgentDefinition`, which creates the Foundry agent record if it
does not already exist. After registration succeeds, open the Foundry
portal:

> **Project → Agents → `weather-agent` → Traces**

The trace view will show spans attributed to this `external` agent.

## Step 5 — Run a one-off trace evaluation

Before running the trace evaluation, grant the Foundry project managed
identity the **Log Analytics Reader** role on the connected Application
Insights resource.

```bash
python run_trace_eval.py
```

This:

1. Resolves the registered agent's `otel_agent_id`.
2. Creates an OpenAI-compatible eval group with the built-in trace
   evaluator `intent_resolution`.
3. Creates an `azure_ai_traces` run scoped to that
   `agent_id` over the last 24 hours.
4. Polls until completion and prints per-criterion pass/fail counts.
