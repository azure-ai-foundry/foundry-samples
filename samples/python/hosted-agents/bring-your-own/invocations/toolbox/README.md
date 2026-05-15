<!-- Begin standard disclaimer — do not modify -->
**IMPORTANT!** All samples and other resources made available in this GitHub repository ("samples") are designed to assist in accelerating development of agents, solutions, and agent workflows for various scenarios. Review all provided resources and carefully test output behavior in the context of your use case. AI responses may be inaccurate and AI actions should be monitored with human oversight. Learn more in the transparency documents for [Agent Service](https://learn.microsoft.com/en-us/azure/ai-foundry/responsible-ai/agents/transparency-note) and [Agent Framework](https://github.com/microsoft/agent-framework/blob/main/TRANSPARENCY_FAQ.md).

Agents, solutions, or other output you create may be subject to legal and regulatory requirements, may require licenses, or may not be suitable for all industries, scenarios, or use cases. By using any sample, you are acknowledging that any output created using those samples are solely your responsibility, and that you will comply with all applicable laws, regulations, and relevant safety standards, terms of service, and codes of conduct.

Third-party samples contained in this folder are subject to their own designated terms, and they have not been tested or verified by Microsoft or its affiliates.

Microsoft has no responsibility to you or others with respect to any of these samples or any resulting output.
<!-- End standard disclaimer -->

# What this sample demonstrates

A **Bring Your Own** hosted agent using the **Invocations protocol** with **Azure AI Foundry Toolbox MCP** integration in Python. It shows how to connect to a Foundry toolbox at startup, discover available tools via MCP, and let the model call them during conversation through an agentic tool-calling loop.

This sample combines:
- The [`azure-ai-agentserver-invocations`](https://pypi.org/project/azure-ai-agentserver-invocations/) SDK for the Invocations protocol
- The [Foundry SDK (`azure-ai-projects`)](https://pypi.org/project/azure-ai-projects/) for model access via the Responses API
- Direct MCP (JSON-RPC over HTTP) for toolbox tool discovery and invocation

> **Invocations vs Responses:** Unlike the Responses protocol, the Invocations protocol does **not** provide built-in server-side conversation history. This agent maintains an in-memory session store keyed by `agent_session_id`. In production, replace it with durable storage (Redis, Cosmos DB, etc.) so history survives restarts.

## How It Works

### Toolbox Integration

At startup, the agent connects to the toolbox MCP endpoint, runs `initialize` + `tools/list`, and converts the discovered tools into function definitions for the Responses API. When the model requests a tool call, the agent executes it via MCP `tools/call` and feeds the result back to the model.

### Model Integration

The agent uses the Foundry SDK Responses API with tool definitions. The agentic loop handles multi-step tool calling — the model can call tools multiple times before producing a final text answer.

### Agent Hosting

The agent is hosted using the [Azure AI AgentServer Invocations SDK](https://pypi.org/project/azure-ai-agentserver-invocations/), which provisions a REST API endpoint compatible with the Azure AI Invocations protocol.

### Agent Deployment

The hosted agent can be developed and deployed to Microsoft Foundry using the [Azure Developer CLI](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?view=foundry&pivots=azd).

## Running the Agent Locally

### Prerequisites

Before running this sample, ensure you have:

1. **Azure Developer CLI (`azd`)** (recommended)
   - [Install azd](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd) and the AI agent extension: `azd ext install azure.ai.agents`
   - Authenticated: `azd auth login`

2. **Azure CLI**
   - Installed and authenticated: `az login`

3. **Python 3.10 or later**
   - Verify your version: `python --version`

4. **A Foundry Toolbox**
   - Create a toolbox in your Foundry project (e.g. with web search, Azure AI Search, or custom MCP tools)

### Environment Variables

See [`.env.example`](.env.example) for the full list of environment variables this sample uses.

| Variable | Required | Description |
|----------|----------|-------------|
| `FOUNDRY_PROJECT_ENDPOINT` | Yes | Foundry project endpoint. Auto-injected in hosted containers; set automatically by `azd ai agent run` locally. |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | Yes | Model deployment name — must match your Foundry project deployment. Declared in `agent.manifest.yaml`. |
| `TOOLBOX_ENDPOINT` | Yes | Full toolbox MCP endpoint URL including toolbox name and `?api-version=v1`. Declared in `agent.manifest.yaml`. |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Recommended | Enables telemetry. Auto-injected in hosted containers; set manually for local dev. |

`TOOLBOX_ENDPOINT` must be the complete MCP URL for your toolbox. Two forms are supported:
```
# Latest version:
https://<account>.services.ai.azure.com/api/projects/<project>/toolboxes/<toolbox-name>/mcp?api-version=v1

# Pinned to a specific version:
https://<account>.services.ai.azure.com/api/projects/<project>/toolboxes/<toolbox-name>/versions/<version>/mcp?api-version=v1
```
Set it as an environment variable in `.env` for local dev, or via `azd env set TOOLBOX_ENDPOINT "<url>"` for deployed agents.

### Running the Sample

#### Using `azd` (Recommended)

```bash
azd ai agent run
```

The agent starts on `http://localhost:8088`.

#### Without `azd`

```bash
cp .env.example .env
# Edit .env and fill in your values, then:
export $(grep -v '^#' .env | xargs)

python -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
python main.py
```

The agent starts on `http://localhost:8088`.

### Testing

**Bash:**
```bash
azd ai agent invoke --local '{"message": "Search the web for Azure AI Foundry news"}'
```

**PowerShell:**
```powershell
azd ai agent invoke --local '{\"message\": \"Search the web for Azure AI Foundry news\"}'
```

Or use `curl`:

```bash
# Turn 1
curl -sS -N -X POST "http://localhost:8088/invocations?agent_session_id=chat-001" \
    -H "Content-Type: application/json" \
    -d '{"message": "Search the web for Azure AI Foundry news"}'

# Turn 2 (same session)
curl -sS -N -X POST "http://localhost:8088/invocations?agent_session_id=chat-001" \
    -H "Content-Type: application/json" \
    -d '{"message": "Tell me more about the first result"}'
```

### Deploying the Agent to Microsoft Foundry

Once you've tested locally, deploy to Microsoft Foundry:

```bash
# Provision Azure resources (skip if already done during local setup)
azd provision

# Build, push, and deploy the agent to Foundry
azd deploy
```

After deploying, invoke the agent running in Foundry:

```bash
azd ai agent invoke "Search the web for Azure AI Foundry news"
```

To stream logs from the running agent:

```bash
azd ai agent monitor
```

For the full deployment guide, see [Azure AI Foundry hosted agents](https://aka.ms/azdaiagent/docs).
---

## Supported Scenarios

The sample toolbox can be configured for any of these 14 scenarios. For each scenario, create a `agent.manifest.yaml` file (see examples below) and pass it to `azd ai agent init -m <manifest-file>`.

<details>
<summary><strong>View all 14 supported scenarios</strong></summary>

Refer to [`samples/python/toolbox/azd/README.md`](../../../../toolbox/azd/README.md#supported-scenarios) for complete inline documentation of all scenarios including:

1. **Web Search** — Bing web search (no auth required)
2. **File Search** — Vector store RAG search
3. **Code Interpreter** — Python code execution
4. **MCP Key-Auth (GitHub)** — GitHub MCP with PAT
5. **MCP No-Auth** — Public MCP servers
6. **MCP OAuth (Managed)** — Foundry-managed OAuth
7. **MCP OAuth (Custom)** — Bring-your-own OAuth app
8. **MCP Agent Identity** — Entra ID agent identity
9. **Azure AI Search** — Search index queries
10. **A2A (Agent-to-Agent)** — Remote agent delegation
11. **Bing Custom Search** — Scoped web search
12. **OpenAPI Key-Auth** — REST API integration
13. **MCP OAuth (Entra Passthrough)** — User identity delegation
14. **Multi-Tool Toolbox** — Web search + GitHub MCP combined

Each scenario includes a complete `agent.manifest.yaml` example with parameter definitions and resource configurations.

</details>
## Troubleshooting

### Images built on Apple Silicon or other ARM64 machines do not work on our service

We **recommend deploying with `azd deploy`**, which uses ACR remote build and always produces images with the correct architecture.

If you choose to **build locally**, and your machine is **not `linux/amd64`** (for example, an Apple Silicon Mac), the image will **not be compatible with our service**, causing runtime failures.

**Fix for local builds:**

```bash
docker build --platform=linux/amd64 -t image .
```

This forces the image to be built for the required `amd64` architecture.
