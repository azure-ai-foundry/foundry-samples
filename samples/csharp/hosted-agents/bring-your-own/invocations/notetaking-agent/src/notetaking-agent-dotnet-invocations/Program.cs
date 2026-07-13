// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using Azure.AI.AgentServer.Invocations;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using DotNetEnv;
using Microsoft.AspNetCore.Http;
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

InvocationsServer.Run<NoteTakingHandler>(configure: builder =>
{
    builder.Services.AddSingleton(responsesClient);
});

// ──────────────────────────────────────────────────────────────────
// Handler
// ──────────────────────────────────────────────────────────────────

/// <summary>
/// Note-taking agent using the invocations protocol with the Foundry Responses
/// API for function calling. Streams the final reply as SSE events with
/// per-session JSONL persistence.
/// </summary>
public class NoteTakingHandler : InvocationHandler
{
    // Maximum number of tool-call rounds before giving up. Bounds API cost and
    // request latency if the model gets stuck in a tool-call feedback loop.
    private const int MaxToolRounds = 5;

    private const string SystemPrompt =
        "You are a helpful note-taking assistant. You can save notes and retrieve them. " +
        "When the user asks to save a note, extract the note content and call save_note. " +
        "When the user asks to see their notes, call get_notes. " +
        "Always respond in a friendly, concise manner.";

    private static readonly OpenAI.Responses.FunctionTool s_saveNoteTool = ResponseTool.CreateFunctionTool(
        functionName: "save_note",
        functionDescription: "Save a note with the current timestamp. Use this when the user asks to save, add, or create a note.",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "note": {
                    "type": "string",
                    "description": "The note text to save"
                }
            },
            "required": ["note"]
        }
        """),
        strictModeEnabled: false);

    private static readonly OpenAI.Responses.FunctionTool s_getNotesTool = ResponseTool.CreateFunctionTool(
        functionName: "get_notes",
        functionDescription: "Retrieve all saved notes. Use this when the user asks to get, list, show, or view their notes.",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {},
            "required": []
        }
        """),
        strictModeEnabled: false);

    private readonly ProjectResponsesClient _responsesClient;

    public NoteTakingHandler(ProjectResponsesClient responsesClient) => _responsesClient = responsesClient;

    public override async Task HandleAsync(
        HttpRequest request,
        HttpResponse response,
        InvocationContext context,
        CancellationToken cancellationToken)
    {
        string userMessage;
        try
        {
            var input = await request.ReadFromJsonAsync<NoteInput>(cancellationToken);
            userMessage = input?.Message ?? "";
            if (string.IsNullOrWhiteSpace(userMessage))
                throw new JsonException("missing or empty \"message\" field");
        }
        catch (JsonException)
        {
            response.StatusCode = 400;
            await response.WriteAsJsonAsync(
                new
                {
                    error = "invalid_request",
                    message = "Request body must be a JSON object with a non-empty \"message\" string, e.g. {\"message\": \"save a note - book reservation for dinner\"}",
                },
                cancellationToken);
            return;
        }

        var sessionId = context.SessionId;

        // Set up SSE streaming
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";

        var options = new CreateResponseOptions { Instructions = SystemPrompt };
        options.Tools.Add(s_saveNoteTool);
        options.Tools.Add(s_getNotesTool);
        options.InputItems.Add(ResponseItem.CreateUserMessageItem(userMessage));

        // Function-call loop: non-streaming rounds while the model emits tool
        // calls; once tools have been executed, the final reply is streamed
        // token-by-token to the client as SSE events.
        string finalText = "";
        bool toolsExecuted = false;
        for (int round = 0; round < MaxToolRounds; round++)
        {
            if (toolsExecuted)
            {
                await foreach (var update in _responsesClient.CreateResponseStreamingAsync(options, cancellationToken))
                {
                    if (update is StreamingResponseOutputTextDeltaUpdate delta
                        && !string.IsNullOrEmpty(delta.Delta))
                    {
                        finalText += delta.Delta;
                        var tokenEvent = JsonSerializer.Serialize(new { type = "token", content = delta.Delta });
                        await response.WriteAsync($"data: {tokenEvent}\n\n", cancellationToken);
                        await response.Body.FlushAsync(cancellationToken);
                    }
                }
                break;
            }

            var result = await _responsesClient.CreateResponseAsync(options, cancellationToken);
            bool functionCalled = false;

            foreach (var responseItem in result.Value.OutputItems)
            {
                options.InputItems.Add(responseItem);
                if (responseItem is FunctionCallResponseItem functionCall)
                {
                    var toolOutput = ExecuteToolCall(
                        functionCall.FunctionName,
                        functionCall.FunctionArguments,
                        sessionId);
                    options.InputItems.Add(
                        ResponseItem.CreateFunctionCallOutputItem(functionCall.CallId, toolOutput));
                    functionCalled = true;
                }
            }

            if (!functionCalled)
            {
                // No tool calls — emit the complete first-round reply as one token event.
                finalText = result.Value.GetOutputText() ?? "";
                var tokenEvent = JsonSerializer.Serialize(new { type = "token", content = finalText });
                await response.WriteAsync($"data: {tokenEvent}\n\n", cancellationToken);
                break;
            }

            toolsExecuted = true;
        }

        if (string.IsNullOrEmpty(finalText))
        {
            finalText = "(No final response produced — tool-call loop may have exceeded the limit.)";
            var tokenEvent = JsonSerializer.Serialize(new { type = "token", content = finalText });
            await response.WriteAsync($"data: {tokenEvent}\n\n", cancellationToken);
        }

        var doneEvent = JsonSerializer.Serialize(new
        {
            type = "done",
            invocation_id = context.InvocationId,
            session_id = context.SessionId,
            full_text = finalText
        });
        await response.WriteAsync($"data: {doneEvent}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    // ── Tool execution ──

    private static string ExecuteToolCall(string functionName, BinaryData arguments, string sessionId)
    {
        try
        {
            if (functionName == "save_note")
            {
                var args = JsonSerializer.Deserialize<JsonElement>(arguments);
                if (!args.TryGetProperty("note", out var noteProp))
                    return JsonSerializer.Serialize(new { error = "Missing required 'note' argument" });

                var noteText = noteProp.GetString() ?? "";
                var entry = NoteStore.SaveNote(sessionId, noteText);
                return JsonSerializer.Serialize(new { status = "saved", note = entry.Note, timestamp = entry.Timestamp });
            }
            else if (functionName == "get_notes")
            {
                var notes = NoteStore.GetNotes(sessionId);
                return JsonSerializer.Serialize(new { count = notes.Count, notes = notes.Select(n => new { n.Note, n.Timestamp }) });
            }
            return JsonSerializer.Serialize(new { error = $"Unknown function: {functionName}" });
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new { error = $"Invalid tool arguments: {ex.Message}" });
        }
    }
}

// ──────────────────────────────────────────────────────────────────
// Input model
// ──────────────────────────────────────────────────────────────────

public record NoteInput(string Message);
