# LangGraph Toolbox Sample

This sample demonstrates a hosted LangGraph agent prewired for Azure AI Foundry toolbox MCP integration.

## Overview

Deploy a LangGraph agent that automatically loads tools from Azure AI Foundry toolboxes via MCP protocol. Supports:

- **No-auth MCP servers** (public endpoints)
- **Key-auth MCP servers** (GitHub PAT, etc.)
- **OAuth MCP servers** (consent-required flows)
- **Azure Foundry toolboxes** with project connections
- **Fallback to MS Learn MCP** if no toolbox is configured

## Files

| File | Purpose |
|------|---------|
| `main.py` | LangGraph hosted agent runtime with toolbox MCP support |
| `setup.py` | Agent name resolution, telemetry setup, logging |
| `agent.yaml.template` | Hosted agent manifest with env variables |
| `dot-env.template` | Environment template (project endpoint, model, toolbox settings) |
| `sample_toolboxes_crud.py` | SDK samples for creating and managing toolbox resources |
| `requirements.txt` | Python dependencies |
| `Dockerfile` | Container build with CA cert setup |
| `.dockerignore` | Docker build exclusions |

## Quick Start

### 1. Get the project endpoint and credentials

```bash
export AZURE_AI_PROJECT_ENDPOINT=https://<region>.api.azureml.ms/<project-id>
```

### 2. Create a toolbox (optional)

Use `sample_toolboxes_crud.py` to create a toolbox with tools:

```bash
python sample_toolboxes_crud.py mcp-noauth
```

Or use the Azure AI Foundry portal to create a custom toolbox.

### 3. Configure environment

Edit `.env` with one of:

- **Option A: Explicit toolbox endpoint**
  ```
  AZURE_AI_TOOLSET_ENDPOINT=https://<endpoint>/toolsets/<name>/mcp?api-version=v1
  ```

- **Option B: Profile with presets**
  ```
  AZURE_AI_TOOLSET_PROFILE=noauth
  AZURE_AI_TOOLSET_NOAUTH_ENDPOINT=<fallback if not set>
  ```

- **Option C: Fallback to MS Learn MCP (no config needed)**

### 4. Run locally

```bash
python main.py
```

The agent:
1. Reads `AZURE_AI_PROJECT_ENDPOINT` and `MODEL_DEPLOYMENT_NAME`
2. Authenticates to Azure via `DefaultAzureCredential`
3. Connects to the toolbox MCP endpoint (if configured)
4. Loads tools and builds a LangGraph ReAct agent
5. Listens for hosted agent requests

### 5. Deploy to Foundry

```bash
foundry-agent deploy --name my-agent --acr <registry>
```

Deployment:
1. Builds Docker container
2. Pushes to ACR
3. Registers with Azure AI Foundry
4. Returns invoke URL for remote calls

## Toolbox Endpoints

### Resolution order (checked in main.py)

1. **Explicit**: `AZURE_AI_TOOLSET_ENDPOINT` (full MCP URL)
2. **Profile**: `AZURE_AI_TOOLSET_PROFILE=noauth|keyauth` (uses preset endpoints)
3. **Fallback**: MS Learn MCP (`https://learn.microsoft.com/api/mcp`)

### Toolbox MCP authentication

- **No-auth**: public servers (no connection needed)
- **Key-auth**: stored in a project connection (PAT, API keys)
- **OAuth**: stored in an oauth-type connection (requires consent URL)

## Sample Toolbox Operations

`sample_toolboxes_crud.py` demonstrates:

```bash
# Create a no-auth MCP toolbox
python sample_toolboxes_crud.py mcp-noauth

# Create a GitHub-auth MCP toolbox
python sample_toolboxes_crud.py mcp-keyauth

# List all toolbox resources
python sample_toolboxes_crud.py list

# Create an OpenAPI toolbox with a project connection
python sample_toolboxes_crud.py openapi-conn
```

See docstrings in the file for full parameter requirements.

## Environment Variables

| Variable | Purpose | Example |
|----------|---------|---------|
| `AZURE_AI_PROJECT_ENDPOINT` | Foundry project endpoint | `https://<region>.api.azureml.ms/...` |
| `AZURE_OPENAI_ENDPOINT` | Azure OpenAI endpoint for inference | `https://<region>.openai.azure.com` |
| `MODEL_DEPLOYMENT_NAME` | Model deployment in Azure OpenAI | `gpt-4o` |
| `AZURE_AI_TOOLSET_ENDPOINT` | Explicit toolbox MCP endpoint | `https://.../toolsets/<name>/mcp?...` |
| `AZURE_AI_TOOLSET_PROFILE` | Preset profile | `noauth` or `keyauth` |
| `FOUNDRY_PROJECT_ENDPOINT` | For sample_toolboxes_crud.py | Same as PROJECT_ENDPOINT |

Azure still exposes these settings through `toolset` API names, so the environment variables keep their original `AZURE_AI_TOOLSET_*` form.

## Hosting & Invocation

### Local invoke

```bash
curl -X POST http://localhost:8088/evaluate \
  -H "Content-Type: application/json" \
  -d '{"messages": [{"role": "user", "content": "Use a tool to..."}]}'
```

### Remote invoke (after deploy)

```bash
foundry-agent invoke --remote --name my-agent "Use a tool to..."
```

## Troubleshooting

### Tools not appearing in agent response

1. Check if the toolbox endpoint is reachable:
   ```bash
   curl -X POST $AZURE_AI_TOOLSET_ENDPOINT \
     -H "Authorization: Bearer $(az account get-access-token --resource https://ai.azure.com --query accessToken -o tsv)" \
     -H "Foundry-Features: Toolsets=V1Preview" \
     -d '{"jsonrpc":"2.0","id":1,"method":"initialize",...}'
   ```

2. Verify `AZURE_AI_TOOLSET_ENDPOINT` is not empty in deployed config

3. Check container logs: `foundry-agent logs --console --name my-agent`

### Authentication failures

- Ensure `DefaultAzureCredential` is configured (e.g., `az login` locally, or managed identity in hosted)
- Verify project connection exists and has correct credentials
- Check token scope: Foundry uses `https://ai.azure.com/.default`

### Model deployment not found

- Confirm model ID exists in `agent.yaml` resources
- Verify `MODEL_DEPLOYMENT_NAME` matches deployment name in Azure OpenAI
- Check `AZURE_OPENAI_ENDPOINT` is correct

## References

- [Azure AI Foundry Toolsets](https://learn.microsoft.com/en-us/azure/ai-studio/how-to/create-manage-toolsets)
- [LangGraph](https://langchain-ai.github.io/langgraph/)
- [MCP Protocol](https://spec.modelcontextprotocol.io/)
- [Azure AI Agent Framework](https://aka.ms/agent-framework)
