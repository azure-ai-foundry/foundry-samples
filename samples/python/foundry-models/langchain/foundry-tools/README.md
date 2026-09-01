# Foundry Model Web Search

This sample creates a LangChain agent backed by a model deployed in Microsoft Foundry. The agent uses the server-side Web Search tool based on Bing groundings to stream an answer using current public web information and citations.

## Prerequisites

- Python 3.10 or later
- A Microsoft Foundry project with a supported model deployment
- The **Foundry User** role on the project
- Azure CLI authentication (`az login`)

## Setup

```bash
python -m venv .venv
# Windows
.venv\Scripts\activate
# macOS/Linux
source .venv/bin/activate

pip install -r requirements.txt
cp .env.sample .env
```

Set these values in `.env`:

```text
FOUNDRY_PROJECT_ENDPOINT=https://your-resource.services.ai.azure.com/api/projects/your-project
MODEL_DEPLOYMENT_NAME=gpt-5.4-mini
```

## Run

```bash
az login
python web-search.py
```

## Resources

- [Web search with Azure OpenAI](https://learn.microsoft.com/azure/foundry/openai/how-to/web-search)
- [Microsoft Foundry provider for LangChain](https://docs.langchain.com/oss/python/integrations/providers/microsoft)