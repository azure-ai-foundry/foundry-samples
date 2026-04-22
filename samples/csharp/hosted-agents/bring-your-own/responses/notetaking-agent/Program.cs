// Copyright (c) Microsoft. All rights reserved.

using System.Runtime.CompilerServices;
using System.Text.Json;
using Azure.AI.AgentServer.Responses;
using Azure.AI.AgentServer.Responses.Models;
using Azure.AI.OpenAI;
using Azure.Identity;
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

ResponsesServer.Run<NoteTakingHandler>(configure: builder =>
{
    builder.Services.AddSingleton(new LlmConfig(chatClient));
});

// ──────────────────────────────────────────────────────────────────
// Handler
// ──────────────────────────────────────────────────────────────────

public class NoteTakingHandler : ResponseHandler
{
    private readonly LlmConfig _llm;

    public NoteTakingHandler(LlmConfig llm) => _llm = llm;

    public override IAsyncEnumerable<ResponseStreamEvent> CreateAsync(
        CreateResponse request,
        ResponseContext context,
        CancellationToken cancellationToken)
    {
        return new TextResponse(context, request,
            createTextStream: ct => ProcessAsync(request, context, ct));
    }

    private async IAsyncEnumerable<string> ProcessAsync(
        CreateResponse request,
        ResponseContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var userMessage = await context.GetInputTextAsync(cancellationToken: cancellationToken) ?? "";
        var sessionId = request.AgentSessionId ?? "default";

        await foreach (var token in ProcessWithLlmAsync(userMessage, sessionId, cancellationToken))
        {
            yield return token;
        }
    }

    // ── LLM mode: Azure OpenAI with function calling ──

    private async IAsyncEnumerable<string> ProcessWithLlmAsync(
        string userMessage,
        string sessionId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
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
        var completion = await _llm.ChatClient.CompleteChatAsync(messages, options, cancellationToken);

        // If tool calls are requested, execute them and send results back
        if (completion.Value.FinishReason == ChatFinishReason.ToolCalls)
        {
            messages.Add(new AssistantChatMessage(completion.Value));

            foreach (var toolCall in completion.Value.ToolCalls)
            {
                var result = ExecuteToolCall(toolCall.FunctionName, toolCall.FunctionArguments, sessionId);
                messages.Add(new ToolChatMessage(toolCall.Id, result));
            }

            // Second call — get natural language response
            var finalCompletion = await _llm.ChatClient.CompleteChatAsync(messages, options, cancellationToken);

            var response = finalCompletion.Value.Content[0].Text ?? "";
            foreach (var word in SplitIntoTokens(response))
            {
                yield return word;
                await Task.Delay(30, cancellationToken);
            }
        }
        else
        {
            // Direct text response (no tool calls)
            var response = completion.Value.Content[0].Text ?? "";
            foreach (var word in SplitIntoTokens(response))
            {
                yield return word;
                await Task.Delay(30, cancellationToken);
            }
        }
    }

    // ── Helpers ──

    private static string ExecuteToolCall(string functionName, BinaryData arguments, string sessionId)
    {
        if (functionName == "save_note")
        {
            var args = JsonSerializer.Deserialize<JsonElement>(arguments);
            var noteText = args.GetProperty("note").GetString() ?? "";
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

    private static IEnumerable<string> SplitIntoTokens(string text)
    {
        var words = text.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            yield return i == 0 ? words[i] : $" {words[i]}";
        }
    }
}

// ──────────────────────────────────────────────────────────────────
// Config record for DI
// ──────────────────────────────────────────────────────────────────

public record LlmConfig(ChatClient ChatClient);
