# Foundry Toolbox - MCP Skills

An agent that uses [**Foundry Toolbox skills**](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/tools/skills?pivots=dotnet) to answer user prompts. Skills are discovered from a Foundry Toolbox MCP endpoint and made available to the agent at runtime.

## How It Works

The agent uses an `AgentSkillsProvider` (built via `AgentSkillsProviderBuilder.UseMcpSkills`) to interact with skills through the [Agent Skills](https://agentskills.io/) progressive-disclosure pattern:

1. **Advertise** - skill names and descriptions are injected into the system prompt so the agent knows what is available.
2. **Load** - when the agent decides a skill is relevant, it retrieves the full skill body via the provider.
3. **Read resources** - if a skill includes supplementary content (reference documents, assets), the agent reads them on demand via the provider.

The full skill body and resources are only fetched from the toolbox when the agent actually needs them, reducing token usage.

### Agent Hosting

The agent is hosted using the [Agent Framework](https://github.com/microsoft/agent-framework) with the Responses API hosting layer (`AddFoundryResponses` / `MapFoundryResponses`).

See [Program.cs](src/foundry-toolbox-mcp-skills/Program.cs) for the full implementation.

## Prerequisites

Before running this sample, ensure you have:

1. **Azure Developer CLI (`azd`)**
   - [Install azd](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd) and the AI agent extension: `azd ext install azure.ai.agents`
   - Authenticated: `azd auth login`

2. **Azure CLI**
   - Installed and authenticated: `az login`

3. **.NET 10.0 SDK or later**
   - Verify your version: `dotnet --version`
   - Download from [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)

4. **A Foundry Toolbox with MCP-based skills**
   - A [Foundry Toolbox](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/tools/toolbox) with at least one [skill](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/tools/skills?pivots=dotnet) attached. Skills are authored through the Skills API and attached to a toolbox version so that any MCP client can discover and load them.

> [!NOTE]
> This sample only consumes skills from an existing toolbox - it does not create or provision them.

## Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `FOUNDRY_PROJECT_ENDPOINT` | Yes | Foundry project endpoint. |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | Yes | Model deployment name - must match a deployment in your Foundry project. |
| `TOOLBOX_NAME` | Yes | Name of the Foundry Toolbox to connect to. The toolbox must already be provisioned with MCP-based skills. |

> [!NOTE]
> Values for these environment variables are provided by the `azd ai agent init` command.

## Running Locally

For instructions on running this sample locally, see the [parent README](../README.md).

## Deploying the Agent to Microsoft Foundry

The quickest path to deploy this sample to Microsoft Foundry:

```bash
# Create a new folder for the agent and navigate into it
mkdir foundry-toolbox-mcp-skills && cd foundry-toolbox-mcp-skills

# Initialize from the manifest - azd reads it, downloads the sample,
# and adopts its azure.yaml as the project manifest and configures your environment.
azd ai agent init -m https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/csharp/hosted-agents/agent-framework/foundry-toolbox-mcp-skills/azure.yaml

# Provision Azure resources, build, push, and deploy the agent to Foundry.
azd up
```

After deploying, invoke the agent running in Foundry:

```bash
azd ai agent invoke "What skills do you have available?"
```

To stream logs from the running agent:

```bash
azd ai agent monitor
```

For additional deployment options (Foundry VS Code extension, deploying from a local clone, etc.), see the [parent README](../README.md). For the full deployment guide, see [Azure AI Foundry hosted agents](https://aka.ms/azdaiagent/docs).
