# Note-Taking Agent — Python (Invocations Protocol)

A note-taking agent built with `azure-ai-agentserver-invocations` and Azure OpenAI. Uses function calling to save and retrieve notes, with per-session JSONL persistence accessible via the Session Files API.

## Features

- **Save notes** — natural language commands like "save a note - buy groceries"
- **Retrieve notes** — "show me my notes" returns all saved entries with timestamps
- **Per-session isolation** — each session gets its own note file
- **Streaming responses** — real-time SSE streaming via the Invocations protocol
- **Session Files API** — notes stored at `$HOME` are accessible via the platform file API

## Prerequisites

- Python 3.12+
- Azure OpenAI resource with a deployed model (e.g., `gpt-4.1-mini`)
- Azure credentials configured (e.g., `az login`)

## Environment Variables

| Variable | Description | Example |
|---|---|---|
| `FOUNDRY_PROJECT_ENDPOINT` | Foundry project endpoint (auto-injected when deployed) | `https://account.services.ai.azure.com/api/projects/proj` |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | Model deployment name | `gpt-4.1-mini` |

## Run Locally

### Using `azd` (Recommended)

```bash
azd ai agent run
```

### Without `azd`

```bash
# Install dependencies
pip install -r requirements.txt

# Set environment variables
export FOUNDRY_PROJECT_ENDPOINT="https://account.services.ai.azure.com/api/projects/proj"
export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4.1-mini"

# Start the agent
python main.py
```

## Test with azd

**Bash:**
```bash
azd ai agent invoke --local '{"message": "save a note - book reservation for dinner"}'
```

**PowerShell:**
```powershell
azd ai agent invoke --local '{\"message\": \"save a note - book reservation for dinner\"}'
```

## Test with curl

### Save a note

```bash
curl -N -X POST "http://localhost:8088/invocations?agent_session_id=my-session" \
  -H "Content-Type: application/json" \
  -d '{"message": "save a note - book reservation for dinner"}'
```

### Save another note

```bash
curl -N -X POST "http://localhost:8088/invocations?agent_session_id=my-session" \
  -H "Content-Type: application/json" \
  -d '{"message": "save a note - buy groceries for the weekend"}'
```

### Get all notes

```bash
curl -N -X POST "http://localhost:8088/invocations?agent_session_id=my-session" \
  -H "Content-Type: application/json" \
  -d '{"message": "show me all my notes"}'
```

### New session (isolated)

```bash
curl -N -X POST "http://localhost:8088/invocations?agent_session_id=another-session" \
  -H "Content-Type: application/json" \
  -d '{"message": "show me my notes"}'
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
azd ai agent invoke '{"message": "save a note - book reservation for dinner"}'
```

To stream logs from the running agent:

```bash
azd ai agent monitor
```

For the full deployment guide, see [Azure AI Foundry hosted agents](https://aka.ms/azdaiagent/docs).

## File Structure

| File | Description |
|---|---|
| `main.py` | Agent entry point with Invocations handler and OpenAI function calling |
| `note_store.py` | Thread-safe per-session JSONL note persistence |
| `requirements.txt` | Python dependencies |
| `Dockerfile` | Container image definition with SSL cert support |
| `agent.yaml` | Agent hosting configuration |
| `agent.manifest.yaml` | Agent metadata and template |
| `.dockerignore` | Docker build exclusions |

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