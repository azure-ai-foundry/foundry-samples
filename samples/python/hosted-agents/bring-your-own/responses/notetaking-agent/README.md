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

### Using `azd` (Recommended)

```bash
azd ai agent run
```

### Without `azd`

```bash
# Copy and edit environment file
cp .env.example .env

# Install dependencies
pip install -r requirements.txt

# Start the agent
python main.py
```

## Test with azd

```bash
azd ai agent invoke --local "save a note - book reservation for dinner"
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

## Troubleshooting

### Images built on Apple Silicon or other ARM64 machines do not work on our service

We **recommend deploying with `azd deploy`**, which uses ACR remote build and always produces images with the correct architecture.

If you choose to **build locally**, and your machine is **not `linux/amd64`** (for example, an Apple Silicon Mac), the image will **not be compatible with our service**, causing runtime failures.

**Fix for local builds:**

```bash
docker build --platform=linux/amd64 -t image .
```

This forces the image to be built for the required `amd64` architecture.
