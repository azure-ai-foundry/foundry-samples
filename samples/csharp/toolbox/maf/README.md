<!-- Begin standard disclaimer — do not modify -->
**IMPORTANT!** All samples and other resources made available in this GitHub repository ("samples") are designed to assist in accelerating development of agents, solutions, and agent workflows for various scenarios. Review all provided resources and carefully test output behavior in the context of your use case. AI responses may be inaccurate and AI actions should be monitored with human oversight. Learn more in the transparency documents for [Agent Service](https://learn.microsoft.com/en-us/azure/ai-foundry/responsible-ai/agents/transparency-note) and [Agent Framework](https://github.com/microsoft/agent-framework/blob/main/TRANSPARENCY_FAQ.md).

Agents, solutions, or other output you create may be subject to legal and regulatory requirements, may require licenses, or may not be suitable for all industries, scenarios, or use cases. By using any sample, you are acknowledging that any output created using those samples are solely your responsibility, and that you will comply with all applicable laws, regulations, and relevant safety standards, terms of service, and codes of conduct.

Third-party samples contained in this folder are subject to their own designated terms, and they have not been tested or verified by Microsoft or its affiliates.

Microsoft has no responsibility to you or others with respect to any of these samples or any resulting output.
<!-- End standard disclaimer -->

# Agent Framework Toolbox Agent (.NET) — Responses Protocol

This sample deploys a .NET Agent Framework agent wired to toolbox MCP in Microsoft Foundry,
using the `Azure.AI.AgentServer.Responses` SDK with the **Responses** protocol.

It is the .NET counterpart to the Python Agent Framework toolbox sample in
[../../python/toolbox/maf/](../../python/toolbox/maf/).

## Features

- **Toolbox MCP integration**: Connects to toolbox endpoints in Microsoft Foundry via MCP HTTP
- **Azure OpenAI function calling**: Discovered MCP tools are exposed as OpenAI function tools
- **Bearer token authentication**: Acquires an Azure AD token for the toolbox endpoint
  using `DefaultAzureCredential`
- **Multi-round tool calling**: Supports up to 5 rounds of LLM ↔ tool interaction per request
- **Graceful error handling**: Reports toolbox configuration issues at startup

## How It Works

1. The agent connects to a toolbox MCP endpoint using a custom `ToolboxMcpClient`
2. It discovers available tools via MCP `tools/list`
3. Tools are converted to Azure OpenAI `ChatTool` function definitions
4. User messages are processed with the LLM; when the model requests a tool call,
   it is forwarded to the toolbox MCP endpoint via `tools/call`
5. The final text response is streamed back via the Responses protocol

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A [Microsoft Foundry](https://ai.azure.com) project
- A toolbox already created in that project — see [`../crud-sample/`](../crud-sample/) or the
  Python SDK sample [`../../python/toolbox/sample_toolboxes_crud.py`](../../python/toolbox/sample_toolboxes_crud.py)
- Azure CLI installed and logged in:

  ```bash
  az login
  ```

## Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `FOUNDRY_PROJECT_ENDPOINT` | **Yes** | Foundry project endpoint URL — platform-injected at runtime |
| `MODEL_DEPLOYMENT_NAME` | **Yes** | Model deployment name (e.g. `gpt-4.1`) |
| `TOOLBOX_ENDPOINT` | **Yes** | Full toolbox MCP URL **including `?api-version=v1`** |

The toolbox MCP endpoint URL supports two forms:

```
# Latest version:
https://<account>.services.ai.azure.com/api/projects/<project>/toolboxes/<name>/mcp?api-version=v1

# Pinned to a specific version:
https://<account>.services.ai.azure.com/api/projects/<project>/toolboxes/<name>/versions/<version>/mcp?api-version=v1
```

## Running Locally

**Bash / macOS / Linux:**
```bash
export FOUNDRY_PROJECT_ENDPOINT=https://<account>.services.ai.azure.com/api/projects/<project>
export MODEL_DEPLOYMENT_NAME=gpt-4.1
export TOOLBOX_ENDPOINT=https://<account>.services.ai.azure.com/api/projects/<project>/toolboxes/<name>/mcp?api-version=v1

dotnet run
```

**PowerShell (Windows):**
```powershell
$env:FOUNDRY_PROJECT_ENDPOINT = "https://<account>.services.ai.azure.com/api/projects/<project>"
$env:MODEL_DEPLOYMENT_NAME = "gpt-4.1"
$env:TOOLBOX_ENDPOINT = "https://<account>.services.ai.azure.com/api/projects/<project>/toolboxes/<name>/mcp?api-version=v1"

dotnet run
```

## Testing

```bash
curl -N -X POST http://localhost:8088/responses \
    -H "Content-Type: application/json" \
    -d '{"model": "chat", "input": "What tools do you have available?"}'
```

## Deploying as a Hosted Agent

### Prerequisites for deployment

- [Azure Developer CLI (`azd`)](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd) installed
- The `azure.ai.agents` azd extension installed:

  ```bash
  azd extension install azure.ai.agents
  ```

### Deploy steps

```bash
# 1. Log in to Azure
azd auth login

# 2. Create a new directory and initialize an agent project
mkdir my-dotnet-maf-agent && cd my-dotnet-maf-agent
azd ai agent init \
  -m https://github.com/microsoft/hosted-agents-vnext-private-preview/blob/main/samples/dotnet/toolbox/maf/ToolboxMafAgent/agent.manifest.yaml \
  --project-id /subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>/projects/<project>
```

After `azd ai agent init` completes, set required environment variables:

```bash
# Enable vNext features (required during private preview)
azd env set enableHostedAgentVNext "true"

# Set the model deployment name (must match a deployment in your Foundry project)
azd env set MODEL_DEPLOYMENT_NAME "gpt-4.1"

# Set the toolbox endpoint
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

## Protocol

This sample uses the **Responses Protocol** via `Azure.AI.AgentServer.Responses`, which provides:

- OpenAI-compatible `/responses` endpoint
- Streaming SSE support
- Multi-turn conversation via `previous_response_id`
- Tool calling through Azure OpenAI function tools

## Troubleshooting

### 401 Unauthorized from Azure OpenAI

Ensure your identity has the **Cognitive Services OpenAI User** role on the Azure OpenAI resource:

```bash
az role assignment create \
    --role "Cognitive Services OpenAI User" \
    --assignee <your-principal-id> \
    --scope /subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>
```

### Toolbox MCP endpoint returns 400

Ensure the endpoint URL includes `?api-version=v1`.

## Contributing

This project welcomes contributions and suggestions.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow [Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general). Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos are subject to those third-party's policies.
