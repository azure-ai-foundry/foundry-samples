# What this sample demonstrates

An **instrumented** [LangGraph](https://langchain-ai.github.io/langgraph/) agent hosted on Foundry over the **Responses protocol** using [`langchain_azure_ai.agents.hosting`](https://github.com/langchain-ai/langchain-azure/tree/main/libs/azure-ai/langchain_azure_ai/agents/hosting). A single call to [`enable_auto_tracing()`](https://github.com/langchain-ai/langchain-azure/blob/main/libs/azure-ai/langchain_azure_ai/callbacks/tracers/auto_instrument.py) wires GenAI OpenTelemetry spans into every LangGraph node, LLM call, and tool invocation, with no per-call instrumentation code.

## How It Works

### Tools

| Tool | Purpose |
|---|---|
| `get_current_time` | Local `@tool` — returns the current UTC time. |
| `calculator` | Local `@tool` — evaluates a math expression. |

System prompt: *"You are a friendly assistant. Keep your answers brief."*

### LangGraph Agent

The compiled graph is built with `langchain.agents.create_agent(model, tools=[...], system_prompt=...)`, which returns a compiled LangGraph runnable implementing the standard ReAct loop.

See [main.py](main.py) for the full implementation.

### Agent Hosting

The compiled graph is hosted with `ResponsesHostServer`, which exposes the OpenAI-compatible Responses endpoint at `/responses` and handles conversation history, streaming lifecycle events, and tool-call surfacing automatically.

### Instrumentation

LangGraph does not auto-enable tracing from environment variables alone — a code call is required. The sample makes this one call at startup:

```python
from langchain_azure_ai.callbacks.tracers import enable_auto_tracing
enable_auto_tracing()
```

This injects an `AzureAIOpenTelemetryTracer` into every LangChain `BaseCallbackManager` and into the LangGraph helper factories, so every node, LLM call, and tool invocation produces [GenAI semantic-convention](https://opentelemetry.io/docs/specs/semconv/gen-ai/) spans.

The sample sets two tracing toggles in [agent.manifest.yaml](agent.manifest.yaml) and [agent.yaml](agent.yaml):

| Variable | Purpose |
|---|---|
| `OTEL_AUTO_CONFIGURE_AZURE_MONITOR` | Let `enable_auto_tracing()` configure the OpenTelemetry `TracerProvider` and Azure Monitor exporter itself, using `APPLICATIONINSIGHTS_CONNECTION_STRING`. |
| `AZURE_TRACING_GEN_AI_CONTENT_RECORDING_ENABLED` | Capture prompts, completions, and tool I/O on spans. |

The `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable is injected when the agent is deployed to Foundry, so no extra setup is needed in hosted mode. To ship telemetry from a **local** run, you must set it yourself — either in `.env` (for `python main.py`) or via `azd env set` (for `azd ai agent run`).

## Running the Agent Host

Follow the instructions in the [Running the Agent Host Locally](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/langgraph/README.md#running-the-agent-host-locally) section of the README in the parent directory to run the agent host. To ship telemetry from a local run, also set `APPLICATIONINSIGHTS_CONNECTION_STRING` — see [.env.example](.env.example).

## Interacting with the agent

> Depending on how you run the agent host, you can invoke the agent using `curl` (`Invoke-WebRequest` in PowerShell) or `azd`. Please refer to the [parent README](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/langgraph/README.md) for more details. Use this README for sample queries you can send to the agent.

Send a single-turn request that exercises both tools and produces a rich span tree:

```bash
curl -X POST http://localhost:8088/responses \
    -H "Content-Type: application/json" \
    -d '{"input": "What time is it right now, and what is 42 multiplied by 17?"}'
```

```powershell
(Invoke-WebRequest -Uri http://localhost:8088/responses -Method POST -ContentType "application/json" -Body '{"input": "What time is it right now, and what is 42 multiplied by 17?"}').Content
```

Invoke with `azd`:

```powershell
azd ai agent invoke --local "What time is it right now, and what is 42 multiplied by 17?"
```

A typical span hierarchy for this request:

- `invoke_agent` — the overall agent turn.
- `chat` — each call to the underlying model.
- `execute_tool` — each tool invocation (`get_current_time`, `calculator`).

See the [OpenTelemetry GenAI Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/) for the span and attribute reference.

### Test in Agent Inspector

Once the agent is running locally, open **Agent Inspector** in VS Code (Command Palette: **Foundry Toolkit: Open Agent Inspector**) to interactively send messages and view responses.

Type the following message in Inspector:

```
What time is it right now, and what is 42 multiplied by 17?
```

## Deploying the Agent to Foundry

To host the agent on Foundry, follow the instructions in the [Deploying the Agent to Foundry](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/langgraph/README.md#deploying-the-agent-to-foundry) section of the README in the parent directory.

### Viewing telemetry in Foundry

Once deployed, the agent's traces, metrics, and logs flow into the Application Insights workspace associated with your Foundry project. In the Foundry portal, open the agent and switch to the **Traces** tab to see each conversation and drill into its span tree.

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
