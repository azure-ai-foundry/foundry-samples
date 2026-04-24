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

### Using `azd` (Recommended)

```bash
azd ai agent run
```

The agent starts on `http://localhost:8088/`.

### Without `azd`

```bash
export FOUNDRY_PROJECT_ENDPOINT="https://your-resource.services.ai.azure.com/api/projects/your-project"
export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4.1-mini"
dotnet run
```

The agent starts on `http://localhost:8088/`.

## Invoke with azd

### Local

```bash
azd ai agent invoke --local "Analyze the impact of AI on healthcare"
```

### Remote (after `azd up`)

```bash
azd ai agent invoke "Analyze the impact of AI on healthcare"
```

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

## Deploying the Agent to Microsoft Foundry

Once you've tested locally, deploy to Microsoft Foundry:

```bash
# Provision Azure resources (skip if already done during local setup)
azd provision

# Build, push, and deploy the agent to Foundry
azd deploy
```

After deploying, invoke the agent running in Foundry:

```bash
azd ai agent invoke "Analyze the impact of AI on healthcare"
```

To stream logs from the running agent:

```bash
azd ai agent monitor
```

For the full deployment guide, see [Azure AI Foundry hosted agents](https://aka.ms/azdaiagent/docs).

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
├── test-payload.txt         # Sample request payload for testing
└── README.md                # This file
```

## Troubleshooting

### Images built on Apple Silicon or other ARM64 machines do not work on our service

We **recommend deploying with `azd deploy`**, which uses ACR remote build and always produces images with the correct architecture.

If you choose to **build locally**, and your machine is **not `linux/amd64`** (for example, an Apple Silicon Mac), the image will **not be compatible with our service**, causing runtime failures.

**Fix for local builds:**

```bash
docker build --platform=linux/amd64 -t image .
```

This forces the image to be built for the required `amd64` architecture.
