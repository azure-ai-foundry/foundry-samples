// Copyright (c) Microsoft. All rights reserved.

using System.Runtime.CompilerServices;
using Azure.AI.AgentServer.Responses;
using Azure.AI.AgentServer.Responses.Models;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Responses;

// Load environment variables from a .env file if present (for local development).
Env.NoClobber().TraversePath().Load();

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
    Console.Error.WriteLine(
        "[WARNING] APPLICATIONINSIGHTS_CONNECTION_STRING not set — traces will not be sent " +
        "to Application Insights. Set it to enable local telemetry. " +
        "(This variable is auto-injected in hosted Foundry containers — do not declare it in agent.manifest.yaml.)");

var foundryEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT environment variable is not set.");
var deployment = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("AZURE_AI_MODEL_DEPLOYMENT_NAME environment variable is not set.");

var projectClient = new AIProjectClient(new Uri(foundryEndpoint), new DefaultAzureCredential());

// Use the Responses API via the Foundry project client — replaces the legacy
// Azure.AI.OpenAI / AzureOpenAIClient pattern.
var responsesClient = projectClient.ProjectOpenAIClient
    .GetProjectResponsesClientForModel(deployment);

ResponsesServer.Run<BackgroundResearchHandler>(configure: builder =>
{
    builder.Services.AddSingleton(responsesClient);
});

// ──────────────────────────────────────────────────────────────────
// Handler
// ──────────────────────────────────────────────────────────────────

/// <summary>
/// Background research agent using the responses protocol with the Foundry
/// Responses API. Processes requests asynchronously — the SDK handles
/// background mode, polling, and cancellation automatically.
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

    private readonly ProjectResponsesClient _responsesClient;

    public BackgroundResearchHandler(ProjectResponsesClient responsesClient) => _responsesClient = responsesClient;

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

        var options = new CreateResponseOptions { Instructions = SystemPrompt };
        options.InputItems.Add(ResponseItem.CreateUserMessageItem($"Research topic: {userInput}"));

        await foreach (var update in _responsesClient.CreateResponseStreamingAsync(options, cancellationToken))
        {
            if (update is StreamingResponseOutputTextDeltaUpdate delta
                && !string.IsNullOrEmpty(delta.Delta))
            {
                yield return delta.Delta;
            }
        }
    }
}
