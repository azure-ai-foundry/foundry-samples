---
page_type: sample
languages:
- csharp
products:
- ai-services
- azure
description: Foundry Local C# samples — chat, embeddings, audio, tool calling, model management, web server, and tutorials.
---

# 🚀 Foundry Local C# Samples

These samples demonstrate how to use the [Foundry Local](https://learn.microsoft.com/azure/foundry-local/) C# SDK. Each sample uses a **unified project file** that automatically detects your operating system and selects the optimal NuGet package:

- **Windows**: Uses [`Microsoft.AI.Foundry.Local.WinML`](https://www.nuget.org/packages/Microsoft.AI.Foundry.Local.WinML) for hardware acceleration via Windows ML.
- **macOS / Linux**: Uses [`Microsoft.AI.Foundry.Local`](https://www.nuget.org/packages/Microsoft.AI.Foundry.Local) for cross-platform support.

Both packages provide the same APIs, so the same source code works on all platforms.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Foundry Local](https://learn.microsoft.com/azure/foundry-local/) installed on your machine

## Samples

| Sample | Description |
|---|---|
| [native-chat-completions](native-chat-completions/) | Initialize the SDK, download a model, and run chat completions. |
| [embeddings](embeddings/) | Generate single and batch text embeddings using the Foundry Local SDK. |
| [audio-transcription-example](audio-transcription-example/) | Transcribe audio files using the Foundry Local SDK. |
| [foundry-local-web-server](foundry-local-web-server/) | Start a local OpenAI-compatible web server. |
| [tool-calling-foundry-local-sdk](tool-calling-foundry-local-sdk/) | Use tool calling with native chat completions. |
| [tool-calling-foundry-local-web-server](tool-calling-foundry-local-web-server/) | Use tool calling with the local web server. |
| [model-management-example](model-management-example/) | Manage models, variant selection, and updates. |
| [live-audio-transcription](live-audio-transcription/) | Real-time microphone-to-text using NAudio (Windows). |
| [verify-winml](verify-winml/) | Verify WinML 2.0 execution providers are correctly discovered, downloaded, and registered (Windows only). |
| [tutorial-chat-assistant](tutorial-chat-assistant/) | Build an interactive chat assistant (tutorial). |
| [tutorial-document-summarizer](tutorial-document-summarizer/) | Summarize documents with AI (tutorial). |
| [tutorial-tool-calling](tutorial-tool-calling/) | Create a tool-calling assistant (tutorial). |
| [tutorial-voice-to-text](tutorial-voice-to-text/) | Transcribe and summarize audio (tutorial). |

## Running a sample

1. Clone the repository:

   ```bash
   git clone https://github.com/microsoft-foundry/foundry-samples.git
   cd foundry-samples/samples/csharp/foundry-local
   ```

2. Run a sample:

   ```bash
   cd native-chat-completions
   dotnet run
   ```

> [!TIP]
> Shared helpers used by multiple samples live under [`Shared/`](Shared/). Central package versions are managed by [`Directory.Packages.props`](Directory.Packages.props), and the [`nuget.config`](nuget.config) restricts package sources to `nuget.org`.
