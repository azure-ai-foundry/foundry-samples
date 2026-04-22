// Copyright (c) Microsoft. All rights reserved.

using System.Runtime.CompilerServices;
using Azure.AI.AgentServer.Responses;
using Azure.AI.AgentServer.Responses.Models;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Chat;

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
    Console.Error.WriteLine(
        "[WARNING] APPLICATIONINSIGHTS_CONNECTION_STRING not set — traces will not be sent " +
        "to Application Insights. Set it to enable local telemetry. " +
        "(This variable is auto-injected in hosted Foundry containers — do not declare it in agent.manifest.yaml.)");

// Derive Azure OpenAI endpoint from the auto-injected Foundry project endpoint
var foundryEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT environment variable is required.");
var azureOpenAIEndpoint = new Uri(foundryEndpoint).GetLeftPart(UriPartial.Authority);
var deployment = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("AZURE_AI_MODEL_DEPLOYMENT_NAME environment variable is required.");

var aoaiClient = new AzureOpenAIClient(
    new Uri(azureOpenAIEndpoint),
    new DefaultAzureCredential());
var chatClient = aoaiClient.GetChatClient(deployment);

ResponsesServer.Run<BackgroundResearchHandler>(configure: builder =>
{
    builder.Services.AddSingleton(chatClient);
});

// ──────────────────────────────────────────────────────────────────
// Handler
// ──────────────────────────────────────────────────────────────────

/// <summary>
/// Background research agent using the responses protocol with Azure OpenAI.
/// Processes requests asynchronously — the SDK handles background mode,
/// polling, and cancellation automatically.
/// </summary>
public class BackgroundResearchHandler : ResponseHandler
{
    private const string SystemPrompt =
        "You are a research analyst. When given a topic, produce a thorough " +
        "multi-section analysis report. Include:\n" +
        "1. Executive Summary\n" +
        "2. Background & Context\n" +
        "3. Key Findings (at least 3)\n" +
        "4. Implications & Recommendations\n" +
        "5. Conclusion\n\n" +
        "Be detailed and substantive. Target 500-800 words.";

    private readonly ChatClient _chatClient;

    public BackgroundResearchHandler(ChatClient chatClient) => _chatClient = chatClient;

    public override IAsyncEnumerable<ResponseStreamEvent> CreateAsync(
        CreateResponse request,
        ResponseContext context,
        CancellationToken cancellationToken)
    {
        return new TextResponse(context, request,
            createTextStream: ct => StreamResearchAsync(context, ct));
    }

    private async IAsyncEnumerable<string> StreamResearchAsync(
        ResponseContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var userInput = await context.GetInputTextAsync(cancellationToken: cancellationToken)
            ?? "General AI trends analysis";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(SystemPrompt),
            new UserChatMessage($"Research topic: {userInput}")
        };

        await foreach (var update in _chatClient.CompleteChatStreamingAsync(messages, cancellationToken: cancellationToken))
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    yield return part.Text;
                }
            }
        }
    }
}
