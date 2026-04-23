# Agent Framework Samples

This directory contains samples that demonstrate how to use the [Agent Framework](https://github.com/microsoft/agent-framework) to host agents with different capabilities and configurations. Each sample includes a README with instructions on how to interact with the agent.

## Samples

### Responses API

| # | Sample | Description |
|---|--------|-------------|
| 1 | [hello-world](hello-world/) | A minimal agent demonstrating basic request/response interaction and multi-turn conversations. |
| 2 | [simple-agent](simple-agent/) | A general-purpose AI assistant — the simplest hosted agent using `AsAIAgent(model, instructions)`. |
| 3 | [local-tools](local-tools/) | A hotel search assistant with local C# function tools (`AIFunctionFactory.Create`). |
| 4 | [mcp-tools](mcp-tools/) | An agent demonstrating client-side and server-side MCP tool integration. |
| 5 | [text-search-rag](text-search-rag/) | A support agent with RAG capabilities using `TextSearchProvider`. |
| 6 | [workflows](workflows/) | A multi-agent translation pipeline using `WorkflowBuilder`. |

### Invocations API

| # | Sample | Description |
|---|--------|-------------|
| 1 | [invocations-echo-agent](invocations-echo-agent/) | A minimal echo agent demonstrating session state management via `agent_session_id` (no LLM needed). |

## Running the Agent Host Locally

### Using `azd`

#### Prerequisites

1. **Azure Developer CLI (`azd`)**

    - [Install azd](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd) and the AI agent extension: `azd ext install azure.ai.agents`
    - Authenticated: `azd auth login`

2. **Azure Subscription**

#### Create a new project

**No cloning required**. Create a new folder, point azd at the manifest on GitHub.

```bash
mkdir hosted-agent-framework-agent && cd hosted-agent-framework-agent

# Initialize from the manifest
azd ai agent init -m https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/csharp/hosted-agents/agent-framework/hello-world/agent.manifest.yaml
```

Follow the instructions from `azd ai agent init` to complete the agent initialization. If you don't have an existing Foundry project and a model deployment, `azd ai agent init` will guide you through creating them.

#### Provision Azure Resources

> This step is only needed if you don't have an existing Foundry project and model deployment.

Run the following command to provision the necessary Azure resources:

```bash
azd provision
```

This will create the following Azure resources:

- A new resource group named `rg-[project_name]-dev`. In this guide, `[project_name]` will be `hosted-agent-framework-agent`.
- Within the resource group, among other resources, the most important ones are:
  - A new Foundry instance
  - A new Foundry project, within which a new model deployment will be created
  - An Application Insights instance
  - A container registry, which will be used to store the container images for the hosted agent

#### Set Environment Variables

```bash
export FOUNDRY_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
export AZURE_AI_MODEL_DEPLOYMENT_NAME="<your-model-deployment-name>"
# And any other environment variables required by the sample
```

Or in PowerShell:

```powershell
$env:FOUNDRY_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
$env:AZURE_AI_MODEL_DEPLOYMENT_NAME="<your-model-deployment-name>"
# And any other environment variables required by the sample
```

> Note: The environment variables set above are only for the current session. You will need to set them again if you open a new terminal session.

#### Running the Agent Host

```bash
azd ai agent run
```

Right now, the agent host should be running on `http://localhost:8088`

#### Invoking the Agent

Open another terminal, **navigate to the project directory**, and run the following command to invoke the agent:

```bash
azd ai agent invoke --local "Hello!"
```

Or you can in another terminal, without navigating to the project directory, run the following command to invoke the agent:

```bash
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "Hello!"}'
```

Or in PowerShell:

```powershell
(Invoke-WebRequest -Uri http://localhost:8088/responses -Method POST -ContentType "application/json" -Body '{"input": "Hello!"}').Content
```

### Using `dotnet run`

#### Prerequisites

1. An existing Foundry project
2. A deployed model in your Foundry project
3. [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) installed and authenticated (`az login`)
4. [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later

#### Running the Agent Host with `dotnet`

Clone the repository containing the sample code:

```bash
git clone https://github.com/microsoft-foundry/foundry-samples.git
cd foundry-samples/samples/csharp/hosted-agents/agent-framework
```

#### Environment setup

1. Navigate to the sample directory you want to explore:

   ```bash
   cd hello-world
   ```

2. Restore dependencies:

   ```bash
   dotnet restore
   ```

3. Set environment variables:

   ```bash
   export FOUNDRY_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
   export AZURE_AI_MODEL_DEPLOYMENT_NAME="<your-model-deployment-name>"
   ```

   Or in PowerShell:

   ```powershell
   $env:FOUNDRY_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
   $env:AZURE_AI_MODEL_DEPLOYMENT_NAME="<your-model-deployment-name>"
   ```

4. Make sure you are logged in with the Azure CLI:

   ```bash
   az login
   ```

#### Running the Agent Host

```bash
dotnet run
```

Right now, the agent host should be running on `http://localhost:8088`

#### Invoking the Agent

On another terminal, run the following command to invoke the agent:

```bash
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "Hello!"}'
```

Or in PowerShell:

```powershell
(Invoke-WebRequest -Uri http://localhost:8088/responses -Method POST -ContentType "application/json" -Body '{"input": "Hello!"}').Content
```

## Deploying the Agent to Foundry

Once you've tested locally, deploy to Microsoft Foundry.

### With an Existing Foundry Project

If you already have a Foundry project and the necessary Azure resources provisioned, you can skip the setup steps and proceed directly to deploying the agent.

After running `azd ai agent init -m <agent.manifest.yaml>` and following the prompts to configure your agent, you will have a project ready for deployment.

### Setting Up a New Foundry Project

Follow the steps in [Using `azd`](#using-azd) to set up the project and provision the necessary Azure resources for your Foundry deployment.

### Deploying the Agent

Once the project is setup and resources are provisioned, you can deploy the agent to Foundry by running:

```bash
azd deploy
```

> The Foundry hosting infrastructure will inject the following environment variables into your agent at runtime:
>
> - `FOUNDRY_PROJECT_ENDPOINT`: The endpoint URL for the Foundry project where the agent is deployed.
> - `AZURE_AI_MODEL_DEPLOYMENT_NAME`: The name of the model deployment in your Foundry project. This is configured during the agent initialization process with `azd ai agent init`.
> - `APPLICATIONINSIGHTS_CONNECTION_STRING`: The connection string for Application Insights to enable telemetry for your agent.

This will package your agent and deploy it to the Foundry environment, making it accessible through the Foundry project endpoint. Once it's deployed, you can also access the agent through the Foundry UI.

For the full deployment guide, see [Azure AI Foundry hosted agents](https://aka.ms/azdaiagent/docs).
