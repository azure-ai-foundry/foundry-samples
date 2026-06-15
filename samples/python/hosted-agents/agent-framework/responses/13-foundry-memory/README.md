# What this sample demonstrates

An [Agent Framework](https://github.com/microsoft/agent-framework) agent with persistent semantic memory backed by an **Azure AI Foundry Memory Store**, hosted using the **Responses protocol**. The agent remembers facts the user has shared (e.g., dietary preferences, name) across sessions by retrieving and updating memories around every model invocation via `FoundryMemoryProvider`.

## How It Works

### Model Integration

The agent uses `FoundryChatClient` from the Agent Framework to create a Responses client from the project endpoint and model deployment. `allow_preview=True` is passed so the same `AIProjectClient` can also call the preview `beta.memory_stores` API.

### Memory via Foundry Memory Store

`FoundryMemoryProvider` is wired into the agent as a context provider. Around each model invocation it:

1. **Retrieves user-profile memories** for the configured `scope` (e.g., user id) on the first turn of a session.
2. **Searches for contextual memories** matching the current user message and injects them into the model context.
3. **Updates the store** with new facts inferred from the conversation.

Crucially, the provider is constructed with `project_client=client.project_client` — i.e. it reuses the `AIProjectClient` that `FoundryChatClient` already created, instead of allocating a second one. This keeps a single authentication context and connection pool for both chat and memory operations.

See [main.py](main.py) for the full implementation.

### Agent Hosting

The agent is hosted using the [Agent Framework](https://github.com/microsoft/agent-framework) with the `ResponsesHostServer`, which provisions a REST API endpoint compatible with the OpenAI Responses protocol.

## Prerequisites

- An Azure AI Foundry project with:
  - A deployed chat model (e.g., `gpt-4.1-mini`)
  - A deployed embedding model (e.g., `text-embedding-3-small`) — used by the memory store itself, not by the agent at runtime
- Azure CLI logged in (`az login`)

### Required RBAC

Your identity (or the Managed Identity running the container in production) needs **Azure AI User** on the Foundry project scope. This single role covers both provisioning the memory store with `provision_memory_store.py` and reading/writing memories from `main.py`.

## Provisioning the memory store (one time)

[`provision_memory_store.py`](provision_memory_store.py) creates a Foundry Memory Store with the user-profile capability enabled (and chat-summary disabled) using `AIProjectClient.beta.memory_stores.create`. It is safe to re-run: if a store with the same name already exists, the script leaves it alone.

From this directory, with the venv activated and `az login` done:

```bash
export FOUNDRY_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4.1-mini"
export AZURE_AI_EMBEDDING_MODEL_DEPLOYMENT_NAME="text-embedding-3-small"
export MEMORY_STORE_NAME="agent_framework_memory"
python provision_memory_store.py
```

Or in PowerShell:

```powershell
$env:FOUNDRY_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
$env:AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4.1-mini"
$env:AZURE_AI_EMBEDDING_MODEL_DEPLOYMENT_NAME="text-embedding-3-small"
$env:MEMORY_STORE_NAME="agent_framework_memory"
python provision_memory_store.py
```

Expected output (first run):

```text
Creating memory store 'agent_framework_memory'...
Created memory store 'agent_framework_memory' (id=memstore_...).
```

> To delete the store manually, call `project.beta.memory_stores.delete("<name>")` on an `AIProjectClient` constructed with `allow_preview=True`.

## Option 1: Azure Developer CLI (`azd`)

### Prerequisites

1. **Azure Developer CLI (`azd`)** — [Install azd](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd)
2. Install the AI agent extension:
   ```bash
   azd ext install microsoft.foundry
   ```
3. Authenticate:
   ```bash
   azd auth login
   ```

### Initialize the agent project

No cloning required. Create a new folder and initialize from the manifest:

```bash
mkdir my-memory-agent && cd my-memory-agent

azd ai agent init -m https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/agent-framework/responses/13-foundry-memory/agent.manifest.yaml
```

Follow the prompts to configure your Foundry project and model deployment. If you don't have an existing Foundry project, `azd ai agent init` will guide you through creating one.

### Provision Azure resources (if needed)

If you don't already have a Foundry project and model deployment:

```bash
azd provision
```

### Run the agent locally

```bash
azd ai agent run
```

The agent host will start on `http://localhost:8088`.

### Invoke the local agent

In a separate terminal, from the project directory:

```bash
azd ai agent invoke --local "Hi"
```

### Deploy to Foundry

Once tested locally, deploy to Microsoft Foundry:

Make sure `MEMORY_STORE_NAME` is set in your `azd` environment:

```bash
azd env set MEMORY_STORE_NAME "agent_framework_memory"
```

```bash
azd deploy
```

For the full deployment guide, see [Deploy a hosted agent](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/deploy-hosted-agent).

The deployed agent's Managed Identity needs **Azure AI User** on the Foundry project to read and write memories at runtime. Make sure you have run `provision_memory_store.py` against the same Foundry project before deploying.

### Invoke the deployed agent

```bash
azd ai agent invoke "Hi"
```

## Option 2: VS Code (Foundry Toolkit)

### Prerequisites

1. **VS Code** with the **[Foundry Toolkit](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.azure-ai-foundry)** extension installed.
2. Sign in to Azure in VS Code.

### Create the project

1. Open the Command Palette (`Ctrl+Shift+P`) and run **Foundry Toolkit: Create Hosted Agent**.
2. Select this sample from the gallery. The extension scaffolds the project into a new workspace and generates `agent.yaml`, `.env`, and `.vscode/tasks.json` + `launch.json` automatically.
3. Complete the **Foundry Project Setup** to pick the subscription and Foundry project (or create a new one).

### Run and debug the agent

Press **F5** to start the agent in debug mode. The agent host will start on `http://localhost:8088`.

### Test with Agent Inspector

1. Open the Command Palette (`Ctrl+Shift+P`) and run **Foundry Toolkit: Open Agent Inspector**.
2. The Inspector connects to the running agent. Send messages to chat and view streamed responses.

### Deploy to Foundry

1. Open the Command Palette (`Ctrl+Shift+P`) and run **Foundry Toolkit: Deploy Hosted Agent**. The extension opens a **Deploy Hosted Agent** wizard and reads `agent.yaml` to auto-populate settings.
2. If prompted, complete **Foundry Project Setup** to select subscription and project.
3. On the **Basics** tab, choose deployment method (**Code** or **Container**) and confirm the agent name.
4. On **Review + Deploy**, confirm runtime details, pick **CPU and Memory** size, and click **Deploy**.
5. After deployment, invoke the agent in the Agent Playground and stream live logs from the **Logs** tab.

## Next steps

- [Quickstart: Create a hosted agent](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent) — end-to-end walkthrough using `azd`
- [Manage hosted agents](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/manage-hosted-agent) — monitor and manage deployed agents
