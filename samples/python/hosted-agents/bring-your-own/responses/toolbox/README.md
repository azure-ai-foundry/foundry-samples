<!-- Begin standard disclaimer — do not modify -->
**IMPORTANT!** All samples and other resources made available in this GitHub repository ("samples") are designed to assist in accelerating development of agents, solutions, and agent workflows for various scenarios. Review all provided resources and carefully test output behavior in the context of your use case. AI responses may be inaccurate and AI actions should be monitored with human oversight. Learn more in the transparency documents for [Agent Service](https://learn.microsoft.com/en-us/azure/ai-foundry/responsible-ai/agents/transparency-note) and [Agent Framework](https://github.com/microsoft/agent-framework/blob/main/TRANSPARENCY_FAQ.md).

Agents, solutions, or other output you create may be subject to legal and regulatory requirements, may require licenses, or may not be suitable for all industries, scenarios, or use cases. By using any sample, you are acknowledging that any output created using those samples are solely your responsibility, and that you will comply with all applicable laws, regulations, and relevant safety standards, terms of service, and codes of conduct.

Third-party samples contained in this folder are subject to their own designated terms, and they have not been tested or verified by Microsoft or its affiliates.

Microsoft has no responsibility to you or others with respect to any of these samples or any resulting output.
<!-- End standard disclaimer -->

# What this sample demonstrates

A **Bring Your Own** hosted agent using the **Responses protocol** with **Azure AI Foundry Toolbox MCP** integration in Python. It shows how to connect to a Foundry toolbox at startup, discover available tools via MCP, and let the model call them during conversation through an agentic tool-calling loop.

This sample combines:
- The [`azure-ai-agentserver-responses`](https://pypi.org/project/azure-ai-agentserver-responses/) SDK for the Responses protocol
- The [Foundry SDK (`azure-ai-projects`)](https://pypi.org/project/azure-ai-projects/) for model access via the Responses API
- Direct MCP (JSON-RPC over HTTP) for toolbox tool discovery and invocation

Conversation history is automatically managed by the platform via `previous_response_id`. The handler calls `context.get_history()` to retrieve prior turns.

## How It Works

### Toolbox Integration

At startup, the agent connects to the toolbox MCP endpoint, runs `initialize` + `tools/list`, and converts the discovered tools into function definitions for the Responses API. When the model requests a tool call, the agent executes it via MCP `tools/call` and feeds the result back to the model.

### Model Integration

The agent uses the Foundry SDK Responses API with tool definitions. The agentic loop handles multi-step tool calling — the model can call tools multiple times before producing a final text answer.

### Agent Hosting

The agent is hosted using the [Azure AI AgentServer Responses SDK](https://pypi.org/project/azure-ai-agentserver-responses/), which provisions a REST API endpoint compatible with the Azure AI Responses protocol.

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

**Local development (without `azd`):**

```bash
cp .env.example .env
# Edit .env and fill in your values, then:
export $(grep -v '^#' .env | xargs)
```

### Installing Dependencies

```bash
python -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
```

### Running the Sample

```bash
python main.py
```

The agent starts on `http://localhost:8088`.

### Testing

```bash
# Non-streaming
curl -sS -X POST http://localhost:8088/responses \
    -H "Content-Type: application/json" \
    -d '{"input": "Search the web for Azure AI Foundry news", "stream": false}' | jq .

# Streaming
curl -sS -N -X POST http://localhost:8088/responses \
    -H "Content-Type: application/json" \
    -d '{"input": "What is Azure AI Foundry?", "stream": true}'
```
