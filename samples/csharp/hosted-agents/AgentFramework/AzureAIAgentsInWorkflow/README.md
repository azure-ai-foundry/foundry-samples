**IMPORTANT!** All samples and other resources made available in this GitHub repository ("samples") are designed to assist in accelerating development of agents, solutions, and agent workflows for various scenarios. Review all provided resources and carefully test output behavior in the context of your use case. AI responses may be inaccurate and AI actions should be monitored with human oversight. Learn more in the transparency documents for [Agent Service](https://learn.microsoft.com/en-us/azure/ai-foundry/responsible-ai/agents/transparency-note) and [Agent Framework](https://github.com/microsoft/agent-framework/blob/main/TRANSPARENCY_FAQ.md).

Agents, solutions, or other output you create may be subject to legal and regulatory requirements, may require licenses, or may not be suitable for all industries, scenarios, or use cases. By using any sample, you are acknowledging that any output created using those samples are solely your responsibility, and that you will comply with all applicable laws, regulations, and relevant safety standards, terms of service, and codes of conduct.

Third-party samples contained in this folder are subject to their own designated terms, and they have not been tested or verified by Microsoft or its affiliates.

Microsoft has no responsibility to you or others with respect to any of these samples or any resulting output.

# What this sample demonstrates

This sample demonstrates a **key advantage of code-based hosted agents**:

- **Multi-agent workflows** - Orchestrate multiple agents working together

Code-based agents can execute **any C# code** you write. This sample includes a Writer-Reviewer workflow where two agents collaborate: a Writer creates content and a Reviewer provides feedback.

The agent is hosted using the [Azure AI AgentServer SDK](https://www.nuget.org/packages/Azure.AI.AgentServer.AgentFramework/) and
deploy it to Microsoft Foundry using the Azure Developer CLI [ai agent](https://aka.ms/azdaiagent/docs) extension.

## How It Works

### Multi-Agent Workflow

This sample creates two agents:

- **Writer** - An agent that creates and edits content based on feedback
- **Reviewer** - An agent that provides actionable feedback on the content

The `WorkflowBuilder` connects these agents in a sequential flow:

1. The Writer receives the initial request and generates content
2. The Reviewer evaluates the content and provides feedback
3. Both agent responses are output to the user

### Agent Hosting

The agent is hosted using the [Azure AI AgentServer SDK](https://www.nuget.org/packages/Azure.AI.AgentServer.AgentFramework/),
which provisions a REST API endpoint compatible with the OpenAI Responses protocol. This allows interaction with the agent workflow using OpenAI Responses compatible clients.

### Agent Deployment

The hosted agent workflow can be seamlessly deployed to Microsoft Foundry using the Azure Developer CLI [ai agent](https://aka.ms/azdaiagent/docs) extension.
The extension builds a container image for the agent, deploys it to Azure Container Instances (ACI), and creates a hosted agent version and deployment on Foundry Agent Service.

## Running the Agent Locally

### Prerequisites

Before running this sample, ensure you have:

1. **Azure AI Foundry Project**
   - Project created.
   - Chat model deployed (e.g., `gpt-4o` or `gpt-4.1`)
   - Note your project endpoint URL and model deployment name

2. **Azure CLI**
   - Installed and authenticated
   - Run `az login` and verify with `az account show`

3. **.NET 10.0 SDK or later**
   - Verify your version: `dotnet --version`
   - Download from [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)

### Environment Variables

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
dotnet restore
dotnet build
dotnet run
```

This will start the hosted agent locally on `http://localhost:8088/`.

### Interacting with the Agent

**Run-Requests:**

You can interact with the agent workflow using:

- The `run-requests.http` file in this directory to test and prompt the agent
- Any OpenAI Responses compatible client by sending requests to `http://localhost:8088/`.

**PowerShell (Windows):**

```powershell
$body = @{
    input = "Create a slogan for a new electric SUV that is affordable and fun to drive"
    stream = $false
} | ConvertTo-Json

Invoke-RestMethod -Uri http://localhost:8088/responses -Method Post -Body $body -ContentType "application/json"
```

**Bash/curl (Linux/macOS):**

```bash
curl -sS -H "Content-Type: application/json" -X POST http://localhost:8088/responses \
   -d '{"input": "Create a slogan for a new electric SUV that is affordable and fun to drive","stream":false}'
```

You can also use the `run-requests.http` file in this directory with the VS Code REST Client extension.

The Writer agent will generate content based on your prompt, and the Reviewer agent will provide feedback on the output.

### Deploying the Agent to Microsoft Foundry

To deploy your agent to Microsoft Foundry, follow the comprehensive deployment guide at https://aka.ms/azdaiagent/docs

## Troubleshooting

### Images built on Apple Silicon or other ARM64 machines do not work on our service

We **recommend using `azd` cloud build**, which always builds images with the correct architecture.

If you choose to **build locally**, and your machine is **not `linux/amd64`** (for example, an Apple Silicon Mac), the image will **not be compatible with our service**, causing runtime failures.

**Fix for local builds**

Add this line at the top of your `Dockerfile`:

```dockerfile
FROM --platform=linux/amd64 python:3.12-slim
```

This forces the image to be built for the required `amd64` architecture.
