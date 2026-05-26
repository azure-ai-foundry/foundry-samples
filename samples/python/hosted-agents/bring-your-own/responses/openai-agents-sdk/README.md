# OpenAI Agents SDK — Responses Protocol (Streaming)

**IMPORTANT!** All samples and other resources made available in this GitHub repository ("samples") are designed to assist in accelerating development of agents, solutions, and agent workflows for various scenarios. Review all provided resources and carefully test output behavior in the context of your use case. AI responses may be inaccurate and AI actions should be monitored with human oversight.

A minimal getting-started agent using the [OpenAI Python SDK](https://pypi.org/project/openai/) (Responses API) backed by **Microsoft Foundry** (no OpenAI API key required), with the [azure-ai-agentserver-responses](https://pypi.org/project/azure-ai-agentserver-responses/) protocol.

Authentication uses `DefaultAzureCredential` via `AIProjectClient` — the same pattern used by other Foundry hosted-agent samples.

## How It Works

1. Receives requests via `POST /responses`
2. Reads input from the responses context (`context.get_input_text()`)
3. Reads platform-managed history (`context.get_history()`)
4. Streams text deltas from OpenAI Agents SDK events
5. Returns `TextResponse(...)` so the responses protocol SDK emits the response lifecycle/events

## Environment Variables

| Variable | Required | Description |
| -------- | -------- | ----------- |
| `FOUNDRY_PROJECT_ENDPOINT` | Yes | Foundry project endpoint (auto-injected in hosted containers; set by `azd ai agent run` locally) |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | Yes | Model deployment name in your Foundry project |

## Running Locally

### Prerequisites

- Python 3.10+
- A Microsoft Foundry project with a model deployment (e.g. `gpt-4o-mini`)
- Azure CLI logged in (`az login`) or another credential supported by `DefaultAzureCredential`

### Using `azd` (Recommended)

`azd ai agent run` automatically injects `FOUNDRY_PROJECT_ENDPOINT` and starts the agent:

```bash
azd ai agent run
```

The agent starts on `http://localhost:8088/`.

### Using the Foundry Toolkit VS Code Extension

The [Foundry Toolkit VS Code extension](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?view=foundry&pivots=vscode) has a built-in sample gallery. You can open this sample directly from the extension without cloning the repository, it scaffolds the project into a new workspace, generates `agent.yaml`, `.env`, and `.vscode/tasks.json` + `launch.json` automatically, and configures a one-click **F5** debug experience.

Chat with a running agent using the **Agent Inspector**:

1. Start the agent locally first using **Using `azd`** or **Without `azd`** above. The agent listens on `http://localhost:8088/`.
2. Open the Command Palette (`Ctrl+Shift+P`) and run **Foundry Toolkit: Open Agent Inspector**.
3. The Inspector auto-connects to the running agent. Send messages to chat with the agent and watch the streamed responses.

### Without `azd`

```bash
pip install -r requirements.txt
cp .env.example .env  # then set FOUNDRY_PROJECT_ENDPOINT and AZURE_AI_MODEL_DEPLOYMENT_NAME (skip if .env already exists)
python main.py
```

The agent starts on `http://localhost:8088/`.

## Invoke with azd

### Local

**Bash:**

```bash
azd ai agent invoke --local "What can you help me with?"
```

**PowerShell:**

```powershell
azd ai agent invoke --local '{\"input\": \"What can you help me with?\"}'
```

### Test with curl

```bash
# First message
curl -sS -N -X POST http://localhost:8088/responses \
  -H "Content-Type: application/json" \
  -d '{"input": "What is Microsoft Foundry?", "stream": true}'

# Follow-up (multi-turn — same session remembers context)
curl -sS -N -X POST "http://localhost:8088/responses" \
  -H "Content-Type: application/json" \
  -d '{"input": "What hosted agent options does it offer?", "agent_session_id": "chat-001", "stream": true}'
```

### Streaming Behavior

The responses protocol SDK emits lifecycle and content events automatically when
`TextResponse(...)` is returned.

## Deploying the Agent to Microsoft Foundry

Once you've tested locally, deploy to Microsoft Foundry:

```bash
# Provision Azure resources (skip if already done during local setup)
azd provision

# Build, push, and deploy the agent to Foundry
azd deploy
```

After deploying, invoke the agent running in Foundry:

**Bash:**

```bash
azd ai agent invoke '{"input": "What can you help me with?"}'
```

**PowerShell:**

```powershell
azd ai agent invoke '{\"input\": \"What can you help me with?\"}'
```

To stream logs from the running agent:

```bash
azd ai agent monitor
```

For the full deployment guide, see [Azure AI Foundry hosted agents](https://aka.ms/azdaiagent/docs).

### Deploying with the Foundry Toolkit VS Code Extension

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

### `FOUNDRY_PROJECT_ENDPOINT` not set

```text
EnvironmentError: FOUNDRY_PROJECT_ENDPOINT environment variable is not set.
```

Use `azd ai agent run` which sets this automatically, or set it manually:

```bash
export FOUNDRY_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
```

### `AZURE_AI_MODEL_DEPLOYMENT_NAME` not set

Set it to the name of a model deployment in your Foundry project:

```bash
export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4o-mini"
```

### Authentication failure

Ensure you are logged in with Azure CLI:

```bash
az login
```

`DefaultAzureCredential` tries several credential sources in order. See the [azure-identity docs](https://learn.microsoft.com/python/api/azure-identity/azure.identity.defaultazurecredential) for details.

### No streaming output

Ensure you are using `curl -N` or another streaming-capable HTTP client. The agent uses `text/event-stream` media type.

### Images built on Apple Silicon or other ARM64 machines do not work on our service

We **recommend deploying with `azd deploy`**, which uses ACR remote build and always produces images with the correct architecture.

If you choose to **build locally**, and your machine is **not `linux/amd64`** (for example, an Apple Silicon Mac), the image will **not be compatible with our service**, causing runtime failures.

**Fix for local builds:**

```bash
docker build --platform=linux/amd64 -t image .
```

This forces the image to be built for the required `amd64` architecture.
