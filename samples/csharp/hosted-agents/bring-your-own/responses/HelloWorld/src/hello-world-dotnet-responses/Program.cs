// Copyright (c) Microsoft. All rights reserved.

/*
 * Hello World — Bring Your Own Responses agent for C#
 *
 * Minimal hosted agent that forwards user input to a Foundry model via the
 * Responses API and returns the reply through the Responses protocol.
 *
 * This sample demonstrates the simplest possible BYO integration: the protocol
 * SDK (Azure.AI.AgentServer.Responses) handles the HTTP contract and SSE
 * lifecycle, and you supply the model call using the Foundry SDK
 * (Azure.AI.Projects and Azure.AI.Extensions.OpenAI).
 *
 * Conversation history is retrieved via ResponseContext.GetHistoryAsync() and
 * included in each model call so the agent maintains context across turns.
 *
 * Required environment variables:
 *   FOUNDRY_PROJECT_ENDPOINT  — Foundry project endpoint (auto-injected in hosted containers)
 *   AZURE_AI_MODEL_DEPLOYMENT_NAME     — Model deployment name (declared in agent.manifest.yaml)
 *
 * Usage:
 *   dotnet run
 *
 *   # Invoke the agent (in a separate terminal):
 *   curl -sS -X POST http://localhost:8088/responses \
 *     -H "Content-Type: application/json" \
 *     -d '{"input": "What is Microsoft Foundry?", "stream": false}' | jq .
 */

using Azure.AI.AgentServer.Responses;
using Azure.AI.AgentServer.Responses.Models;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using OpenAI.Responses;

// One-liner startup — wires up Kestrel on port 8088, OpenTelemetry, health probes,
// and the Responses API endpoints. Telemetry is configured automatically:
// when APPLICATIONINSIGHTS_CONNECTION_STRING is set, traces and logs are sent to
// Application Insights with no extra code.
ResponsesServer.Run<HelloWorldHandler>(configure: builder =>
{
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
        Console.Error.WriteLine(
            "[WARNING] APPLICATIONINSIGHTS_CONNECTION_STRING not set — traces will not be sent " +
            "to Application Insights. Set it to enable local telemetry. " +
            "(This variable is auto-injected in hosted Foundry containers — do not declare it in agent.manifest.yaml.)");

    var endpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
        ?? throw new InvalidOperationException(
            "FOUNDRY_PROJECT_ENDPOINT environment variable is not set.");

    var model = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME")
        ?? throw new InvalidOperationException(
            "AZURE_AI_MODEL_DEPLOYMENT_NAME environment variable is not set.");

    var projectClient = new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential());

    // Use the Responses client — not GetChatClient() (Chat Completions API)
    var responsesClient = projectClient.ProjectOpenAIClient
        .GetProjectResponsesClientForModel(model);

    builder.Services.AddSingleton(responsesClient);
});

/// <summary>
/// Hello World handler — forwards user input to a Foundry model via the Responses API.
/// Conversation history is fetched via <see cref="ResponseContext.GetHistoryAsync"/> and
/// included in each model call so the agent maintains context across conversation turns.
/// </summary>
/// <param name="responsesClient">Foundry Responses API client, injected via DI.</param>
/// <param name="logger">Logger injected via DI. Calls are automatically exported to Application Insights.</param>
public sealed class HelloWorldHandler(
    ProjectResponsesClient responsesClient,
    ILogger<HelloWorldHandler> logger) : ResponseHandler
{
    private const string SystemPrompt = "You are a helpful AI assistant. Be concise and informative.";

    public override IAsyncEnumerable<ResponseStreamEvent> CreateAsync(
        CreateResponse request,
        ResponseContext context,
        CancellationToken cancellationToken)
    {
        // TextResponse wraps the result text in the full SSE lifecycle:
        // response.created → response.in_progress → content events → response.completed
        return new TextResponse(context, request,
            createText: ct => GenerateTextAsync(context, ct));
    }

    private async Task<string> GenerateTextAsync(
        ResponseContext context,
        CancellationToken cancellationToken)
    {
        var userInput = await context.GetInputTextAsync(cancellationToken: cancellationToken) ?? "Hello!";
        var history = await context.GetHistoryAsync(cancellationToken);

        logger.LogInformation("Processing request {ResponseId}", context.ResponseId);

        var options = new CreateResponseOptions
        {
            Instructions = SystemPrompt,
        };

        // Reconstruct conversation history for the model.
        // GetHistoryAsync walks the previous_response_id chain and returns items oldest-first.
        // Each OutputItemMessage can contain both user input content and assistant output content.
        foreach (var item in history)
        {
            if (item is OutputItemMessage { Content: { } contents })
            {
                foreach (var content in contents)
                {
                    switch (content)
                    {
                        case MessageContentOutputTextContent { Text: { } assistantText }:
                            // Assistant message from a previous turn
                            options.InputItems.Add(ResponseItem.CreateAssistantMessageItem(assistantText));
                            break;
                        case MessageContentInputTextContent { Text: { } userText }:
                            // User message from a previous turn
                            options.InputItems.Add(ResponseItem.CreateUserMessageItem(userText));
                            break;
                    }
                }
            }
        }

        // Add the current user message
        options.InputItems.Add(ResponseItem.CreateUserMessageItem(userInput));

        var result = await responsesClient.CreateResponseAsync(options);
        return result.Value.GetOutputText() ?? string.Empty;
    }
}
