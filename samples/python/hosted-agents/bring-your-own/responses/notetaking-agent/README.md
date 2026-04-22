# Note-Taking Agent — Python (Responses Protocol)

A note-taking agent built with `azure-ai-agentserver-responses` and Azure OpenAI. Uses function calling to save and retrieve notes, with per-session JSONL persistence accessible via the Session Files API.

## Features

- **Save notes** — natural language commands like "save a note - buy groceries"
- **Retrieve notes** — "show me my notes" returns all saved entries with timestamps
- **Per-session isolation** — each session gets its own note file
- **Streaming responses** — real-time SSE streaming via the Responses protocol
- **Session Files API** — notes stored at `$HOME` are accessible via the platform file API

## Prerequisites

- Python 3.12+
- Azure OpenAI resource with a deployed model (e.g., `gpt-4.1-mini`)
- Azure credentials configured (e.g., `az login`)

## Environment Variables

| Variable | Description | Example |
|---|---|---|
| `FOUNDRY_PROJECT_ENDPOINT` | Foundry project endpoint URL | `https://your-project.services.ai.azure.com/api/projects/your-project` |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | Model deployment name declared in `agent.manifest.yaml` | `gpt-4.1-mini` |

## Run Locally

```bash
# Copy and edit environment file
cp .env.example .env

# Install dependencies
pip install -r requirements.txt

# Start the agent
python main.py
```

## Test with curl

### Save a note

```bash
curl -N -X POST http://localhost:8088/responses \
  -H "Content-Type: application/json" \
  -d '{
    "input": "save a note - book reservation for dinner",
    "stream": true,
    "agent_session_id": "my-session"
  }'
```

### Save another note

```bash
curl -N -X POST http://localhost:8088/responses \
  -H "Content-Type: application/json" \
  -d '{
    "input": "save a note - buy groceries for the weekend",
    "stream": true,
    "agent_session_id": "my-session"
  }'
```

### Get all notes

```bash
curl -N -X POST http://localhost:8088/responses \
  -H "Content-Type: application/json" \
  -d '{
    "input": "show me all my notes",
    "stream": true,
    "agent_session_id": "my-session"
  }'
```

## Deploy

See the [Azure AI Agent Hosting documentation](../../README.md) for deployment instructions.

## File Structure

| File | Description |
|---|---|
| `main.py` | Agent entry point with Responses handler and OpenAI function calling |
| `note_store.py` | Thread-safe per-session JSONL note persistence |
| `requirements.txt` | Python dependencies |
| `Dockerfile` | Container image definition |
| `agent.yaml` | Agent hosting configuration |
| `agent.manifest.yaml` | Agent metadata and template |
| `.dockerignore` | Docker build exclusions |
