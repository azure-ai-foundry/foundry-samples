// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using Azure.AI.AgentServer.Invocations;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Chat;

// Derive Azure OpenAI endpoint from the auto-injected Foundry project endpoint
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
    Console.Error.WriteLine(
        "[WARNING] APPLICATIONINSIGHTS_CONNECTION_STRING not set — traces will not be sent " +
        "to Application Insights. Set it to enable local telemetry. " +
        "(This variable is auto-injected in hosted Foundry containers — do not declare it in agent.manifest.yaml.)");

var foundryEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT environment variable is not set.");
var azureOpenAIEndpoint = new Uri(foundryEndpoint).GetLeftPart(UriPartial.Authority);
var deployment = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("AZURE_AI_MODEL_DEPLOYMENT_NAME environment variable is not set.");

var aoaiClient = new AzureOpenAIClient(
    new Uri(azureOpenAIEndpoint),
    new DefaultAzureCredential());
var chatClient = aoaiClient.GetChatClient(deployment);

InvocationsServer.Run<NoteTakingHandler>(configure: builder =>
{
    builder.Services.AddSingleton(chatClient);
});

// ──────────────────────────────────────────────────────────────────
// Handler
// ──────────────────────────────────────────────────────────────────

/// <summary>
/// Note-taking agent using the invocations protocol with Azure OpenAI function calling.
/// Streams responses as SSE events with per-session JSONL persistence.
/// </summary>
public class NoteTakingHandler : InvocationHandler
{
    private readonly ChatClient _chatClient;

    public NoteTakingHandler(ChatClient chatClient) => _chatClient = chatClient;

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

        // Define tools for Azure OpenAI function calling
        var tools = new ChatTool[]
        {
            ChatTool.CreateFunctionTool(
                "save_note",
                "Save a note with the current timestamp. Use this when the user asks to save, add, or create a note.",
                BinaryData.FromString("""
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
                """)),
            ChatTool.CreateFunctionTool(
                "get_notes",
                "Retrieve all saved notes. Use this when the user asks to get, list, show, or view their notes.",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {},
                    "required": []
                }
                """))
        };

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(
                "You are a helpful note-taking assistant. You can save notes and retrieve them. " +
                "When the user asks to save a note, extract the note content and call save_note. " +
                "When the user asks to see their notes, call get_notes. " +
                "Always respond in a friendly, concise manner."),
            new UserChatMessage(userMessage)
        };

        var options = new ChatCompletionOptions();
        foreach (var tool in tools)
            options.Tools.Add(tool);

        // First call — may return tool calls
        var completion = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);

        // If tool calls are requested, execute them and send results back
        if (completion.Value.FinishReason == ChatFinishReason.ToolCalls)
        {
            messages.Add(new AssistantChatMessage(completion.Value));

            foreach (var toolCall in completion.Value.ToolCalls)
            {
                var result = ExecuteToolCall(toolCall.FunctionName, toolCall.FunctionArguments, sessionId);
                messages.Add(new ToolChatMessage(toolCall.Id, result));
            }

            // Second call — stream natural language response
            await StreamResponseAsync(messages, options, response, context, cancellationToken);
        }
        else
        {
            // Direct text response (no tool calls) — stream it
            var text = completion.Value.Content?.FirstOrDefault()?.Text ?? "";
            await StreamTextAsync(text, response, context, cancellationToken);
        }
    }

    // ── Streaming helpers ──

    private async Task StreamResponseAsync(
        List<ChatMessage> messages,
        ChatCompletionOptions options,
        HttpResponse response,
        InvocationContext context,
        CancellationToken cancellationToken)
    {
        var fullText = "";

        await foreach (var update in _chatClient.CompleteChatStreamingAsync(messages, options, cancellationToken))
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    fullText += part.Text;
                    var tokenEvent = JsonSerializer.Serialize(new { type = "token", content = part.Text });
                    await response.WriteAsync($"data: {tokenEvent}\n\n", cancellationToken);
                    await response.Body.FlushAsync(cancellationToken);
                }
            }
        }

        // Send completion event
        var doneEvent = JsonSerializer.Serialize(new
        {
            type = "done",
            invocation_id = context.InvocationId,
            session_id = context.SessionId,
            full_text = fullText
        });
        await response.WriteAsync($"data: {doneEvent}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    private static async Task StreamTextAsync(
        string text,
        HttpResponse response,
        InvocationContext context,
        CancellationToken cancellationToken)
    {
        var words = text.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            var token = i == 0 ? words[i] : $" {words[i]}";
            var tokenEvent = JsonSerializer.Serialize(new { type = "token", content = token });
            await response.WriteAsync($"data: {tokenEvent}\n\n", cancellationToken);
            await response.Body.FlushAsync(cancellationToken);
            await Task.Delay(30, cancellationToken);
        }

        // Send completion event
        var doneEvent = JsonSerializer.Serialize(new
        {
            type = "done",
            invocation_id = context.InvocationId,
            session_id = context.SessionId,
            full_text = text
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
