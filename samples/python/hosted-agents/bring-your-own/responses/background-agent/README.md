**IMPORTANT!** All samples and other resources made available in this GitHub repository ("samples") are designed to assist in accelerating development of agents, solutions, and agent workflows for various scenarios. Review all provided resources and carefully test output behavior in the context of your use case. AI responses may be inaccurate and AI actions should be monitored with human oversight.

# Background Agent — Responses Protocol (Long-Running)

This sample demonstrates a long-running agent built with [azure-ai-agentserver-responses](https://pypi.org/project/azure-ai-agentserver-responses/) that uses the background execution mode for asynchronous processing. It calls Azure OpenAI to generate a multi-section research analysis, streaming LLM tokens as they arrive via the Responses API event lifecycle.

## How It Works

The agent receives a request via `POST /responses` with `"background": true`. The server returns immediately while the handler calls Azure OpenAI in the background, streaming response tokens as `text.delta` events. The caller polls `GET /responses/{id}` until the response reaches a terminal status (`completed`, `failed`, or `incomplete`). In-flight requests can be cancelled via `POST /responses/{id}/cancel`.

## Running Locally

### Prerequisites

- Python 3.12+
- Azure CLI installed and authenticated (`az login`)
- Foundry project with a deployed model

### Using `azd` (Recommended)

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
cp .env.example .env  # then edit values (skip if .env already exists)
export FOUNDRY_PROJECT_ENDPOINT="https://your-project.services.ai.azure.com/api/projects/your-project"
export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4.1-mini"
python main.py
```

The agent starts on `http://localhost:8088/`.

## Invoke with azd

### Local

```bash
azd ai agent invoke --local "Analyze the impact of AI on healthcare"
```

### Remote (after `azd up`)

```bash
azd ai agent invoke "Analyze the impact of AI on healthcare"
```

### Test — Background Mode

```bash
# Submit a background research analysis
curl -X POST http://localhost:8088/responses \
  -H "Content-Type: application/json" \
  -d '{"model": "research", "input": "Analyze the impact of AI on healthcare", "background": true, "store": true}'

# Poll for result (use the id from the response)
curl http://localhost:8088/responses/<response_id>

# Cancel an in-flight request
curl -X POST http://localhost:8088/responses/<response_id>/cancel
```

### Test — Default Mode (Synchronous)

```bash
curl -X POST http://localhost:8088/responses \
  -H "Content-Type: application/json" \
  -d '{"model": "research", "input": "Analyze the impact of AI on healthcare"}'
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
azd ai agent invoke "Analyze the impact of AI on healthcare"
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
