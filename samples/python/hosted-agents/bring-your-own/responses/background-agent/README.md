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

### Install Dependencies

```bash
pip install -r requirements.txt
```

### Start the Agent

```bash
cp .env.example .env  # then edit values
export FOUNDRY_PROJECT_ENDPOINT="https://your-project.services.ai.azure.com/api/projects/your-project"
export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4.1-mini"
python main.py
```

The agent starts on `http://localhost:8088/`.

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

## Invoke with azd

### Local

**Bash:**
```bash
azd ai agent invoke --local "Analyze the impact of AI on healthcare"
```

**PowerShell:**
```powershell
azd ai agent invoke --local "Analyze the impact of AI on healthcare"
```

### Remote (after `azd up`)

**Bash:**
```bash
azd ai agent invoke "Analyze the impact of AI on healthcare"
```

**PowerShell:**
```powershell
azd ai agent invoke "Analyze the impact of AI on healthcare"
```

## Deploying to Microsoft Foundry

To deploy your agent to Microsoft Foundry, follow the deployment guide at https://github.com/microsoft/hosted-agents-vnext-private-preview/blob/main/azd-quickstart.md
