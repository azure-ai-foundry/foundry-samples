**IMPORTANT!** All samples and other resources made available in this GitHub repository ("samples") are designed to assist in accelerating development of agents, solutions, and agent workflows for various scenarios. Review all provided resources and carefully test output behavior in the context of your use case. AI responses may be inaccurate and AI actions should be monitored with human oversight. Learn more in the transparency documents for [Agent Service](https://learn.microsoft.com/en-us/azure/ai-foundry/responsible-ai/agents/transparency-note) and [Agent Framework](https://github.com/microsoft/agent-framework/blob/main/TRANSPARENCY_FAQ.md).

Agents, solutions, or other output you create may be subject to legal and regulatory requirements, may require licenses, or may not be suitable for all industries, scenarios, or use cases. By using any sample, you are acknowledging that any output created using those samples are solely your responsibility, and that you will comply with all applicable laws, regulations, and relevant safety standards, terms of service, and codes of conduct.

Third-party samples contained in this folder are subject to their own designated terms, and they have not been tested or verified by Microsoft or its affiliates.

Microsoft has no responsibility to you or others with respect to any of these samples or any resulting output.

# What this sample demonstrates

This sample demonstrates a **key advantage of code-based hosted agents**:

- **Local C# tool execution** - Run custom C# methods as agent tools

Code-based agents can execute **any C# code** you write. This sample includes a Seattle Hotel Agent with a `GetAvailableHotels` tool that searches for available hotels based on check-in/check-out dates and budget preferences.

The agent is hosted using the [Azure AI AgentServer SDK](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/ai.agentserver.agentframework-readme) and can be deployed to Microsoft Foundry using the Azure Developer CLI.

## How It Works

### Local Tools Integration

In [Program.cs](Program.cs), the agent uses a local C# method (`GetAvailableHotels`) that simulates a hotel availability API. This demonstrates how code-based agents can execute custom server-side logic that prompt agents cannot access.

The tool accepts:
- **checkInDate** - Check-in date in YYYY-MM-DD format
- **checkOutDate** - Check-out date in YYYY-MM-DD format  
- **maxPrice** - Maximum price per night in USD (optional, defaults to $500)

### Agent Hosting

The agent is hosted using the [Azure AI AgentServer SDK](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/ai.agentserver.agentframework-readme),
which provisions a REST API endpoint compatible with the OpenAI Responses protocol.

### Agent Deployment

The hosted agent can be deployed to Microsoft Foundry using the Azure Developer CLI [ai agent](https://learn.microsoft.com/en-us/azure/ai-foundry/agents/concepts/hosted-agents?view=foundry&tabs=cli#create-a-hosted-agent) extension.

## Running the Agent Locally

### Prerequisites

Before running this sample, ensure you have:

1. **Azure AI Foundry Project**
   - Project created in [Azure AI Foundry](https://learn.microsoft.com/en-us/azure/ai-foundry/what-is-foundry?view=foundry#microsoft-foundry-portals)
   - Chat model deployed (e.g., `gpt-4o` or `gpt-4.1`)
   - Note your project endpoint URL and model deployment name

2. **Azure CLI**
   - Installed and authenticated
   - Run `az login` and verify with `az account show`

3. **.NET 10.0 SDK or later**
   - Verify your version: `dotnet --version`
   - Download from [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)

### Environment Variables

Set the following environment variables (matching `agent.yaml`):

- `AZURE_AI_PROJECT_ENDPOINT` - Your Azure AI Foundry project endpoint URL (required)
- `MODEL_DEPLOYMENT_NAME` - The deployment name for your chat model (defaults to `gpt-4.1-mini`)

**PowerShell:**

```powershell
# Replace with your actual values
$env:AZURE_AI_PROJECT_ENDPOINT="https://<your-resource>.services.ai.azure.com/api/projects/<your-project>"
$env:MODEL_DEPLOYMENT_NAME="gpt-4.1-mini"
```

**Bash:**

```bash
export AZURE_AI_PROJECT_ENDPOINT="https://<your-resource>.services.ai.azure.com/api/projects/<your-project>"
export MODEL_DEPLOYMENT_NAME="gpt-4.1-mini"
```

### Running the Sample

To run the agent, execute the following command in your terminal:

```bash
dotnet run
```

This will start the hosted agent locally on `http://localhost:8088/`.

### Interacting with the Agent

**PowerShell (Windows):**
```powershell
$body = @{
    input = "I need a hotel in Seattle from 2025-03-15 to 2025-03-18, budget under $200 per night"
    stream = $false
} | ConvertTo-Json

Invoke-RestMethod -Uri http://localhost:8088/responses -Method Post -Body $body -ContentType "application/json"
```

**Bash/curl (Linux/macOS):**
```bash
curl -sS -H "Content-Type: application/json" -X POST http://localhost:8088/responses \
   -d '{"input": "Find me hotels in Seattle for March 20-23, 2025 under $200 per night","stream":false}'
```

You can also use the `run-requests.http` file in this directory with the VS Code REST Client extension.

The agent will use the `GetAvailableHotels` tool to search for available hotels matching your criteria.

### Deploying the Agent to Microsoft Foundry

To deploy your agent to Microsoft Foundry, follow the comprehensive deployment guide at https://learn.microsoft.com/en-us/azure/ai-foundry/agents/concepts/hosted-agents?view=foundry&tabs=cli

## Troubleshooting

### Images built on Apple Silicon or other ARM64 machines do not work on our service

We **recommend using `azd` cloud build**, which always builds images with the correct architecture.

If you choose to **build locally**, and your machine is **not `linux/amd64`** (for example, an Apple Silicon Mac), the image will **not be compatible with our service**, causing runtime failures.

**Fix for local builds**

Use this command to build the image locally:

```shell
docker build --platform=linux/amd64 -t image .
```

This forces the image to be built for the required `amd64` architecture.
