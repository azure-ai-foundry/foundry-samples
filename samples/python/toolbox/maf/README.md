<!-- Begin standard disclaimer — do not modify -->
**IMPORTANT!** All samples and other resources made available in this GitHub repository ("samples") are designed to assist in accelerating development of agents, solutions, and agent workflows for various scenarios. Review all provided resources and carefully test output behavior in the context of your use case. AI responses may be inaccurate and AI actions should be monitored with human oversight. Learn more in the transparency documents for [Agent Service](https://learn.microsoft.com/en-us/azure/ai-foundry/responsible-ai/agents/transparency-note) and [Agent Framework](https://github.com/microsoft/agent-framework/blob/main/TRANSPARENCY_FAQ.md).

Agents, solutions, or other output you create may be subject to legal and regulatory requirements, may require licenses, or may not be suitable for all industries, scenarios, or use cases. By using any sample, you are acknowledging that any output created using those samples are solely your responsibility, and that you will comply with all applicable laws, regulations, and relevant safety standards, terms of service, and codes of conduct.

Third-party samples contained in this folder are subject to their own designated terms, and they have not been tested or verified by Microsoft or its affiliates.

Microsoft has no responsibility to you or others with respect to any of these samples or any resulting output.
<!-- End standard disclaimer -->

# Agent Framework Toolbox Agent (Responses Protocol)

A Microsoft Agent Framework (MAF) agent that connects to an **toolbox in Microsoft Foundry**
via MCP and serves responses over the Foundry Responses Protocol. This is the MAF
counterpart to the LangGraph sample in [`../langgraph/`](../langgraph/).

## How It Works

1. On startup the agent creates an `MCPStreamableHTTPTool` pointing at the Toolbox
   MCP endpoint — no async `get_tools()` prefetch is needed; the SDK handles the MCP
   handshake on first use.
2. An Agent Framework agent is built with the MCP tool and a `FoundryChatClient`.
3. Incoming requests are handled by `ResponsesAgentServerHost` on port `8088`.
4. The agent is initialized **lazily** (once, on the first request) and reused for all
   subsequent turns.

## Prerequisites

- Python 3.12+
- A Microsoft Foundry project with a toolbox already created — see
  [`../sample_toolboxes_crud.py`](../sample_toolboxes_crud.py) to create one
- Azure CLI installed and logged in:

  ```bash
  az login
  ```

## Quick Start (Local)

**Linux/macOS:**
```bash
# 1. Copy and fill in the environment file
cp .env.example .env
# Edit .env — set FOUNDRY_PROJECT_ENDPOINT, MODEL_DEPLOYMENT_NAME,
#              and TOOLBOX_ENDPOINT at minimum

# 2. Install dependencies
pip install -r requirements.txt

# 3. Start the agent
python main.py

# 4. Invoke

# Option A — Agent Inspector in VS Code (recommended):
Press F5 and select "Debug Local Agent HTTP Server".
This starts the agent with debugging and opens the Agent Inspector —
an interactive UI for sending messages, viewing tool calls, and debugging.

# Option B — curl:
curl -X POST http://localhost:8088/responses \
  -H "Content-Type: application/json" \
  -d '{"input": "What tools do you have?"}'
```

**Windows (PowerShell):**
```powershell
# 1. Copy and fill in the environment file
Copy-Item .env.example .env
# Edit .env — set FOUNDRY_PROJECT_ENDPOINT, MODEL_DEPLOYMENT_NAME,
#              and TOOLBOX_ENDPOINT at minimum

# 2. Install dependencies
pip install -r requirements.txt

# 3. Start the agent
python main.py

# 4. Invoke

# Option A — Agent Inspector in VS Code (recommended):
# Press F5 and select "Debug Local Agent HTTP Server".
# This starts the agent with debugging and opens the Agent Inspector.

# Option B — Invoke-RestMethod:
Invoke-RestMethod -Method POST http://localhost:8088/responses `
  -ContentType "application/json" `
  -Body '{"input": "What tools do you have?"}'
```

## Deploy as a Hosted Agent

### Option A: Deploy via Microsoft Foundry VS Code Extension

1. Install the **Microsoft Foundry** extension in VS Code.
2. Open the **Command Palette** (`Ctrl+Shift+P`).
3. Run **Microsoft Foundry: Deploy Hosted Agent**.
4. Follow the prompts to select your Foundry project and confirm the deployment.

### Option B: Deploy with azd

**Prerequisites for deployment:**

- [Azure Developer CLI (`azd`)](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd) installed
- The `azure.ai.agents` azd extension installed:

  ```bash
  azd extension install azure.ai.agents
  ```

**Deploy steps:**

```bash
# 1. Log in to Azure
azd auth login

# 2. Create a new directory and initialize the agent project
mkdir my-maf-agent && cd my-maf-agent
azd ai agent init \
  -m https://github.com/microsoft/hosted-agents-vnext-private-preview/blob/main/samples/python/toolbox/maf/agent.manifest.yaml \
  --project-id /subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>/projects/<project>
```

After `azd ai agent init` completes, set required environment variables:

```bash
# Enable vNext features (required during private preview)
azd env set enableHostedAgentVNext "true"

# Set the model deployment name (must match a deployment in your Foundry project)
azd env set MODEL_DEPLOYMENT_NAME "gpt-4.1"

# Set the toolbox endpoint (full URL including ?api-version=v1)
azd env set TOOLBOX_ENDPOINT "https://<account>.services.ai.azure.com/api/projects/<project>/toolboxes/<name>/mcp?api-version=v1"
```

```bash
# 3. Provision Azure infrastructure
azd provision

# 4. Build and deploy the container
azd deploy

# 5. Invoke the deployed agent
azd ai agent invoke --new-session "What tools do you have?" --timeout 120
```

> **Tip:** `azd ai agent invoke` must be run from the scaffolded project directory
> (the directory where `azure.yaml` was created by `azd ai agent init`).  
> The `--timeout 120` flag is recommended — agent cold starts can take up to 60 seconds.

## Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `FOUNDRY_PROJECT_ENDPOINT` | **Yes** | Project endpoint URL — platform-injected at runtime |
| `MODEL_DEPLOYMENT_NAME` | **Yes** | Model deployment name (e.g. `gpt-4.1`) |
| `TOOLBOX_ENDPOINT` | **Yes** | Full toolbox MCP endpoint URL including toolbox name and api-version |
| `FOUNDRY_AGENT_TOOLBOX_FEATURES` | No | Feature-flag header value — platform-injected (default: `Toolboxes=V1Preview`) |

`TOOLBOX_ENDPOINT` is the full pre-constructed MCP URL. Two forms are supported:
```
# Latest version:
https://<account>.services.ai.azure.com/api/projects/<project>/toolboxes/<name>/mcp?api-version=v1

# Pinned to a specific version:
https://<account>.services.ai.azure.com/api/projects/<project>/toolboxes/<name>/versions/<version>/mcp?api-version=v1
```
The version number is the integer toolbox version (e.g. `1`). Use the versioned form to pin to a known-good version.

## Supported Toolbox Tools

See [../SUPPORTED_TOOLBOX_TOOLS.md](../SUPPORTED_TOOLBOX_TOOLS.md) for all supported
tool and auth types. For runnable SDK creation examples, see
[../sample_toolboxes_crud.py](../sample_toolboxes_crud.py).

## SDK Notes

This sample uses the **Agent Framework (MAF)** SDK — `agent-framework-core`,
`agent-framework-foundry`, and `agent-framework-openai`. These package versions
**must match exactly**. Do not upgrade them independently.

## Protocol

This sample uses the **Responses Protocol** (`azure-ai-agentserver-responses`):
- OpenAI-compatible `/responses` endpoint on port `8088`
- Streaming SSE output
- Multi-turn conversation (history fetched automatically)
- 120-second per-request timeout

## Comparison with LangGraph Sample

| Feature | This sample (`maf/`) | LangGraph sample (`langgraph/`) |
|---------|----------------------|--------------------------------|
| SDK | Agent Framework | LangChain / LangGraph |
| MCP client | `MCPStreamableHTTPTool` | `langchain-mcp-adapters` |
| OAuth consent handling | ✅ | ✅ |
| Tool schema sanitization | ❌ (SDK handles it) | ✅ |
| Dependencies | Minimal | LangChain + LangGraph + adapters |

If you're unsure which to use, start with [`../langgraph/`](../langgraph/) — it handles
more edge cases out of the box.

## Troubleshooting

### `ImportError` for `agent_framework`

The `agent-framework-core` and `agent-framework-azure-ai` packages come from PyPI.
Make sure you installed with `pip install -r requirements.txt`.

### `FOUNDRY_PROJECT_ENDPOINT` vs `AZURE_AI_PROJECT_ENDPOINT`

The platform injects `FOUNDRY_PROJECT_ENDPOINT`. The code also accepts
`AZURE_AI_PROJECT_ENDPOINT` for backward compatibility, but always prefer the `FOUNDRY_`
variable in new deployments.

### Tool endpoint returns HTTP 400

The `?api-version=v1` query parameter is required. Verify your `TOOLBOX_ENDPOINT`
includes it (e.g. `.../<name>/mcp?api-version=v1`).

## Tracing

The agent has distributed tracing enabled out of the box via
`enable_instrumentation()` from `agent_framework.observability`. Traces are emitted
using OpenTelemetry and can be exported to Azure Monitor (Application Insights).

### Enable Azure Monitor export

**Locally:** set `APPLICATIONINSIGHTS_CONNECTION_STRING` in `.env`:

```bash
# Get the connection string from your Application Insights resource in the Azure Portal
# (Settings → Properties → Connection String)
APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=<key>;IngestionEndpoint=...
```

**When deployed:** the platform automatically injects `APPLICATIONINSIGHTS_CONNECTION_STRING`
from the Application Insights resource linked to your Foundry project. No additional
configuration is required.

### View traces in Azure Monitor

Once `APPLICATIONINSIGHTS_CONNECTION_STRING` is set:

1. Go to the [Azure Portal](https://portal.azure.com) and open your Application Insights resource.
2. Navigate to **Investigate** → **Transaction search** to see individual traces.
3. Use **Investigate** → **Application map** for an end-to-end dependency view.

Traces include LLM calls, tool invocations (including MCP calls to the toolbox), and
agent framework spans. Each conversation turn produces a linked trace tree rooted at
the incoming `/responses` request.

## Contributing

This project welcomes contributions and suggestions.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow [Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general). Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos are subject to those third-party's policies.

