# What this sample demonstrates

A personal coach hosted agent with persistent per-user memory, the **Foundry Memory RAG Agent (Responses Protocol)** sample shows how to ground answers in user-private memories that survive across requests and across sessions, using `FoundryMemoryProvider` from [Agent Framework](https://github.com/microsoft/agent-framework) on top of the [Foundry Memory](https://learn.microsoft.com/azure/ai-foundry/) service.

## How It Works

The agent registers a `FoundryMemoryProvider` as an `AIContextProvider`. When the user shares training goals, dietary preferences, injuries, or scheduling constraints, the framework writes those facts to a project-scoped Foundry Memory store. On every subsequent turn (and on requests in brand new sessions) the framework retrieves the most relevant memories and injects them as context for the model, which composes its answer grounded in what it already knows about the user.

The store is created on startup via `EnsureMemoryStoreCreatedAsync` (idempotent), so a fresh `azd provision` produces a fully working agent on first invocation.

> [!NOTE]
> Provisioning of the Foundry project, model deployments, and supporting Azure resources is handled by the [`azd-ai-starter-basic`](https://github.com/Azure-Samples/azd-ai-starter-basic) template, which `azd ai agent init` pulls in automatically. The chat and embedding deployments declared under `resources:` in `azure.yaml` flow into the starter's `AI_PROJECT_DEPLOYMENTS` parameter.

> [!NOTE]
> This sample uses a single shared memory scope so any caller writes to and reads from the same partition. Production agents should partition memory per end user using the platform-injected isolation headers. See the comment near `stateInitializer` in [Program.cs](src/foundry-memory-rag/Program.cs) for the pattern that becomes available once the `HostedSessionContext` API ships in a future `Microsoft.Agents.AI.Foundry.Hosting` release.

See [Program.cs](src/foundry-memory-rag/Program.cs) for the full implementation.

## Running the Agent Locally

### Prerequisites

Before running this sample, ensure you have:

1. **Azure Developer CLI (`azd`)**
   - [Install azd](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd) (1.25 or later) and the unified Foundry CLI extension: `azd ext install microsoft.foundry`
   - Authenticated: `azd auth login`

2. **Azure CLI**
   - Installed and authenticated: `az login`

3. **.NET 10.0 SDK or later**
   - Verify your version: `dotnet --version`
   - Download from [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)

> [!NOTE]
> You do **not** need an existing [Microsoft Foundry](https://learn.microsoft.com/en-us/azure/ai-foundry/what-is-foundry?view=foundry) project or model deployment to get started, `azd provision` creates them for you. If you already have a project, see the [note below](#using-azd) on how to target it.

### Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `FOUNDRY_PROJECT_ENDPOINT` | Yes | Foundry project endpoint. Auto-injected in hosted containers; set automatically by `azd ai agent run` locally. |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | Yes | Chat model deployment name. Declared in `azure.yaml`. |
| `AZURE_AI_EMBEDDING_DEPLOYMENT_NAME` | Yes | Embedding model deployment name (used by Foundry Memory). Declared in `azure.yaml`. |
| `AZURE_AI_MEMORY_STORE_ID` | No | Memory store name. Defaults to `foundry-memory-rag-store`. The store is created on startup if it does not exist. |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Recommended | Enables telemetry. Auto-injected in hosted containers; set manually for local dev. |

**Local development (without `azd`):**

```bash
# Set env vars directly. .NET does not natively read .env files.
export FOUNDRY_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
export AZURE_AI_MODEL_DEPLOYMENT_NAME="<your-chat-deployment-name>"
export AZURE_AI_EMBEDDING_DEPLOYMENT_NAME="<your-embedding-deployment-name>"
```

> [!NOTE]
> When using `azd ai agent run`, environment variables are handled automatically. No manual setup needed.

### Installing Dependencies

> [!NOTE]
> If using `azd ai agent run`, dependencies are restored automatically. Skip to [Running the Sample](#running-the-sample).

Dependencies are restored automatically when building the project:

```bash
dotnet restore
```

### Running the Sample

Run and test hosted agents locally with the Azure Developer CLI (`azd`) or the Foundry Toolkit VS Code extension.

<details>
<summary><h4>Using the Foundry Toolkit VS Code Extension</h4></summary>

The [Foundry Toolkit VS Code extension](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?view=foundry&pivots=vscode) has a built-in sample gallery. You can open this sample directly from the extension without cloning the repository, it scaffolds the project into a new workspace, generates `agent.yaml`, `.env`, and `.vscode/tasks.json` + `launch.json` automatically, and configures a one-click **F5** debug experience.

Chat with a running agent using the **Agent Inspector**:

1. Start the agent locally first using **Using `azd`** or **Manual setup** above. The agent listens on `http://localhost:8088/`.
2. Open the Command Palette (`Ctrl+Shift+P`) and run **Foundry Toolkit: Open Agent Inspector**.
3. The Inspector auto-connects to the running agent. Send messages to chat with the agent and watch the streamed responses.

</details>

#### Using [`azd`](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?view=foundry&pivots=azd)

No cloning required. Create a new folder, point `azd` at the manifest on GitHub, and it sets up the sample, adopts its `azure.yaml` as the project manifest and configures your environment:

```bash
# Create a new folder for the agent and navigate into it
mkdir foundry-memory-rag-agent && cd foundry-memory-rag-agent

# Initialize from the manifest. azd reads it, downloads the sample,
# and adopts its azure.yaml as the project manifest and configures your environment
azd ai agent init -m https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/csharp/hosted-agents/agent-framework/foundry-memory-rag/azure.yaml

# Provision Azure resources (Foundry project, chat + embedding deployments, App Insights)
azd provision

# Run the agent locally (handles env vars, build, and startup)
azd ai agent run
```

> [!NOTE]
> If you've already cloned this repository, pass a local path to the manifest instead:
> `azd ai agent init -m <path-to-repo>/samples/csharp/hosted-agents/agent-framework/foundry-memory-rag/azure.yaml`

> [!NOTE]
> If you already have a Foundry project and model deployments, add `-p <project-id> -d <chat-deployment-name>` to `azd ai agent init` to target existing resources. You also need an embedding deployment (default `text-embedding-3-small`); set its name via `AZURE_AI_EMBEDDING_DEPLOYMENT_NAME` if it differs from the default.

The agent starts on `http://localhost:8088/`. Run a few turns to seed memory, then ask the agent to recall:

```bash
azd ai agent invoke --local "Remember that I want to run my first 5k in October and I prefer morning workouts."
azd ai agent invoke --local "I have a sensitive left knee, please avoid high-impact exercises."
azd ai agent invoke --local "What do you already know about my training goals?"
```

Or use curl directly:

```bash
curl -sS -X POST http://localhost:8088/responses \
  -H "Content-Type: application/json" \
  -d '{"input": "Remember that I want to run my first 5k in October and I prefer morning workouts.", "stream": false}' | jq .

curl -sS -X POST http://localhost:8088/responses \
  -H "Content-Type: application/json" \
  -d '{"input": "What do you already know about my training goals?", "stream": false}' | jq .
```

Memory extraction is asynchronous server-side, expect a few seconds between the teaching turn and the recall turn.

#### Manual setup

If running without `azd`, set environment variables manually (see [Environment Variables](#environment-variables)), then:

```bash
dotnet run
```

### Deploying the Agent to Microsoft Foundry

Once you've tested locally, deploy to Microsoft Foundry:

```bash
# Build, push, and deploy the agent to Foundry (also runs provisioning if needed)
azd deploy
```

After deploying, invoke the agent running in Foundry:

```bash
azd ai agent invoke "What do you already know about my training goals?"
```

To stream logs from the running agent:

```bash
azd ai agent monitor
```

For the full deployment guide, see [Azure AI Foundry hosted agents](https://aka.ms/azdaiagent/docs).

#### Deploying with the Foundry Toolkit VS Code Extension

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

## Troubleshooting

### Images built on Apple Silicon or other ARM64 machines do not work on our service

**Deploy with `azd deploy`**, which uses ACR remote build and always produces images with the correct architecture.

If you choose to **build locally**, and your machine is **not `linux/amd64`** (for example, an Apple Silicon Mac), the image will **not be compatible with our service**, causing runtime failures.

**Fix for local builds:**

```bash
docker build --platform=linux/amd64 -t image .
```

This forces the image to be built for the required `amd64` architecture.
