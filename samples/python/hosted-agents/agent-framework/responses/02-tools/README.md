# What this sample demonstrates

An [Agent Framework](https://github.com/microsoft/agent-framework) hosted agent with **locally-defined Python tools** using the **Responses protocol**. It shows how to define custom tools with the `@tool` decorator and register them with the agent so the model can call them during a conversation. A `get_weather` function is included as an example tool.

## How It Works

### Model Integration

The agent uses `FoundryChatClient` from the Agent Framework to create an OpenAI-compatible Responses client from the project endpoint and model deployment. When a request arrives, the framework routes it to the agent, which can call registered local tools (such as `get_weather`) before composing the final reply.

See [main.py](main.py) for the full implementation.

### Agent Hosting

The agent is hosted using the [Agent Framework](https://github.com/microsoft/agent-framework) with the `ResponsesHostServer`, which provisions a REST API endpoint compatible with the OpenAI Responses protocol.

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

3. **Python 3.10 or higher**
   - Verify your version: `python --version`

> [!NOTE]
> You do **not** need an existing [Microsoft Foundry](https://learn.microsoft.com/en-us/azure/ai-foundry/what-is-foundry?view=foundry) project or model deployment to get started — `azd provision` creates them for you. If you already have a project, see the [note below](#using-azd-recommended-for-cli-workflows) on how to target it.

### Environment Variables

See [`.env.example`](.env.example) for the full list of environment variables this sample uses.

| Variable | Required | Description |
|----------|----------|-------------|
| `FOUNDRY_PROJECT_ENDPOINT` | Yes | Foundry project endpoint. Auto-injected in hosted containers; set automatically by `azd ai agent run` locally. |
| `MODEL_DEPLOYMENT_NAME` | Yes | Model deployment name — must match your Foundry project deployment. Declared in `agent.manifest.yaml`. |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Recommended | Enables telemetry. Auto-injected in hosted containers; set manually for local dev. |

**Local development (without `azd`):**

```bash
# Copy and fill in values, then source
cp .env.example .env
# Edit .env with your values
source .env
```

> [!NOTE]
> When using `azd ai agent run`, environment variables are handled automatically — no manual setup needed.

### Installing Dependencies

> [!NOTE]
> If using `azd ai agent run`, dependencies are installed automatically — skip to [Running the Sample](#running-the-sample).

```bash
python -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
```

### Running the Sample

The recommended way to run and test hosted agents locally is with the Azure Developer CLI (`azd`) or the Foundry VS Code extension.

#### Using the Foundry VS Code Extension

The [Foundry VS Code extension](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?view=foundry&pivots=vscode) has a built-in sample gallery. You can open this sample directly from the extension without cloning the repository — it scaffolds the project into a new workspace, generates `agent.yaml`, `.env`, and `.vscode/tasks.json` + `launch.json` automatically, and configures a one-click **F5** debug experience.

Follow the [VS Code quickstart](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?view=foundry&pivots=vscode) for a full step-by-step walkthrough.

#### Using [`azd`](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?view=foundry&pivots=azd) (recommended for CLI workflows)

No cloning required. Create a new folder, point `azd` at the manifest on GitHub, and it sets up the sample and generates Bicep infrastructure, `agent.yaml`, and env config automatically:

```bash
# Create a new folder for the agent and navigate into it
mkdir tools-agent && cd tools-agent

# Initialize from the manifest — azd reads it, downloads the sample,
# and generates Bicep infrastructure, agent.yaml, and env config
azd ai agent init -m https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/agent-framework/responses/02-tools/agent.manifest.yaml

# Provision Azure resources (Foundry project, model deployment, App Insights)
azd provision

# Run the agent locally (handles env vars, Docker build, and startup)
azd ai agent run
```

> [!NOTE]
> If you've already cloned this repository, pass a local path to the manifest instead:
> `azd ai agent init -m <path-to-repo>/samples/python/hosted-agents/agent-framework/responses/02-tools/agent.manifest.yaml`

> [!NOTE]
> If you already have a Foundry project and model deployment, add `-p <project-id> -d <deployment-name>` to `azd ai agent init` to target existing resources. You can also skip provisioning entirely and configure env vars manually — see [Without `azd`](#without-azd).

The agent starts on `http://localhost:8088/`. To invoke it:

```bash
azd ai agent invoke --local "What is the weather in Seattle?"
```

Or use curl directly:

```bash
curl -sS -X POST http://localhost:8088/responses \
  -H "Content-Type: application/json" \
  -d '{"input": "What is the weather in Seattle?", "stream": false}' | jq .
```

#### Without `azd`

If running without `azd`, set environment variables manually (see [Environment Variables](#environment-variables)), then:

```bash
python main.py
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
azd ai agent invoke "What is the weather in Seattle?"
```

To stream logs from the running agent:

```bash
azd ai agent monitor
```

For the full deployment guide, see [Azure AI Foundry hosted agents](https://aka.ms/azdaiagent/docs).

## Troubleshooting

### Images built on Apple Silicon or other ARM64 machines do not work on our service

We **recommend deploying with `azd deploy`**, which uses ACR remote build and always produces images with the correct architecture.

If you choose to **build locally**, and your machine is **not `linux/amd64`** (for example, an Apple Silicon Mac), the image will **not be compatible with our service**, causing runtime failures.

**Fix for local builds**

Use this command to build the image locally:

```shell
docker build --platform=linux/amd64 -t image .
```

This forces the image to be built for the required `amd64` architecture.
