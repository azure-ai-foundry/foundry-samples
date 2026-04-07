# Foundry Model Router — API & SDK Samples

Simple "Hello World" Python examples showing how to use [Foundry Model Router](https://learn.microsoft.com/azure/foundry/openai/how-to/model-router) across different Azure OpenAI APIs and SDKs.

Model Router is a deployable AI chat model in Azure AI Foundry that **automatically selects the best underlying LLM** for each prompt in real time. It delivers high performance and cost savings from a single deployment — you use it just like any other chat model.

## Examples

| Folder | API | Auth | Description |
|--------|-----|------|-------------|
| [`chat-completions/`](chat-completions/) | Chat Completions | API Key | Basic single-prompt chat completion via `AzureOpenAI` client |
| [`foundry-responses-sdk/`](foundry-responses-sdk/) | Foundry SDK | Entra ID | Uses `AIProjectClient` → `get_openai_client()` → Responses API |

## Prerequisites

- **Python 3.9+**
- **Azure subscription** with an Azure OpenAI resource
- **Model Router deployment** — deploy `model-router` from the model catalog in [Microsoft Foundry](https://ai.azure.com/)
- For the Foundry SDK example only: **Azure CLI** installed and logged in (`az login`)

## Setup

1. **Create a virtual environment** (recommended)

   ```bash
   python -m venv .venv
   # Windows
   .venv\Scripts\activate
   # macOS/Linux
   source .venv/bin/activate
   ```

2. **Install dependencies**

   ```bash
   pip install -r requirements.txt
   ```

3. **Create your `.env` file**

   ```bash
   cp .env.sample .env
   ```

   Edit `.env` with your values:

   ```
   AZURE_OPENAI_ENDPOINT=https://your-resource-name.openai.azure.com/
   AZURE_OPENAI_API_KEY=your-api-key-here
   MODEL_DEPLOYMENT_NAME=model-router
   AZURE_AI_PROJECT_ENDPOINT=https://your-ai-services-account-name.services.ai.azure.com/api/projects/your-project-name
   ```

## Run the Examples

### Chat Completions API

```bash
python model-router-chat-completions.py
```

### Foundry Responses SDK (Entra ID)

```bash
az login
python model-router-foundry-responses-sdk.py
```

## What to Expect

Each example prints:
- **Which underlying model** was selected by the router (e.g. `gpt-4.1-mini-2025-04-14`)
- **The model's response** to the prompt
- Token usage

The `model` field in the response reveals which LLM the router chose. You control routing behavior (Balanced / Quality / Cost) at deployment time in the Foundry portal — not in code.

## Resources

- [Model Router documentation](https://learn.microsoft.com/azure/foundry/openai/how-to/model-router)
- [Model Router concepts](https://learn.microsoft.com/azure/foundry/openai/concepts/model-router)
- [Azure OpenAI Chat Completions quickstart](https://learn.microsoft.com/azure/ai-foundry/openai/how-to/chatgpt)
- [Azure AI Projects SDK (PyPI)](https://pypi.org/project/azure-ai-projects/)

## License

MIT
