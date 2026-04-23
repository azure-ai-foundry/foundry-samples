**IMPORTANT!** All samples and other resources made available in this GitHub repository ("samples") are designed to assist in accelerating development of agents, solutions, and agent workflows for various scenarios. Review all provided resources and carefully test output behavior in the context of your use case. AI responses may be inaccurate and AI actions should be monitored with human oversight.

# Note-Taking Agent — Invocations Protocol

This sample demonstrates a note-taking agent built with [Azure.AI.AgentServer.Invocations](https://www.nuget.org/packages/Azure.AI.AgentServer.Invocations) that uses **Azure OpenAI function calling** for intent understanding, **SSE streaming** for real-time token delivery, and **local JSONL file storage** for session-persistent notes.

## How It Works

The agent receives natural language messages via `POST /invocations` and uses Azure OpenAI with two tool definitions — `save_note` and `get_notes` — to understand user intent. When the LLM returns a tool call, the agent executes it locally (reads/writes a JSONL file) and streams the LLM's natural language response back as Server-Sent Events.

Notes are stored per session in `notes_{session_id}.jsonl` files, demonstrating **session persistence** — notes survive across multiple invocations within the same session. The session ID is resolved automatically from the `agent_session_id` query parameter.

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

### Without `azd`

```bash
dotnet build
cp .env.example .env  # then edit values
export FOUNDRY_PROJECT_ENDPOINT="https://your-project.services.ai.azure.com/api/projects/your-project"
export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4.1-mini"
dotnet run
```

The agent starts on `http://localhost:8088/`.

### Test

#### 1. Test with azd

**Bash:**
```bash
azd ai agent invoke --local '{"message": "save a note - book reservation for dinner"}'
```

**PowerShell:**
```powershell
azd ai agent invoke --local '{\"message\": \"save a note - book reservation for dinner\"}'
```

#### 2. Test with curl

##### Save a note

```bash
curl -N -X POST "http://localhost:8088/invocations?agent_session_id=my-session" \
  -H "Content-Type: application/json" \
  -d '{"message": "save a note - book reservation for dinner"}'
```

##### Save another note

```bash
curl -N -X POST "http://localhost:8088/invocations?agent_session_id=my-session" \
  -H "Content-Type: application/json" \
  -d '{"message": "save a note - buy groceries"}'
```

##### Get all notes

```bash
curl -N -X POST "http://localhost:8088/invocations?agent_session_id=my-session" \
  -H "Content-Type: application/json" \
  -d '{"message": "get all my notes"}'
```

##### Start a new session

```bash
curl -N -X POST "http://localhost:8088/invocations?agent_session_id=new-session" \
  -H "Content-Type: application/json" \
  -d '{"message": "get all my notes"}'
```

#### 3. Test in Agent Inspector

Once the agent is running, open **Agent Inspector** in VS Code to interactively send messages and view responses.

![Agent Inspector](../../../../assets/agent-inspector-invocations.png)

##### Save a note

```json
{"message": "save a note - book reservation for dinner"}
```

##### Save another note

```json
{"message": "save a note - buy groceries"}
```

##### Get all notes

```json
{"message": "get all my notes"}
```

##### Start a new session

Click the **Clear Conversation** button at the top-right corner to start a new session.

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
azd ai agent invoke '{"message": "save a note - book reservation for dinner"}'
```

**PowerShell:**
```powershell
azd ai agent invoke '{\"message\": \"save a note - book reservation for dinner\"}'
```

To stream logs from the running agent:

```bash
azd ai agent monitor
```

For the full deployment guide, see [Azure AI Foundry hosted agents](https://aka.ms/azdaiagent/docs).

## Troubleshooting

### Azure OpenAI Permission Denied (401)

If you see an error like:

```
Error calling Azure OpenAI: Error code: 401 - {'error': {'code': 'PermissionDenied', 'message': 'The principal <principal-id> lacks the required data action Microsoft.CognitiveServices/accounts/OpenAI/deployments/chat/completions/action to perform POST /openai/deployments/{deployment-id}/chat/completions operation.'}}
```

The identity running the agent does not have the required RBAC roles on the Azure AI Foundry project. Assign the following roles:

- **Cognitive Services OpenAI User**
- **Azure AI User**

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

# Assign "Azure AI User" role
az role assignment create \
  --assignee "$PRINCIPAL_ID" \
  --role "Azure AI User" \
  --scope "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.MachineLearningServices/workspaces/$PROJECT_NAME"
```

> **Note:** It may take a few minutes for role assignments to propagate. Retry the request after waiting.
