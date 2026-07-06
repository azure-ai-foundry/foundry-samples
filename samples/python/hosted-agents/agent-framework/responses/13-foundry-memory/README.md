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

See [main.py](src/agent-framework-agent-foundry-memory-responses/main.py) for the full implementation.

### Agent Hosting

The agent is hosted using the [Agent Framework](https://github.com/microsoft/agent-framework) with the `ResponsesHostServer`, which provisions a REST API endpoint compatible with the OpenAI Responses protocol.

## Prerequisites

- An Azure AI Foundry project with:
  - A deployed chat model (e.g., `gpt-5.4-mini`)
  - A deployed embedding model (e.g., `text-embedding-3-small`) — used by the memory store itself, not by the agent at runtime
- Azure CLI logged in (`az login`)

### Required RBAC

Your identity (or the Managed Identity running the container in production) needs **Azure AI User** on the Foundry project scope. This role covers provisioning the memory store with `provision_memory_store.py` and reading/writing memories from `main.py`.

The memory store embeds and retrieves memories through the project's inference endpoint, so the same identity also needs **Cognitive Services OpenAI User** on the Foundry project scope to call the embedding deployment. Without it, memory writes fail with a `401` (`Authentication to the Azure OpenAI resource failed`) and the store stays empty. When deploying, grant both roles to the hosted agent's runtime identity (the `…-AgentIdentity` service principal) at the project scope.


## Option 1: Azure Developer CLI (`azd`)

With the bundled `postprovision` hook, a single `azd provision` creates the Foundry Memory Store and sets `MEMORY_STORE_NAME` for you.

### 1. Install prerequisites

1. **Azure Developer CLI (`azd`)** — [Install azd](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd) (1.25 or later)
2. Install the unified Foundry CLI extension bundle:
   ```bash
   azd ext install microsoft.foundry
   ```
3. Authenticate:
   ```bash
   azd auth login
   ```

### 2. Initialize the agent project

No cloning required. Create a new folder and initialize from the manifest:

```bash
mkdir my-memory-agent && cd my-memory-agent

azd ai agent init -m https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/agent-framework/responses/13-foundry-memory/azure.yaml
```

Follow the prompts to configure your Foundry project and model deployment. If you don't have an existing Foundry project, `azd ai agent init` will guide you through creating one. Initializing also sets the selected project as the active project, and copies this sample's files into a new service directory `src/<agent-name>/` — including [`provision_memory_store.py`](src/agent-framework-agent-foundry-memory-responses/provision_memory_store.py) and the [`hooks/`](hooks/) scripts.

### 3. Enable one-command provisioning (`postprovision` hook)

Wire the bundled hook into the `azure.yaml` that `azd ai agent init` generated, so the memory store is created automatically every time you run `azd provision`. `postprovision` must be registered at the **top level** of `azure.yaml` (service-scoped hooks only support the package/deploy lifecycle), and the `run:` path must point at the hook inside the generated service directory. Add this top-level block, replacing `<agent-name>` with the service folder `azd ai agent init` created under `src/`:

```yaml
hooks:
  postprovision:
    posix:
      shell: sh
      run: ./src/<agent-name>/hooks/postprovision.sh
    windows:
      shell: pwsh
      run: ./src/<agent-name>/hooks/postprovision.ps1
```

The hook ([`hooks/postprovision.sh`](hooks/postprovision.sh) / [`hooks/postprovision.ps1`](hooks/postprovision.ps1)) runs everything the [manual steps](#provision-manually-without-the-hook) below would, in one shot. It locates its own directory, so it works no matter where `azd` runs it from.

### 4. Provision

Point the hook at an embedding model deployment in your Foundry project (it powers the store's semantic memory, not the agent at runtime), then provision:

```bash
azd env set AZURE_AI_EMBEDDING_MODEL_DEPLOYMENT_NAME "text-embedding-3-small"
azd provision
```

`azd provision` creates (or reuses) your Foundry project and chat model deployment, then the `postprovision` hook:

1. Runs [`provision_memory_store.py`](src/agent-framework-agent-foundry-memory-responses/provision_memory_store.py) to create the Foundry Memory Store (user-profile capability enabled, chat-summary disabled) and verifies it on the service.
2. Sets `MEMORY_STORE_NAME` so the agent reads and writes that store. It persists the name both to the `azd` environment (for `azd ai agent run`) and into the agent service in `azure.yaml` (so `azd deploy` ships it to the container — `azd ai agent init` resolves `${MEMORY_STORE_NAME}` to an empty value at init time, before the store name is known).

> The hook defaults `MEMORY_STORE_NAME` to `agent_framework_memory`. To use a different name, set it first: `azd env set MEMORY_STORE_NAME "<your-store-name>"`.

### 5. Run the agent locally

```bash
azd ai agent run
```

The agent host will start on `http://localhost:8088`.

### Invoke the local agent

In a separate terminal, from the project directory:

```bash
azd ai agent invoke --local "Hi, my name is Alex and I'm vegetarian."
```

### Deploy to Foundry

```bash
azd deploy
```

For the full deployment guide, see [Deploy a hosted agent](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/deploy-hosted-agent).

The deployed agent's Managed Identity needs **Azure AI User** on the Foundry project to read and write memories at runtime. The `postprovision` hook already created the memory store against that same project.

### Invoke the deployed agent

```bash
azd ai agent invoke "Do you remember my name and what I like to eat?"
```

### Provision manually (without the hook)

Prefer to run the step yourself (or skip the hook)? [`provision_memory_store.py`](src/agent-framework-agent-foundry-memory-responses/provision_memory_store.py) creates a Foundry Memory Store with the user-profile capability enabled (and chat-summary disabled) using `AIProjectClient.beta.memory_stores.create`. It is safe to re-run: if a store with the same name already exists, the script leaves it alone.

From the project directory, with the venv activated and `az login` done:

```bash
pip install azure-ai-projects azure-identity aiohttp python-dotenv

export FOUNDRY_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-5.4-mini"
export AZURE_AI_EMBEDDING_MODEL_DEPLOYMENT_NAME="text-embedding-3-small"
export MEMORY_STORE_NAME="agent_framework_memory"
python provision_memory_store.py
```

In PowerShell, use `$env:NAME="value"` instead of `export`. Then point the agent at the same store name:

```bash
azd env set MEMORY_STORE_NAME "agent_framework_memory"
```

Expected output (first run):

```text
Creating memory store 'agent_framework_memory'...
Created memory store 'agent_framework_memory' (id=memstore_...).
```

> To delete the store manually, call `project.beta.memory_stores.delete("<name>")` on an `AIProjectClient` constructed with `allow_preview=True`.

## Option 2: VS Code (Foundry Toolkit)

> The VS Code flow doesn't run the `azd` hook — provision the memory store first with [Provision manually](#provision-manually-without-the-hook).

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
