# External Agent Observability — Weather Agent on ACA

This sample shows the **end-to-end story for a Foundry "external" agent**:
a third-party agent runtime that lives **outside** Foundry, registered
into Foundry purely so its OpenTelemetry traces and Foundry-side
evaluations light up in the portal.

The runtime here is a tiny [LangChain](https://python.langchain.com/)
weather agent (LangGraph ReAct), instrumented with the
[Microsoft OpenTelemetry distro](https://github.com/microsoft/opentelemetry-distro-python)
so its spans flow into the Application Insights connected to your
Foundry project. We deploy it to **Azure Container Apps** to play the
role of "agent hosted somewhere outside Foundry" (it could just as
easily be GCP Cloud Run, AWS, or on-prem).

> **Preview note.** External agents are gated behind
> `Foundry-Features: ExternalAgents=V1Preview` while in public preview.
> The SDK calls below opt in via `allow_preview=True`.

> **Distro note.** The Microsoft OTel distro does not yet accept an
> explicit `otel_agent_id` input
> ([microsoft/opentelemetry-distro-python#148](https://github.com/microsoft/opentelemetry-distro-python/issues/148)).
> Until that ships, this sample relies on the default `gen_ai.agent.id`
> emitted by the LangChain instrumentation. The Foundry registration
> uses the matching agent name so the Foundry trace view will resolve
> once the distro fix is available.

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
| [Dockerfile](Dockerfile) | Container image for the weather agent. |
| [deploy.sh](deploy.sh) / [deploy.ps1](deploy.ps1) | Build, push to ACR, deploy to ACA, and generate traffic. |
| [generate_traffic.py](generate_traffic.py) | Hits the deployed agent with a handful of weather questions. |
| [register_external_agent.py](register_external_agent.py) | Registers the runtime in Foundry as `kind=external` via the `azure-ai-projects` SDK. |
| [run_trace_eval.py](run_trace_eval.py) | Runs a one-off trace-based eval over the registered agent and prints scores. |
| [requirements.txt](requirements.txt) | Python deps for both the runtime and the helper scripts. |

## Architecture

```
   ┌────────────────────────┐         OTel spans          ┌─────────────────────┐
   │  Weather agent (ACA)   │ ─────────────────────────▶  │ Application Insights │
   │  LangChain + MS distro │   gen_ai.agent.id =         │ (linked to project) │
   └────────────────────────┘   "weather-agent"           └─────────┬───────────┘
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
   - An Azure Container Apps environment + ACR in the same subscription.
   - An Azure OpenAI deployment (e.g. `gpt-4o-mini`) for both the agent
     LLM and the eval judge. (You can split them with the
     `EVAL_MODEL_DEPLOYMENT` env var.)
2. **Permissions** — `Contributor` (or equivalent) on the RG, plus
   permission to create agents in the Foundry project (e.g.
   `Azure AI User`).
3. **Tooling** — Azure CLI, Docker is *not* required (we use
   `az acr build`), Python 3.11+.

## Step 1 — Configure environment

Start from [.env.example](.env.example), create a local `.env`, and
load those values into your shell before running the scripts. The local
`.env` file is ignored by git.

```bash
export AZURE_SUBSCRIPTION_ID="<subscription-id>"
export FOUNDRY_PROJECT_ENDPOINT="https://<resource>.services.ai.azure.com/api/projects/<project>"
export RESOURCE_GROUP="..."
export LOCATION="eastus2"
export ACA_ENV="..."
export ACR_NAME="..."
export APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=...;IngestionEndpoint=..."
export AZURE_OPENAI_ENDPOINT="https://<aoai>.openai.azure.com"
export AZURE_OPENAI_DEPLOYMENT="gpt-4o-mini"
export AZURE_OPENAI_API_VERSION="2024-10-21"
export AZURE_OPENAI_API_KEY="..."
```

PowerShell users: set the same names with `$env:NAME = "..."`.

## Step 2 — Deploy the external runtime to ACA and generate traffic

```bash
cd samples/python/external-agents/observability
./deploy.sh           # or .\deploy.ps1 on Windows
```

The script will:

1. `az acr build` the image.
2. Create or update the Container App.
3. Wait for `/healthz`.
4. Run [generate_traffic.py](generate_traffic.py) to ask several weather
   questions.
5. Wait for OTel export and ingestion. Override the default 90 seconds
   with `TRACE_INGEST_WAIT_SECS` if needed.

## Step 3 — Register the external agent in Foundry

```bash
python register_external_agent.py
```

This calls `project_client.agents.create_version(...)` with an
`ExternalAgentDefinition`, which atomically creates the Foundry agent
record on first call (per the spec, external agents are versionless
from the user's perspective). After registration succeeds, open the
Foundry portal:

> **Project → Agents → `weather-agent` → Traces**

You should see the spans you just generated, attributed to the new
`external` agent.

## Step 4 — Run a one-off trace evaluation

```bash
python run_trace_eval.py
```

This:

1. Resolves the registered agent's `otel_agent_id`.
2. Creates an OpenAI-compatible eval group with two built-in trace
   evaluators (`intent_resolution`, `task_adherence`).
3. Creates an `azure_ai_traces_preview` run scoped to that
   `agent_id` over the last 24 hours.
4. Polls until completion and prints per-criterion pass/fail counts.

> The trace-eval surface is currently only on the OpenAI-compatible
> `evals` API (`project_client.get_openai_client().evals`). When the
> native `project_client.evaluations` surface adds trace-eval support,
> this sample will move there.

## Cleanup

```bash
az containerapp delete -n "$AGENT_NAME" -g "$RESOURCE_GROUP" --yes
# Optional: delete the Foundry registration (does not affect the runtime)
python -c "import os; from azure.identity import DefaultAzureCredential; \
from azure.ai.projects import AIProjectClient; \
AIProjectClient(endpoint=os.environ['FOUNDRY_PROJECT_ENDPOINT'], \
credential=DefaultAzureCredential(), allow_preview=True) \
.agents.delete(agent_name=os.environ.get('AGENT_NAME','weather-agent'))"
```
