// Toolbox agent using a toolbox MCP endpoint in Microsoft Foundry.
//
// Connects to a toolbox MCP endpoint, discovers tools via tools/list, and
// exposes them through the Foundry Responses API for function calling. When
// the model requests a tool call, it is forwarded to the toolbox MCP endpoint
// via tools/call.
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
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Responses;

// ── Configuration ─────────────────────────────────────────────────────────

var projectEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("Set FOUNDRY_PROJECT_ENDPOINT");
var deployment = Environment.GetEnvironmentVariable("MODEL_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("Set MODEL_DEPLOYMENT_NAME");
var toolboxEndpoint = Environment.GetEnvironmentVariable("TOOLBOX_ENDPOINT");

if (string.IsNullOrEmpty(toolboxEndpoint))
    Console.Error.WriteLine(
        "WARNING: TOOLBOX_ENDPOINT is not set. The agent will run without toolbox tools. "
        + "Set this variable (platform-injected at runtime) to enable toolbox integration.");

// ── Foundry Responses API client ─────────────────────────────────────────

var credential = new DefaultAzureCredential();
var projectClient = new AIProjectClient(new Uri(projectEndpoint), credential);
var responsesClient = projectClient.ProjectOpenAIClient
    .GetProjectResponsesClientForModel(deployment);

// ── Toolbox MCP client ───────────────────────────────────────────────────

var toolboxClient = !string.IsNullOrEmpty(toolboxEndpoint)
    ? new ToolboxMcpClient(toolboxEndpoint, credential)
    : null;

ResponsesServer.Run<ToolboxHandler>(configure: builder =>
{
    builder.Services.AddSingleton(new AgentConfig(responsesClient, toolboxClient));
});

// ═══════════════════════════════════════════════════════════════════════════
// Config record
// ═══════════════════════════════════════════════════════════════════════════
public record AgentConfig(ProjectResponsesClient ResponsesClient, ToolboxMcpClient? ToolboxClient);

// ═══════════════════════════════════════════════════════════════════════════
// Response handler
// ═══════════════════════════════════════════════════════════════════════════
public class ToolboxHandler : ResponseHandler
{
    // Maximum number of tool-call rounds before giving up. Bounds API cost and
    // request latency if the model gets stuck in a tool-call feedback loop.
    private const int MaxToolRounds = 5;

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

        var functionTools = _config.ToolboxClient != null
            ? await _config.ToolboxClient.GetFunctionToolsAsync(cancellationToken)
            : new List<OpenAI.Responses.FunctionTool>();

        var options = new CreateResponseOptions
        {
            Instructions =
                "You are a helpful assistant with access to toolbox tools in Microsoft Foundry. " +
                "Use the available tools to help answer user questions.",
        };
        foreach (var tool in functionTools)
            options.Tools.Add(tool);
        options.InputItems.Add(ResponseItem.CreateUserMessageItem(userMessage));

        for (int round = 0; round < MaxToolRounds; round++)
        {
            var result = await _config.ResponsesClient.CreateResponseAsync(options, cancellationToken);
            bool functionCalled = false;

            foreach (var responseItem in result.Value.OutputItems)
            {
                options.InputItems.Add(responseItem);
                if (responseItem is FunctionCallResponseItem functionCall)
                {
                    Console.WriteLine($"  Tool call: {functionCall.FunctionName}({functionCall.FunctionArguments})");
                    var toolResult = _config.ToolboxClient != null
                        ? await _config.ToolboxClient.CallToolAsync(
                            functionCall.FunctionName,
                            functionCall.FunctionArguments.ToString(),
                            cancellationToken)
                        : "{\"error\": \"Toolbox not configured\"}";
                    options.InputItems.Add(
                        ResponseItem.CreateFunctionCallOutputItem(functionCall.CallId, toolResult));
                    functionCalled = true;
                }
            }

            if (!functionCalled)
            {
                yield return result.Value.GetOutputText() ?? "";
                yield break;
            }
        }

        yield return $"(Tool-call loop exceeded {MaxToolRounds} rounds without producing a final response.)";
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

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        var result = await _credential.GetTokenAsync(
            new Azure.Core.TokenRequestContext(new[] { "https://ai.azure.com/.default" }),
            cancellationToken);
        return result.Token;
    }

    private async Task<HttpClient> CreateHttpClientAsync(CancellationToken cancellationToken)
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        var token = await GetTokenAsync(cancellationToken);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        http.DefaultRequestHeaders.Add("Foundry-Features", "Toolboxes=V1Preview");
        return http;
    }

    public async Task<List<OpenAI.Responses.FunctionTool>> GetFunctionToolsAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_endpoint))
            return new List<OpenAI.Responses.FunctionTool>();

        if (_cachedTools != null)
            return _cachedTools.Select(t => t.ToFunctionTool()).ToList();

        using var http = await CreateHttpClientAsync(cancellationToken);
        var payload = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/list",
            @params = new { }
        });

        var resp = await http.PostAsync(_endpoint,
            new StringContent(payload, Encoding.UTF8, "application/json"),
            cancellationToken);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadAsStringAsync(cancellationToken);
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

        return tools.Select(t => t.ToFunctionTool()).ToList();
    }

    public async Task<string> CallToolAsync(string toolName, string argumentsJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_endpoint))
            return "Toolbox endpoint not configured";

        using var http = await CreateHttpClientAsync(cancellationToken);
        var args = JsonDocument.Parse(argumentsJson).RootElement;
        var payload = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "tools/call",
            @params = new { name = toolName, arguments = args }
        });

        var resp = await http.PostAsync(_endpoint,
            new StringContent(payload, Encoding.UTF8, "application/json"),
            cancellationToken);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadAsStringAsync(cancellationToken);
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
// MCP tool definition → Responses FunctionTool converter
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

    public OpenAI.Responses.FunctionTool ToFunctionTool()
    {
        // Ensure the schema always has "type":"object" and "properties" — the
        // Responses API rejects function schemas missing these fields.
        string schemaJson;
        if (InputSchema.HasValue
            && InputSchema.Value.ValueKind == JsonValueKind.Object
            && InputSchema.Value.TryGetProperty("properties", out _))
        {
            schemaJson = InputSchema.Value.GetRawText();
        }
        else
        {
            schemaJson = """{"type":"object","properties":{}}""";
        }

        return ResponseTool.CreateFunctionTool(
            functionName: Name,
            functionDescription: Description,
            functionParameters: BinaryData.FromString(schemaJson),
            strictModeEnabled: false);
    }
}
