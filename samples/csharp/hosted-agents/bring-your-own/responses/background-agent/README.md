**IMPORTANT!** All samples and other resources made available in this GitHub repository ("samples") are designed to assist in accelerating development of agents, solutions, and agent workflows for various scenarios. Review all provided resources and carefully test output behavior in the context of your use case. AI responses may be inaccurate and AI actions should be monitored with human oversight.

# Background Agent (Responses Protocol) — .NET

This sample demonstrates a long-running agent built with [Azure.AI.AgentServer.Responses](https://www.nuget.org/packages/Azure.AI.AgentServer.Responses) that uses the background execution mode for asynchronous processing. It calls Azure OpenAI to generate a multi-section research analysis, streaming LLM tokens as they arrive via the Responses API event lifecycle.

## How It Works

The agent receives a request via `POST /responses` with `"background": true`. The server returns immediately while the handler calls Azure OpenAI in the background, streaming response tokens as `text.delta` events. The caller polls `GET /responses/{id}` until the response reaches a terminal status (`completed`, `failed`, or `incomplete`). In-flight requests can be cancelled via `POST /responses/{id}/cancel`.

The handler itself stays simple — background mode, polling, and cancellation are all managed by the SDK automatically.

## Running Locally

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Azure CLI installed and authenticated (`az login`)
- An Azure AI Foundry project with an Azure OpenAI deployment

### Environment Variables

| Variable | Description |
|---|---|
| `FOUNDRY_PROJECT_ENDPOINT` | Azure AI Foundry project endpoint (auto-injected when deployed) |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | Azure OpenAI model deployment name (e.g., `gpt-4.1-mini`) |

### Start the Agent

```bash
export FOUNDRY_PROJECT_ENDPOINT="https://your-resource.services.ai.azure.com/api/projects/your-project"
export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4.1-mini"
dotnet run
```

The agent starts on `http://localhost:8088/`.

### Test — Background Mode

```bash
# Submit a background research analysis
curl -X POST http://localhost:8088/responses \
  -H "Content-Type: application/json" \
  -d '{"model": "research", "input": "Analyze the impact of AI on healthcare", "background": true, "store": true}'

# Poll for result (use the id from the response)
curl http://localhost:8088/responses/<response_id>

# Cancel an in-flight request
curl -X POST http://localhost:8088/responses/<response_id>/cancel
```

### Test — Default Mode (Synchronous)

```bash
curl -X POST http://localhost:8088/responses \
  -H "Content-Type: application/json" \
  -d '{"model": "research", "input": "Analyze the impact of AI on healthcare"}'
```

## Invoke with azd

### Local

```bash
azd ai agent invoke --local "Analyze the impact of AI on healthcare"
```

### Remote (after `azd up`)

```bash
azd ai agent invoke "Analyze the impact of AI on healthcare"
```

## Deploying to Microsoft Foundry

To deploy your agent to Microsoft Foundry, follow the deployment guide at https://github.com/microsoft/hosted-agents-vnext-private-preview/blob/main/azd-quickstart.md

## Project Structure

```
background-agent/
├── Program.cs               # Agent entry point and handler implementation
├── background-agent.csproj  # .NET project file with dependencies
├── Dockerfile               # Container build definition
├── agent.yaml               # Agent deployment configuration
├── agent.manifest.yaml      # Agent manifest for Foundry
├── .dockerignore            # Docker build exclusions
├── .env.example             # Example environment variables
├── test-payload.json        # Sample request payload for testing
└── README.md                # This file
```
