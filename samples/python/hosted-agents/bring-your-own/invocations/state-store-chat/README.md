<!-- Begin standard disclaimer — do not modify -->
**IMPORTANT!** All samples and other resources made available in this GitHub repository ("samples") are designed to assist in accelerating development of agents, solutions, and agent workflows for various scenarios. Review all provided resources and carefully test output behavior in the context of your use case. AI responses may be inaccurate and AI actions should be monitored with human oversight. Learn more in the transparency note for [Agent Service](https://learn.microsoft.com/en-us/azure/ai-foundry/responsible-ai/agents/transparency-note).

Agents, solutions, or other output you create may be subject to legal and regulatory requirements, may require licenses, or may not be suitable for all industries, scenarios, or use cases. By using any sample, you are acknowledging that any output created using those samples are solely your responsibility, and that you will comply with all applicable laws, regulations, and relevant safety standards, terms of service, and codes of conduct.

Third-party samples contained in this folder are subject to their own designated terms, and they have not been tested or verified by Microsoft or its affiliates.

Microsoft has no responsibility to you or others with respect to any of these samples or any resulting output.
<!-- End standard disclaimer -->

# What this sample demonstrates

A multi-turn chat hosted agent using the **Bring Your Own** approach with the **Invocations protocol** in Python that persists its conversation history in **Foundry durable storage**. It builds on the [Hello World](../hello-world) sample by replacing the in-memory history dict with [`FoundryStateStore`](https://pypi.org/project/azure-ai-agentserver-core/), so history survives container restarts, scale-out, and redeployments.

The Invocations protocol does **not** provide built-in server-side conversation history. Rather than keep it in memory (lost on every restart), this sample stores it in `FoundryStateStore` — a durable, server-backed key-value store scoped to your Foundry project and reachable from the same `FOUNDRY_PROJECT_ENDPOINT` the agent already uses.

## How It Works

### Durable conversation history

The agent uses [`FoundryStateStore`](https://pypi.org/project/azure-ai-agentserver-core/) from `azure.ai.agentserver.core.storage`:

- **One store per conversation** — named `chat-history/<agent_session_id>`. Encoding the session id into the store name is how you scope data to a conversation; there is no separate session-isolation knob. `FoundryStateStore.get_or_create(name, ...)` resolves (or creates, on first use) the store in a single call.
- **One item holds the transcript** — the full message list is stored as a single item under the key `history`, with value `{"messages": [{"role": ..., "content": ...}, ...]}`. Item values are plain JSON objects (a `dict`), up to 1 MB.
- **Guarded writes** — each turn is appended with an optimistic-concurrency (`if_match`) read-modify-write, so two requests racing on the same session cannot lose each other's messages. On a precondition failure the agent reloads the latest history and retries.
- **TTL** — the store's `item_ttl_seconds` (30 days here) ages out idle conversations automatically; any write renews the window. Set it to `-1` to keep history forever.

On each request the handler loads the persisted history, sends it plus the new user message to the model, streams the reply, then durably appends the completed user/assistant turn before emitting the final `done` event.

See [main.py](src/state-store-chat-python-invocations/main.py) for the full implementation, and the [Durable State Store Guide](https://github.com/Azure/azure-sdk-for-python/blob/main/sdk/agentserver/azure-ai-agentserver-core/docs/state-store-guide.md) for the complete API.

### Model Integration

The agent uses the Foundry SDK to create a Responses client from the project endpoint and model deployment name. When a request arrives, the handler looks up the durable history for the session, appends the new user message, calls the model via the Responses API with streaming, and returns a `StreamingResponse` of SSE events — `token` events during generation, then a final `done` event.

### Agent Hosting

The agent is hosted using the [Azure AI AgentServer Invocations SDK](https://pypi.org/project/azure-ai-agentserver-invocations/), which provisions a REST API endpoint compatible with the Azure AI Invocations protocol.

### Agent Deployment

The hosted agent can be developed and deployed to Microsoft Foundry using the [Azure Developer CLI](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?view=foundry&pivots=azd).

## Running the Agent Locally

### Prerequisites

Before running this sample, ensure you have:

1. **Azure Developer CLI (`azd`)**
   - [Install azd](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd) (1.25 or later) and the unified Foundry CLI extension: `azd ext install microsoft.foundry`
   - Authenticated: `azd auth login`

2. **Azure CLI**
   - Installed and authenticated: `az login`

3. **Python 3.10 or later**
   - Verify your version: `python --version`

> [!NOTE]
> You do **not** need an existing [Microsoft Foundry](https://learn.microsoft.com/en-us/azure/ai-foundry/what-is-foundry?view=foundry) project or model deployment to get started — `azd provision` creates them for you. If you already have a project, see the [note below](#using-azd) on how to target it.

> [!NOTE]
> The durable state store is reached through your Foundry project endpoint using the agent's identity. When running with `azd ai agent run` (locally) or hosted in Foundry, that identity is configured automatically. Running `python main.py` manually uses `DefaultAzureCredential`, so the signed-in `az login` identity needs access to the project.

### Environment Variables

See [`.env.example`](src/state-store-chat-python-invocations/.env.example) or `.env` for the full list of environment variables this sample uses.

| Variable | Required | Description |
|----------|----------|-------------|
| `FOUNDRY_PROJECT_ENDPOINT` | Yes | Foundry project endpoint. Used for both the model call and the durable state store. Auto-injected in hosted containers; set automatically by `azd ai agent run` locally. |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | Yes | Model deployment name — must match your Foundry project deployment. Declared in `azure.yaml`. |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Recommended | Enables telemetry. Auto-injected in hosted containers; set manually for local dev. |

**Local development (without `azd`):**

```bash
cp .env.example .env  # skip if .env already exists
# Edit .env and fill in your values, then:
export $(grep -v '^#' .env | xargs)
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

Run and test hosted agents locally with the Azure Developer CLI (`azd`) or the Foundry Toolkit VS Code extension.

<details>
<summary><h4>Using the Foundry Toolkit VS Code Extension</h4></summary>

**Prerequisites**

1. **VS Code** with the **[Foundry Toolkit](https://marketplace.visualstudio.com/items?itemName=ms-windows-ai-studio.windows-ai-studio)** extension installed.
2. For debugging Python in VS Code, install the **[Python](https://marketplace.visualstudio.com/items?itemName=ms-python.python)** extension pack.

**Set up the Python virtual environment**

- Open the Command Palette (`Ctrl+Shift+P`) and run **Python: Create Environment...** to create a virtual environment in the workspace (or **Python: Select Interpreter** to use an existing one).
- Install dependencies in the virtual environment:

  ```bash
  # use uv to accelerate
  pip install uv
  uv pip install -r requirements.txt

  # or pure pip
  pip install -r requirements.txt
  ```

**Run and debug the agent**

Press **F5** to start the agent. The agent starts and the **Agent Inspector** opens automatically. Chat with the agent in the Inspector.

**Or run manually, then open the Inspector**

1. Set the required environment variables and sign in to Azure with the Azure CLI (`az login`).
2. Start the agent: `python main.py` (listens on `http://localhost:8088`).
3. Command Palette (`Ctrl+Shift+P`) → **Foundry Toolkit: Open Agent Inspector**, then send a message to test.

</details>

#### Using [`azd`](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?view=foundry&pivots=azd)

No cloning required. Create a new folder, point `azd` at the manifest on GitHub, and it sets up the sample and adopts its `azure.yaml` as the project manifest and configures your environment automatically:

```bash
# Create a new folder for the agent and navigate into it
mkdir state-store-chat-agent && cd state-store-chat-agent

# Initialize from the manifest — azd reads it, downloads the sample,
# and adopts its azure.yaml as the project manifest and configures your environment
azd ai agent init -m https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/bring-your-own/invocations/state-store-chat/azure.yaml

# Provision Azure resources (Foundry project, model deployment, App Insights)
azd provision

# Run the agent locally (handles env vars, dependency install, and startup)
azd ai agent run
```

> [!NOTE]
> If you've already cloned this repository, pass a local path to the manifest instead:
> `azd ai agent init -m <path-to-repo>/samples/python/hosted-agents/bring-your-own/invocations/state-store-chat/azure.yaml`

> [!NOTE]
> If you already have a Foundry project and model deployment, add `-p <project-id> -d <deployment-name>` to `azd ai agent init` to target existing resources. You can also skip provisioning entirely and configure env vars manually — see [Manual setup](#manual-setup).

The agent starts on `http://localhost:8088/`. To invoke it:

```bash
azd ai agent invoke --local "My name is Ada. Remember it."
```

Or use curl directly. The `-N` flag disables output buffering so you see SSE tokens as they arrive:

> [!NOTE]
> `agent_session_id` is optional. If omitted, the server auto-generates one and returns it in the `done` event (`session_id` field). To continue a conversation across turns — and reload its durable history — pass the same `agent_session_id` in each request.

```bash
# Turn 1 — start a new conversation
curl -sS -N -X POST "http://localhost:8088/invocations?agent_session_id=chat-001" \
  -H "Content-Type: application/json" \
  -d '{"message": "My name is Ada. Remember it."}'

# Turn 2 — continue the same conversation
curl -sS -N -X POST "http://localhost:8088/invocations?agent_session_id=chat-001" \
  -H "Content-Type: application/json" \
  -d '{"message": "What is my name?"}'
```

**Verify durability:** stop the agent (`Ctrl+C`), start it again, then repeat the "What is my name?" request with the same `agent_session_id=chat-001`. Because history lives in the state store rather than in process memory, the agent still remembers the name — an in-memory sample would have forgotten it.

Each response is a stream of SSE events: `token` events with incremental text, followed by a `done` event with the complete reply.

#### Manual setup

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
azd ai agent invoke "My name is Ada. Remember it."
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

## Customization

- **Scope per user, not just per conversation** — pass `user_isolation=True` to `get_or_create` when the same store name is shared across users and the platform should partition items per user. For trusted callers acting on behalf of an end user, also pass `user_id` to send the delegated `x-ms-user-id` header. See [User Isolation](https://github.com/Azure/azure-sdk-for-python/blob/main/sdk/agentserver/azure-ai-agentserver-core/docs/state-store-guide.md#user-isolation-and-delegated-user-ids).
- **Store more than a transcript** — put checkpoints, tool results, or workflow state in additional items and filter them with `tags` via `list_keys()`.
- **Change retention** — adjust `item_ttl_seconds` (store-level, fixed at creation). Use `-1` to keep history forever.

## Troubleshooting

### Images built on Apple Silicon or other ARM64 machines do not work on our service

**Deploy with `azd deploy`**, which uses ACR remote build and always produces images with the correct architecture.

If you choose to **build locally**, and your machine is **not `linux/amd64`** (for example, an Apple Silicon Mac), the image will **not be compatible with our service**, causing runtime failures.

**Fix for local builds:**

```bash
docker build --platform=linux/amd64 -t image .
```

This forces the image to be built for the required `amd64` architecture.

## Next steps

- [Hello World (Python, Invocations)](../hello-world) — the in-memory starting point this sample builds on.
- [Durable State Store Guide](https://github.com/Azure/azure-sdk-for-python/blob/main/sdk/agentserver/azure-ai-agentserver-core/docs/state-store-guide.md) — full `FoundryStateStore` API: items, tags, TTL, optimistic concurrency, and key listing.
- [Hosted agents overview](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/hosted-agents).
