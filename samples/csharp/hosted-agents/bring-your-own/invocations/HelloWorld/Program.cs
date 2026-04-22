// Copyright (c) Microsoft. All rights reserved.

/*
 * Hello World — Bring Your Own Invocations agent for C#
 *
 * Minimal hosted agent that forwards user input to a Foundry model via the
 * Responses API and returns the reply through the Invocations protocol
 * as a streaming SSE event stream.
 *
 * This sample demonstrates the simplest possible BYO integration: the protocol
 * SDK (Azure.AI.AgentServer.Invocations) handles the HTTP contract and session
 * resolution, and you supply the model call using the Foundry SDK
 * (Azure.AI.Projects and Azure.AI.Extensions.OpenAI).
 *
 * Unlike the Responses protocol, the Invocations protocol does NOT provide
 * built-in server-side conversation history. This agent maintains an in-memory
 * session store keyed by agent_session_id. In production, replace it with
 * durable storage (Redis, Cosmos DB, etc.) so history survives restarts.
 *
 * Required environment variables:
 *   FOUNDRY_PROJECT_ENDPOINT  — Foundry project endpoint (auto-injected in hosted containers)
 *   AZURE_AI_MODEL_DEPLOYMENT_NAME     — Model deployment name (declared in agent.manifest.yaml)
 *
 * Usage:
 *   dotnet run
 *
 *   # Turn 1 — start a new conversation:
 *   curl -sS -N -X POST "http://localhost:8088/invocations?agent_session_id=chat-001" \
 *     -H "Content-Type: application/json" \
 *     -d '{"message": "What is Microsoft Foundry?"}'
 *
 *   # Turn 2 — continue the same conversation:
 *   curl -sS -N -X POST "http://localhost:8088/invocations?agent_session_id=chat-001" \
 *     -H "Content-Type: application/json" \
 *     -d '{"message": "What hosted agent options does it offer?"}'
 */

using System.Collections.Concurrent;
using System.Text.Json;
using Azure.AI.AgentServer.Invocations;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OpenAI.Responses;

// One-liner startup — wires up Kestrel on port 8088, OpenTelemetry, health probes,
// and the Invocations API endpoints. Telemetry is configured automatically:
// when APPLICATIONINSIGHTS_CONNECTION_STRING is set, traces and logs are sent to
// Application Insights with no extra code.
InvocationsServer.Run<HelloWorldHandler>(configure: builder =>
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

    // Use the Responses API — not GetChatClient() (Chat Completions API is legacy).
    var responsesClient = projectClient.ProjectOpenAIClient
        .GetProjectResponsesClientForModel(model);

    builder.Services.AddSingleton(responsesClient);
});

/// <summary>
/// Hello World handler — forwards user input to a Foundry model via the Responses API,
/// streams the reply as SSE token events, and persists conversation history
/// in an in-memory session store keyed by <see cref="InvocationContext.SessionId"/>.
/// </summary>
/// <param name="responsesClient">Foundry Responses API client, injected via DI.</param>
/// <param name="logger">Logger injected via DI. Calls are automatically exported to Application Insights.</param>
public sealed class HelloWorldHandler(
    ProjectResponsesClient responsesClient,
    ILogger<HelloWorldHandler> logger) : InvocationHandler
{
    private const string SystemPrompt = "You are a helpful AI assistant. Be concise and informative.";

    // In-memory session store keyed by agent_session_id.
    // State is lost on restart; use durable storage in production.
    // Note: the inner List<SessionMessage> is not thread-safe — concurrent
    // requests on the same session_id are not supported in this sample.
    private static readonly ConcurrentDictionary<string, List<SessionMessage>> s_sessions = new();

    // ── Required override ─────────────────────────────────────────────────────
    // HandleAsync is the only method you must override. It receives every
    // POST /invocations request.
    //
    // Three optional overrides exist for long-running operations (LRO):
    //   GetAsync          — handle GET /invocations/{id} status polls
    //   CancelAsync       — handle DELETE /invocations/{id} cancellation
    //   GetOpenApiAsync   — serve an OpenAPI spec at GET /invocations/docs/openapi.json
    // For a simple streaming agent like this one, none of them are needed.
    // ─────────────────────────────────────────────────────────────────────────
    public override async Task HandleAsync(
        HttpRequest request,
        HttpResponse response,
        InvocationContext context,
        CancellationToken cancellationToken)
    {
        // Parse the incoming message — accepts JSON or plain text (see ParseUserMessage).
        var rawBody = await new StreamReader(request.Body).ReadToEndAsync(cancellationToken);
        var userMessage = ParseUserMessage(rawBody);
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            response.StatusCode = 400;
            await response.WriteAsJsonAsync(
                new
                {
                    error = "invalid_request",
                    message = "Request body must be a JSON object with a non-empty \"message\" string (e.g. {\"message\": \"What is Microsoft Foundry?\"}) or a non-empty plain-text body.",
                },
                cancellationToken);
            return;
        }

        // InvocationContext is provided by the Invocations SDK. It resolves
        // session and invocation identity from the incoming request headers
        // so you don't have to parse them yourself.
        logger.LogInformation(
            "Processing invocation {InvocationId} (session {SessionId})",
            context.InvocationId, context.SessionId);

        // Retrieve or create conversation history for this session.
        var history = s_sessions.GetOrAdd(context.SessionId, _ => []);

        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";

        // Build the Responses API input from history + current user message.
        // History is stored as SessionMessage records — convert to ResponseItem.
        var inputItems = new List<ResponseItem>();
        foreach (var msg in history)
        {
            inputItems.Add(msg.Role == "user"
                ? ResponseItem.CreateUserMessageItem(msg.Content)
                : ResponseItem.CreateAssistantMessageItem(msg.Content));
        }
        inputItems.Add(ResponseItem.CreateUserMessageItem(userMessage));
        // Record the user message before streaming so history is consistent
        // even if the model call fails partway through.
        history.Add(new SessionMessage("user", userMessage));

        // Stream tokens from the model via the Responses API.
        var options = new CreateResponseOptions { Instructions = SystemPrompt };
        foreach (var item in inputItems)
        {
            options.InputItems.Add(item);
        }

        var fullText = "";
        await foreach (var update in responsesClient.CreateResponseStreamingAsync(
            options, cancellationToken))
        {
            if (update is StreamingResponseOutputTextDeltaUpdate delta
                && !string.IsNullOrEmpty(delta.Delta))
            {
                fullText += delta.Delta;
                var tokenEvent = JsonSerializer.Serialize(
                    new { type = "token", content = delta.Delta });
                await response.WriteAsync($"data: {tokenEvent}\n\n", cancellationToken);
                await response.Body.FlushAsync(cancellationToken);
            }
        }

        // Send the final done event with the complete reply text.
        var doneEvent = JsonSerializer.Serialize(new
        {
            type = "done",
            invocation_id = context.InvocationId,
            session_id = context.SessionId,
            full_text = fullText,
        });
        await response.WriteAsync($"data: {doneEvent}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);

        // Persist the assistant reply to session history.
        history.Add(new SessionMessage("assistant", fullText));
    }

    /// <summary>
    /// Extracts the user message from the request body.
    /// Accepts <c>{"message": "..."}</c> JSON, <c>{"input": "..."}</c> JSON
    /// (alternate Foundry portal format), or a plain text body.
    /// </summary>
    private static string? ParseUserMessage(string rawBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            return
                (root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null)
                ?? (root.TryGetProperty("input", out var inp) && inp.ValueKind == JsonValueKind.String ? inp.GetString() : null)
                ?? rawBody.Trim();
        }
        catch (JsonException)
        {
            // Not JSON — treat the whole body as plain text.
            return rawBody.Trim();
        }
    }
}

/// <summary>Internal session message record — stores role and content for history tracking.</summary>
public record SessionMessage(string Role, string Content);
