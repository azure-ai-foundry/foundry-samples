**IMPORTANT!** All samples and other resources made available in this GitHub repository ("samples") are designed to assist in accelerating development of agents, solutions, and agent workflows for various scenarios. Review all provided resources and carefully test output behavior in the context of your use case. AI responses may be inaccurate and AI actions should be monitored with human oversight.

# Note-Taking Agent — Responses Protocol

This sample demonstrates a note-taking agent built with [Azure.AI.AgentServer.Responses](https://www.nuget.org/packages/Azure.AI.AgentServer.Responses) that uses **Azure OpenAI function calling** for intent understanding and **local JSONL file storage** for session-persistent notes.

## How It Works

The agent receives natural language messages via `POST /responses` and uses Azure OpenAI with two tool definitions — `save_note` and `get_notes` — to understand user intent. When the LLM returns a tool call, the agent executes it locally (reads/writes a JSONL file) and sends the result back to the LLM for a natural language response.

Notes are stored per session in `notes_{session_id}.jsonl` files, demonstrating **session persistence** — notes survive across multiple invocations within the same session.

## Running Locally

### Prerequisites

- .NET 10.0 SDK
- Azure CLI installed and authenticated (`az login`)
- Foundry project with a deployed model (e.g., `gpt-4.1-mini`)

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
dotnet build
cp .env.example .env  # then edit values (skip if .env already exists)
export FOUNDRY_PROJECT_ENDPOINT="https://your-project.services.ai.azure.com/api/projects/your-project"
export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4.1-mini"
dotnet run
```

The agent starts on `http://localhost:8088/`.

### Test

#### 1. Test with azd

> **Note:** Run all `azd` commands from this sample directory (where `azure.yaml` is located) so that session state is persisted across calls.

##### Save a note

```bash
azd ai agent invoke --local "save a note - book reservation for dinner"
```

##### Save another note

```bash
azd ai agent invoke --local "save a note - buy groceries"
```

##### Get all notes

```bash
azd ai agent invoke --local "get all my notes"
```

##### Start a new session

```bash
azd ai agent invoke --local --new-session --new-conversation "get all my notes"
```

#### 2. Test with curl

##### Save a note

```bash
curl -X POST http://localhost:8088/responses \
  -H "Content-Type: application/json" \
  -d '{"input": "save a note - book reservation for dinner", "agent_session_id": "my-session"}' \
  --no-buffer
```

##### Get all notes

```bash
curl -X POST http://localhost:8088/responses \
  -H "Content-Type: application/json" \
  -d '{"input": "get all my notes", "agent_session_id": "my-session"}' \
  --no-buffer
```

#### 3. Test in Agent Inspector

Once the agent is running, open **Agent Inspector** in VS Code to interactively send messages and view responses.

![Agent Inspector](../../../../assets/agent-inspector-responses.png)

##### Save a note

```
save a note - book reservation for dinner
```

##### Save another note

```
save a note - buy groceries
```

##### Get all notes

```
get all my notes
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
azd ai agent invoke "save a note - book reservation for dinner"
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
