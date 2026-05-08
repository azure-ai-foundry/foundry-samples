# Foundry Model Router — .NET Samples

Simple "Hello World" C# examples showing how to use [Foundry Model Router](https://learn.microsoft.com/azure/foundry/openai/how-to/model-router) across different Azure OpenAI APIs and SDKs.

Model Router is a deployable AI chat model in Azure AI Foundry that **automatically selects the best underlying LLM** for each prompt in real time. It delivers high performance and cost savings from a single deployment — you use it just like any other chat model.

## Examples

| File | API | Auth | Description |
|------|-----|------|-------------|
| [`ModelRouterChatCompletions.cs`](ModelRouterChatCompletions.cs) | Chat Completions | Entra ID | Basic single-prompt chat completion via `ChatClient` |
| [`ModelRouterFoundryResponses.cs`](ModelRouterFoundryResponses.cs) | Foundry SDK | Entra ID | Uses `AIProjectClient` → `GetProjectResponsesClientForModel()` → Responses API |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (or later)
- **Azure subscription** with an Azure OpenAI resource
- **Model Router deployment** — deploy `model-router` from the model catalog in [Microsoft Foundry](https://ai.azure.com/)
- **Azure CLI** installed and logged in (`az login`)

## Setup

1. **Log in to Azure**

   ```bash
   az login
   ```

2. **Configure `appsettings.json`**

   Edit [`appsettings.json`](appsettings.json) with your values:

   ```json
   {
     "AzureOpenAI": {
       "Endpoint": "https://your-resource-name.openai.azure.com/openai/v1",
       "ModelDeploymentName": "model-router"
     },
     "AzureFoundry": {
       "Endpoint": "https://your-ai-services-account-name.services.ai.azure.com/api/projects/your-project-name",
       "ModelDeploymentName": "model-router"
     }
   }
   ```

   - `AzureOpenAI` section is used by `ModelRouterChatCompletions.cs`
   - `AzureFoundry` section is used by `ModelRouterFoundryResponses.cs`

## Run the Examples

These samples use [C# scripting with `dotnet run`](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-10#file-scoped-types). NuGet package references are declared inline via `#:package` directives — no `.csproj` file is needed.

### Chat Completions API

```bash
dotnet run ModelRouterChatCompletions.cs
```

### Foundry Responses SDK

```bash
dotnet run ModelRouterFoundryResponses.cs
```

## What to Expect

Each example prints:

- **Which underlying model** was selected by the router (e.g. `gpt-4.1-mini-2025-04-14`)
- **The model's response** to the prompt
- **Token usage**

The `model` field in the response reveals which LLM the router chose. You control routing behavior (Balanced / Quality / Cost) at deployment time in the Foundry portal — not in code.

## Resources

- [Model Router documentation](https://learn.microsoft.com/azure/foundry/openai/how-to/model-router)
- [Model Router concepts](https://learn.microsoft.com/azure/foundry/openai/concepts/model-router)
- [Azure OpenAI Chat Completions quickstart](https://learn.microsoft.com/azure/ai-foundry/openai/how-to/chatgpt)
- [Azure.AI.Projects SDK (NuGet)](https://www.nuget.org/packages/Azure.AI.Projects)

## License

MIT
