**IMPORTANT!** All samples and other resources made available in this GitHub repository ("samples") are designed to assist in accelerating development of agents, solutions, and agent workflows for various scenarios. Review all provided resources and carefully test output behavior in the context of your use case. AI responses may be inaccurate and AI actions should be monitored with human oversight.

# Human-in-the-Loop Agent — Invocations Protocol

This sample demonstrates a human-in-the-loop agent built with [azure-ai-agentserver-invocations](https://pypi.org/project/azure-ai-agentserver-invocations/) that implements an **approval-gate pattern**. The agent generates a proposal using Azure OpenAI, pauses for human review, and resumes execution after the human approves, requests a revision, or rejects.

Session state is persisted as JSON files in the `$HOME` directory, so proposals survive agent restarts and are accessible via the **Session Files API** when deployed to Azure.

This pattern is useful for workflows where an AI agent should **not act autonomously** — for example, drafting communications, generating code changes, or proposing decisions that require human sign-off.

## How It Works

```
[new task] ──► AWAITING_APPROVAL ──► (approve) ──► COMPLETED
                    │
                    ├──► (revise + feedback) ──► AWAITING_APPROVAL (loop)
                    │
                    └──► (reject) ──► REJECTED
```

1. **Submit a task** via `POST /invocations` — the agent calls Azure OpenAI to generate a proposal and returns it with status `awaiting_approval`.
2. **The agent pauses** — the proposal is saved in memory, and the human can return at any time (minutes, hours, or days later).
3. **Respond with a decision** via another `POST /invocations` using the same `agent_session_id`:
   - `approve` — the agent marks the proposal as final and returns it.
   - `revise` (with feedback) — the agent generates an improved proposal incorporating the feedback.
   - `reject` — the agent marks the session as rejected.
4. **Poll status** via `GET /invocations/{id}` — useful for checking whether a proposal is still pending after reconnecting.

## OpenAPI Spec

The agent includes an inline OpenAPI 3.0 specification that documents the request/response contract. It is served at:

```
GET http://localhost:8088/invocations/docs/openapi.json
```

## Running Locally

### Prerequisites

- Python 3.10+
- Azure CLI installed and authenticated (`az login`)
- Azure OpenAI resource with a deployed model

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
cp .env.example .env  # then edit values
export FOUNDRY_PROJECT_ENDPOINT="https://your-project.services.ai.azure.com/api/projects/your-project"
export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4.1-mini"
python main.py
```

The agent starts on `http://localhost:8088/`.

## Invoke with azd

### Local

**Bash:**
```bash
azd ai agent invoke --local '{"task": "Write a product launch announcement for Azure AI Foundry"}'
```

**PowerShell:**
```powershell
azd ai agent invoke --local '{\"task\": \"Write a product launch announcement for Azure AI Foundry\"}'
```

### Remote (after `azd up`)

**Bash:**
```bash
azd ai agent invoke '{"task": "Write a product launch announcement for Azure AI Foundry"}'
```

**PowerShell:**
```powershell
azd ai agent invoke '{\"task\": \"Write a product launch announcement for Azure AI Foundry\"}'
```

### Test with curl

```bash
# Fetch the OpenAPI spec
curl http://localhost:8088/invocations/docs/openapi.json

# Step 1: Submit a task — agent generates a proposal
curl -X POST "http://localhost:8088/invocations?agent_session_id=session-1" \
  -H "Content-Type: application/json" \
  -d '{"task": "Draft a marketing email for our new AI product launch"}'
# -> {"status": "awaiting_approval", "proposal": "...", "session_id": "session-1", ...}

# Step 2: Check status (e.g., after reconnecting hours later)
curl http://localhost:8088/invocations/<invocation_id>
# -> {"status": "awaiting_approval", "proposal": "...", ...}

# Step 3a: Approve the proposal
curl -X POST "http://localhost:8088/invocations?agent_session_id=session-1" \
  -H "Content-Type: application/json" \
  -d '{"decision": "approve"}'
# -> {"status": "completed", "final_output": "...", ...}

# Step 3b: Or request a revision with feedback
curl -X POST "http://localhost:8088/invocations?agent_session_id=session-1" \
  -H "Content-Type: application/json" \
  -d '{"decision": "revise", "feedback": "Make the tone more casual and add a call-to-action"}'
# -> {"status": "awaiting_approval", "proposal": "<revised draft>", ...}

# Step 3c: Or reject
curl -X POST "http://localhost:8088/invocations?agent_session_id=session-1" \
  -H "Content-Type: application/json" \
  -d '{"decision": "reject"}'
# -> {"status": "rejected", ...}

# Cancel a pending session
curl -X POST http://localhost:8088/invocations/<invocation_id>/cancel
# -> {"status": "cancelled", ...}
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
azd ai agent invoke '{"task": "Write a product launch announcement for Azure AI Foundry"}'
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

### Azure OpenAI Permission Denied (401)

If you see an error like:

```
Error calling Azure OpenAI: Error code: 401 - {'error': {'code': 'PermissionDenied', 'message': 'The principal <principal-id> lacks the required data action Microsoft.CognitiveServices/accounts/OpenAI/deployments/chat/completions/action to perform POST /openai/deployments/{deployment-id}/chat/completions operation.'}}
```

The identity running the agent does not have the required RBAC roles on the Azure AI Foundry project. Assign the following roles:

- **Cognitive Services OpenAI User**
- **Foundry User**

Use the Azure CLI to assign them:

```bash
# Set your variables
SUBSCRIPTION_ID="<your-subscription-id>"
RESOURCE_GROUP="<your-resource-group>"
PROJECT_NAME="<your-ai-foundry-project-name>"
PRINCIPAL_ID="<principal-id-from-error-message>"

# Assign "Cognitive Services OpenAI User" role
az role assignment create \
  --assignee "$PRINCIPAL_ID" \
  --role "Cognitive Services OpenAI User" \
  --scope "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.MachineLearningServices/workspaces/$PROJECT_NAME"

# Assign "Foundry User" role
az role assignment create \
  --assignee "$PRINCIPAL_ID" \
  --role "Foundry User" \
  --scope "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.MachineLearningServices/workspaces/$PROJECT_NAME"
```

> **Note:** It may take a few minutes for role assignments to propagate. Retry the request after waiting.
