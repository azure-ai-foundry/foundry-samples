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

## Troubleshooting

### Images built on Apple Silicon or other ARM64 machines do not work on our service

We **recommend deploying with `azd deploy`**, which uses ACR remote build and always produces images with the correct architecture.

If you choose to **build locally**, and your machine is **not `linux/amd64`** (for example, an Apple Silicon Mac), the image will **not be compatible with our service**, causing runtime failures.

**Fix for local builds:**

```bash
docker build --platform=linux/amd64 -t image .
```

This forces the image to be built for the required `amd64` architecture.
