# Agent Framework Samples

This directory contains samples that demonstrate how to use the [Agent Framework](https://github.com/microsoft/agent-framework) to host agents with different capabilities and configurations. Each sample includes a README with instructions on how to set up, run, and interact with the agent.

## Samples

### Responses API

| #   | Sample                                                                     | Description                                                                                                                                                                                                                                   |
| --- | -------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | [Basic](responses/01-basic/)                                               | A minimal agent demonstrating basic request/response interaction and multi-turn conversations using `previous_response_id`.                                                                                                                   |
| 2   | [Tools](responses/02-tools/)                                               | An agent with local tools (e.g., weather lookup), demonstrating how to register and invoke custom tool functions alongside the LLM.                                                                                                           |
| 3   | [MCP](responses/03-mcp/)                                                   | An agent connected to a remote MCP server (GitHub), demonstrating external MCP tool provider integration.                                                                                                                                     |
| 4   | [Foundry Toolbox](responses/04-foundry-toolbox/)                           | An agent using Azure Foundry Toolbox, demonstrating toolbox provisioning and querying available tools at runtime.                                                                                                                             |
| 5   | [Workflows](responses/05-workflows/)                                       | An agent with a multi-step orchestrated workflow, demonstrating chaining prompts through an orchestrated flow.                                                                                                                                |
| 6   | [Files](responses/06-files/)                                               | An agent capable of handling files uploaded by users.                                                                                                                                                                                         |
| 7   | [Skills](responses/07-skills/)                                             | An agent using native Agent Framework file-based skills, demonstrating skill discovery and a script-backed PDF travel guide skill.                                                                                                            |
| 8   | [Observability](responses/08-observability/)                               | An agent demonstrating observability features, including logging, metrics, and tracing.                                                                                                                                                       |
| 9   | [Declarative Customer Support](responses/09-declarative-customer-support/) | A multi-turn customer-support triage workflow defined entirely in YAML and hosted as an agent, demonstrating declarative workflow authoring with `InvokeAzureAgent` calls to specialist Foundry-hosted agents and conversation-aware routing. |
| 10  | [Downstream Azure services](responses/10-downstream-azure/)                | An agent that performs data-plane operations on Azure Blob Storage and Service Bus using its per-agent Microsoft Entra identity, demonstrating the per-agent identity + Azure RBAC pattern with no connection strings or shared keys.         |
| 11  | [Azure AI Search RAG](responses/11-azure-search-rag/)                      | An agent with Retrieval Augmented Generation (RAG) capabilities backed by Azure AI Search, grounding answers in documents indexed in a pre-provisioned search index.                                                                          |
| 12  | [Foundry Skills](responses/12-foundry-skills/)                             | An agent that uploads `SKILL.md` files to the Foundry Skills REST API and downloads them at startup, decoupling tone/policy guidelines from agent code.                                                                                       |
| 13  | [Foundry Memory](responses/13-foundry-memory/)                             | An agent with persistent semantic memory backed by an Azure AI Foundry Memory Store, using `FoundryMemoryProvider` to remember user facts across sessions.                                                                                    |
| 14  | [Browser Automation Agent](responses/14-browser-automation-agent/)         | A Foundry-hosted browser automation agent using Foundry Toolbox and the Browser Automation tool (Azure Playwright Service) for general browsing, web scraping, and form filling.                                                                |

### Invocations API

| #   | Sample                         | Description                                                                                                   |
| --- | ------------------------------ | ------------------------------------------------------------------------------------------------------------- |
| 1   | [Basic](invocations/01-basic/) | A minimal agent demonstrating session state management via `agent_session_id` in URL params/response headers. |

### A2A protocol

| # | Sample | Description |
|---|--------|-------------|
| 1 | [Delegation](a2a/01-delegation/) | A two-agent walkthrough: a Responses-protocol **caller** delegates over A2A to a Responses-protocol **executor** that is exposed as an A2A endpoint via Foundry's [incoming A2A](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/enable-agent-to-agent-endpoint) feature. Includes a Bash/PowerShell script that PATCHes the executor to publish its agent card and enable A2A. |

## Running the Agent Host Locally

You can run any sample in this folder using one of three approaches. Pick the one that matches your workflow.

| Approach | Best for | Setup effort |
| --- | --- | --- |
| **[Azure Developer CLI (`azd`)](#using-azd)** | Command-line workflows, scripting, and CI/CD. Auto-provisions Azure resources from the manifest. | Lowest — no clone required |
| **Foundry Toolkit VS Code Extension** | Integrated editor experience with an **Agent Inspector** for chatting with a running agent and a guided **Deploy Hosted Agent** flow. | Lowest — install the extension |
| **[`python`](#using-python)** | Manual control: clone the repo, manage your own venv, set env vars by hand. | Highest |

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
azd ai agent init -m https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/agent-framework/responses/01-basic/agent.manifest.yaml
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

> Note: The environment variables set above are only for the current session. You will need to set them again if you open a new terminal session. if you want to set the environment variables permanently in the azd environment, you can use `azd env set <name> <value>`.

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

### Using the Foundry Toolkit VS Code Extension

The [Foundry Toolkit VS Code extension](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?view=foundry&pivots=vscode) has a built-in sample gallery. You can open this sample directly from the extension without cloning the repository, it scaffolds the project into a new workspace, generates `agent.yaml`, `.env`, and `.vscode/tasks.json` + `launch.json` automatically, and configures a one-click **F5** debug experience.

The extension also adds an **Agent Inspector** UI for chatting with a hosted agent that is already running locally, plus a guided **Deploy Hosted Agent** command (see [Deploying the Agent to Foundry](#deploying-the-agent-to-foundry) below).

#### Prerequisites

1. **Foundry Toolkit VS Code Extension** — [install from the VS Code marketplace](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?pivots=vscode) and sign in to Azure.
2. The agent is already running locally — start it with [`azd ai agent run`](#using-azd) or [`python main.py`](#using-python) first.

#### Open the Agent Inspector

With the agent running on `http://localhost:8088/`:

1. Open the Command Palette (`Ctrl+Shift+P`) and run **Foundry Toolkit: Open Agent Inspector**.
2. The Inspector auto-connects to the running agent.
3. Send messages from the Inspector to chat with the agent and watch the streamed responses.

### Using `python`

#### Prerequisites

1. An existing Foundry project
2. A deployed model in your Foundry project
3. Azure CLI installed and authenticated
4. Python 3.10 or later

#### Running the Agent Host with Python

Clone the repository containing the sample code:

```bash
git clone https://github.com/microsoft/hosted-agents-vnext-private-preview.git
cd hosted-agents-vnext-private-preview/samples/python/hosted-agents/agent-framework
```

#### Environment setup

1. Navigate to the sample directory you want to explore. Create a virtual environment:

   ```bash
   python -m venv .venv

   # Windows
   .venv\Scripts\Activate

   # macOS/Linux
   source .venv/bin/activate
   ```

2. Install dependencies:

   ```bash
   pip install -r requirements.txt
   ```

3. Create a `.env` file with your Foundry configuration following the `env.example` file in the sample.

4. Make sure you are logged in with the Azure CLI:

   ```bash
   az login
   ```

#### Running the Agent Host

```bash
python main.py
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

Once you've tested locally, deploy to Microsoft Foundry. You can use either `azd` or the Foundry Toolkit VS Code extension — both produce the same result.

| Approach | Best for |
| --- | --- |
| **[`azd deploy`](#using-azd-1)** | Command-line workflows, scripting, and CI/CD. |
| **Foundry Toolkit VS Code Extension** | Guided UI in the editor with prompts for agent name, container registry, and resource size. |

### Using `azd`

#### With an Existing Foundry Project

If you already have a Foundry project and the necessary Azure resources provisioned, you can skip the setup steps and proceed directly to deploying the agent.

After running `azd ai agent init -m <agent.manifest.yaml>` and following the prompts to configure your agent, you will have a project ready for deployment.

#### Setting Up a New Foundry Project

Follow the steps in [Using `azd`](#using-azd) to set up the project and provision the necessary Azure resources for your Foundry deployment.

#### Deploying the Agent

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

For the full deployment guide, see the [official deployment guide](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/deploy-hosted-agent).

Once deployed, learn more about how to manage deployed agents in the [official management guide](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/manage-hosted-agent).

### Deploying with the Foundry Toolkit VS Code Extension

You can also deploy directly from the editor (see [Using the Foundry Toolkit VS Code Extension](#using-the-foundry-toolkit-vscode-extension) above for the local-run setup).

1. Open the Command Palette (`Ctrl+Shift+P`) and run **Foundry Toolkit: Deploy Hosted Agent**. The extension opens a tab-based **Deploy Hosted Agent** wizard and reads `agent.yaml` to auto-populate what it can.
2. If prompted, complete **Foundry Project Setup** to pick the subscription and Foundry project (or create a new one) to deploy to.
3. On the **Basics** tab, configure the core deployment settings:
   - **Deployment Method**: **Code** (upload as a ZIP) or **Container** (Docker image via ACR).
   - For **Code**, pick a packaging option: **Remote** or **Local**.
   - For **Container**, pick a registry option: default ACR, your own ACR, or a prebuilt ACR image.
   - **Hosted Agent Name**: confirm the name to register with the hosting service.
4. On the **Review + Deploy** tab, finalize the runtime and resources:
   - Confirm the auto-detected runtime details (language, entry point, or Dockerfile).
   - Pick a **CPU and Memory** size.
   - Click **Deploy**. Fields are validated inline, and the extension handles the build/upload, agent version creation, and RBAC role assignment.
5. After deployment, invoke the agent in the Agent Playground and stream live logs from the **Logs** tab.

#### Troubleshooting

**Azure OpenAI permission denied (401):** the identity running the agent does not have the required RBAC roles on the Foundry project. Assign **Cognitive Services OpenAI User** and **Azure AI User** to the agent's identity (it may take a few minutes for role assignments to propagate). See the [official deployment guide](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/deploy-hosted-agent) for details.
