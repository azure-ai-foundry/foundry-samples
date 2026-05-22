# LangGraph Multi-turn Chat Agent

A multi-turn conversational agent built with [LangGraph](https://langchain-ai.github.io/langgraph/)
and Azure OpenAI, hosted via the **invocations** protocol.

## What it demonstrates

- **LangGraph agent graph** with conditional tool-calling routing
- **Two built-in tools**: `get_current_time` and `calculator`
- **Multi-turn conversations** via `agent_session_id` (in-memory session store)
- **SSE streaming** output over the invocations protocol
- **Azure OpenAI** with `DefaultAzureCredential` authentication

## Architecture

```
┌───────┐    ┌─────────┐    ┌───────┐
│ START │───▶│ chatbot  │───▶│  END  │
└───────┘    └────┬─────┘    └───────┘
                  │ tool_calls?
                  ▼
             ┌─────────┐
             │  tools   │
             └────┬─────┘
                  │
                  └──▶ chatbot (loop)
```

## Prerequisites

- Python 3.12+
- Azure OpenAI resource with a deployed model (e.g., `gpt-4.1-mini`)
- Azure CLI login (`az login`) or other `DefaultAzureCredential` source

## Environment variables

| Variable | Required | Default | Description |
|---|---|---|---|
| `FOUNDRY_PROJECT_ENDPOINT` | Yes | — | Foundry project endpoint URL |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | Yes | — | Model deployment name declared in `agent.manifest.yaml` |

## Running locally

### Using `azd` (Recommended)

```bash
azd ai agent run
```

### Using the Foundry Toolkit VS Code Extension

The [Foundry Toolkit VS Code extension](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?view=foundry&pivots=vscode) has a built-in sample gallery. You can open this sample directly from the extension without cloning the repository, it scaffolds the project into a new workspace, generates `agent.yaml`, `.env`, and `.vscode/tasks.json` + `launch.json` automatically, and configures a one-click **F5** debug experience.

Chat with a running agent using the **Agent Inspector**:

1. Start the agent locally first using **Using `azd`** or **Without `azd`** above. The agent listens on `http://localhost:8088/`.
2. Open the Command Palette (`Ctrl+Shift+P`) and run **Foundry Toolkit: Open Agent Inspector**.
3. The Inspector auto-connects to the running agent. Send messages to chat with the agent and watch the streamed responses.

### Without `azd`

```bash
cp .env.example .env  # then edit values
pip install -r requirements.txt
python main.py
```

## Testing with azd

**Bash:**
```bash
azd ai agent invoke --local '{"message": "What time is it right now?"}'
```

**PowerShell:**
```powershell
azd ai agent invoke --local '{\"message\": \"What time is it right now?\"}'
```

## Testing with curl

```bash
# Turn 1 — ask for the time (triggers tool call)
curl -N -X POST 'http://localhost:8088/invocations?agent_session_id=s1' \
    -H 'Content-Type: application/json' \
    -d '{"message": "What time is it right now?"}'

# Turn 2 — ask a math question (triggers calculator tool)
curl -N -X POST 'http://localhost:8088/invocations?agent_session_id=s1' \
    -H 'Content-Type: application/json' \
    -d '{"message": "What is 42 * 17?"}'

# Turn 3 — follow-up (uses conversation context, no tools)
curl -N -X POST 'http://localhost:8088/invocations?agent_session_id=s1' \
    -H 'Content-Type: application/json' \
    -d '{"message": "Add 100 to that result"}'
```

## Deploying the Agent to Microsoft Foundry

Once you've tested locally, deploy to Microsoft Foundry:

```bash
# Provision Azure resources (skip if already done during local setup)
azd provision

# Build, push, and deploy the agent to Foundry
azd deploy
```

After deploying, invoke the agent running in Foundry:

```bash
azd ai agent invoke '{"message": "What time is it right now?"}'
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

### Images built on Apple Silicon or other ARM64 machines do not work on our service

We **recommend deploying with `azd deploy`**, which uses ACR remote build and always produces images with the correct architecture.

If you choose to **build locally**, and your machine is **not `linux/amd64`** (for example, an Apple Silicon Mac), the image will **not be compatible with our service**, causing runtime failures.

**Fix for local builds:**

```bash
docker build --platform=linux/amd64 -t image .
```

This forces the image to be built for the required `amd64` architecture.
