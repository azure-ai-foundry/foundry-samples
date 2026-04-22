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

```bash
cp .env.example .env  # then edit values
pip install -r requirements.txt
python main.py
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

## Deploying to Azure AI Agent Hosting

```bash
azd ai agent init -m agent.manifest.yaml
azd up
```
