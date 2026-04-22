// Agent Framework toolbox agent using toolbox MCP endpoint in Microsoft Foundry.
//
// Connects to an toolbox MCP endpoint in Microsoft Foundry, discovers tools via
// tools/list, and exposes them through Azure OpenAI function calling. Incoming
// user messages are processed with the LLM, and when the model requests a tool
// call, it is forwarded to the toolbox MCP endpoint via tools/call.
//
// Usage:
//   export FOUNDRY_PROJECT_ENDPOINT=https://<account>.services.ai.azure.com/api/projects/<project>
//   export MODEL_DEPLOYMENT_NAME=gpt-4.1
//   export TOOLBOX_ENDPOINT=https://<account>.services.ai.azure.com/api/projects/<project>/toolboxes/<name>/mcp?api-version=v1
//   dotnet run

using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Azure.AI.AgentServer.Responses;
using Azure.AI.AgentServer.Responses.Models;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Chat;

// ── Configuration ─────────────────────────────────────────────────────────

var projectEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("Set FOUNDRY_PROJECT_ENDPOINT");
var deployment = Environment.GetEnvironmentVariable("MODEL_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("Set MODEL_DEPLOYMENT_NAME");
var toolboxEndpoint = Environment.GetEnvironmentVariable("TOOLBOX_ENDPOINT");

// Derive Azure OpenAI endpoint from the project endpoint (strip /api/projects/...)
var projectUri = new Uri(projectEndpoint);
var openAiEndpoint = $"{projectUri.Scheme}://{projectUri.Host}";

if (string.IsNullOrEmpty(toolboxEndpoint))
    Console.Error.WriteLine(
        "WARNING: TOOLBOX_ENDPOINT is not set. The agent will run without toolbox tools. "
        + "Set this variable (platform-injected at runtime) to enable toolbox integration.");

// ── Azure OpenAI client ──────────────────────────────────────────────────

var credential = new DefaultAzureCredential();
var aoaiClient = new AzureOpenAIClient(new Uri(openAiEndpoint), credential);
var chatClient = aoaiClient.GetChatClient(deployment);

// ── Toolbox MCP client ───────────────────────────────────────────────────

var toolboxClient = !string.IsNullOrEmpty(toolboxEndpoint)
    ? new ToolboxMcpClient(toolboxEndpoint, credential)
    : null;

ResponsesServer.Run<ToolboxHandler>(configure: builder =>
{
    builder.Services.AddSingleton(new AgentConfig(chatClient, toolboxClient));
});

// ═══════════════════════════════════════════════════════════════════════════
// Config record
// ═══════════════════════════════════════════════════════════════════════════
public record AgentConfig(ChatClient ChatClient, ToolboxMcpClient? ToolboxClient);

// ═══════════════════════════════════════════════════════════════════════════
// Response handler
// ═══════════════════════════════════════════════════════════════════════════
public class ToolboxHandler : ResponseHandler
{
    private readonly AgentConfig _config;

    public ToolboxHandler(AgentConfig config) => _config = config;

    public override IAsyncEnumerable<ResponseStreamEvent> CreateAsync(
        CreateResponse request,
        ResponseContext context,
        CancellationToken cancellationToken)
    {
        return new TextResponse(context, request,
            createTextStream: ct => ProcessAsync(context, ct));
    }

    private async IAsyncEnumerable<string> ProcessAsync(
        ResponseContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var userMessage = await context.GetInputTextAsync(cancellationToken: cancellationToken) ?? "Hello!";

        // Discover tools from the toolbox MCP endpoint
        var chatTools = _config.ToolboxClient != null
            ? await _config.ToolboxClient.GetChatToolsAsync()
            : new List<ChatTool>();

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(
                "You are a helpful assistant with access to toolbox tools in Microsoft Foundry. " +
                "Use the available tools to help answer user questions."),
            new UserChatMessage(userMessage),
        };

        var options = new ChatCompletionOptions();
        foreach (var tool in chatTools)
            options.Tools.Add(tool);

        // Tool-calling loop (max 5 rounds)
        for (int round = 0; round < 5; round++)
        {
            var completion = await _config.ChatClient.CompleteChatAsync(messages, options, cancellationToken);
            var result = completion.Value;

            if (result.FinishReason == ChatFinishReason.ToolCalls)
            {
                var assistantMsg = new AssistantChatMessage(result);
                messages.Add(assistantMsg);

                foreach (var toolCall in result.ToolCalls)
                {
                    Console.WriteLine($"  Tool call: {toolCall.FunctionName}({toolCall.FunctionArguments})");
                    var toolResult = _config.ToolboxClient != null
                        ? await _config.ToolboxClient.CallToolAsync(
                            toolCall.FunctionName,
                            toolCall.FunctionArguments.ToString())
                        : "{\"error\": \"Toolbox not configured\"}";
                    messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                }
                continue;
            }

            // Final text response
            foreach (var part in result.Content)
            {
                if (part.Kind == ChatMessageContentPartKind.Text)
                    yield return part.Text;
            }
            yield break;
        }

        yield return "Reached maximum tool-calling rounds.";
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// Toolbox MCP HTTP client
// ═══════════════════════════════════════════════════════════════════════════
public class ToolboxMcpClient
{
    private readonly string? _endpoint;
    private readonly DefaultAzureCredential _credential;
    private List<McpToolDefinition>? _cachedTools;

    public ToolboxMcpClient(string? endpoint, DefaultAzureCredential credential)
    {
        _endpoint = endpoint;
        _credential = credential;
    }

    private async Task<string> GetTokenAsync()
    {
        var result = await _credential.GetTokenAsync(
            new Azure.Core.TokenRequestContext(new[] { "https://ai.azure.com/.default" }));
        return result.Token;
    }

    private async Task<HttpClient> CreateHttpClientAsync()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        var token = await GetTokenAsync();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        http.DefaultRequestHeaders.Add("Foundry-Features", "Toolboxes=V1Preview");
        return http;
    }

    public async Task<List<ChatTool>> GetChatToolsAsync()
    {
        if (string.IsNullOrEmpty(_endpoint))
            return new List<ChatTool>();

        if (_cachedTools != null)
            return _cachedTools.Select(t => t.ToChatTool()).ToList();

        using var http = await CreateHttpClientAsync();
        var payload = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/list",
            @params = new { }
        });

        var resp = await http.PostAsync(_endpoint,
            new StringContent(payload, Encoding.UTF8, "application/json"));
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var tools = doc.RootElement
            .GetProperty("result")
            .GetProperty("tools")
            .EnumerateArray()
            .Select(McpToolDefinition.FromJson)
            .ToList();

        _cachedTools = tools;
        Console.WriteLine($"Discovered {tools.Count} toolbox tool(s):");
        foreach (var t in tools)
            Console.WriteLine($"  - {t.Name}: {t.Description}");

        return tools.Select(t => t.ToChatTool()).ToList();
    }

    public async Task<string> CallToolAsync(string toolName, string argumentsJson)
    {
        if (string.IsNullOrEmpty(_endpoint))
            return "Toolbox endpoint not configured";

        using var http = await CreateHttpClientAsync();
        var args = JsonDocument.Parse(argumentsJson).RootElement;
        var payload = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "tools/call",
            @params = new { name = toolName, arguments = args }
        });

        var resp = await http.PostAsync(_endpoint,
            new StringContent(payload, Encoding.UTF8, "application/json"));
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var content = doc.RootElement
            .GetProperty("result")
            .GetProperty("content")
            .EnumerateArray()
            .ToList();

        var texts = content
            .Where(c => c.TryGetProperty("text", out _))
            .Select(c => c.GetProperty("text").GetString() ?? "")
            .ToList();

        var result = string.Join("\n", texts);
        Console.WriteLine($"  Tool result ({toolName}): {result[..Math.Min(200, result.Length)]}...");
        return result;
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// MCP tool definition → ChatTool converter
// ═══════════════════════════════════════════════════════════════════════════
public class McpToolDefinition
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public JsonElement? InputSchema { get; set; }

    public static McpToolDefinition FromJson(JsonElement el)
    {
        return new McpToolDefinition
        {
            Name = el.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
            Description = el.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
            InputSchema = el.TryGetProperty("inputSchema", out var s) ? s : null,
        };
    }

    public ChatTool ToChatTool()
    {
        // Ensure schema always has "type":"object" and "properties"
        // Azure OpenAI rejects function schemas without these fields
        string schemaJson;
        if (InputSchema.HasValue)
        {
            var raw = InputSchema.Value.GetRawText();
            var schemaDoc = JsonDocument.Parse(raw);
            var root = schemaDoc.RootElement;

            // Check if properties is present
            if (!root.TryGetProperty("properties", out _))
            {
                schemaJson = """{"type":"object","properties":{}}""";
            }
            else
            {
                schemaJson = raw;
            }
        }
        else
        {
            schemaJson = """{"type":"object","properties":{}}""";
        }

        return ChatTool.CreateFunctionTool(Name, Description, BinaryData.FromString(schemaJson));
    }
}
