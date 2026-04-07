# Foundry Model Router — Chat Completion Sample

Simple "Hello World" Python example showing how to use [Foundry Model Router](https://learn.microsoft.com/azure/foundry/openai/how-to/model-router) with Chat Completions API.

Model Router is a deployable AI chat model in Azure AI Foundry that **automatically selects the best underlying LLM** for each prompt in real time. It delivers high performance and cost savings from a single deployment — you use it just like any other chat model.

## Prerequisites

- **Python 3.9+**
- **Azure subscription** with an Azure OpenAI resource
- **Model Router deployment** — deploy `model-router` from the model catalog in [Microsoft Foundry](https://ai.azure.com/)

3. **Install dependencies**

   ```bash
   pip install -r requirements.txt
   ```

4. **Create your `.env` file**

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
cd chat-completions
python model-router-chat-completions.py
```

## What to Expect

The example prints:
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
