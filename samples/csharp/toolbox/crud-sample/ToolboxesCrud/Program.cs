// Comprehensive SDK sample for toolbox in Microsoft Foundry CRUD operations.
//
// Tested with:
//   Azure.AI.Projects        2.1.0-alpha.20260406.1
//   Azure.AI.Projects.Agents 2.1.0-alpha.20260406.1
//
// API: AIProjectClient → AgentAdministrationClient → GetAgentToolboxes() → AgentToolboxes
//   - CreateToolboxVersion(toolboxName, tools, description, metadata)
//   - GetToolbox(toolboxName)
//   - GetToolboxVersion(toolboxName, version)
//   - GetToolboxVersions(toolboxName)
//   - GetToolboxes()
//   - UpdateToolbox(toolboxName, defaultVersion)
//   - DeleteToolboxVersion(toolboxName, version)
//   - DeleteToolbox(toolboxName)
//
// Tool types demonstrated:
//   - MCP (no-auth, key-auth, OAuth, filtered)
//   - Code Interpreter
//   - File Search
//   - Azure AI Search
//   - Web Search
//   - OpenAPI (no-auth, project-connection auth)
//   - A2A (agent-to-agent)
//   - Multi-tool combinations
//
// Prerequisites:
//   dotnet add package Azure.AI.Projects --version 2.1.0-alpha.20260406.1
//   dotnet add package Azure.AI.Projects.Agents --version 2.1.0-alpha.20260406.1
//   dotnet add package Azure.Identity
//
//   Set environment variables:
//     FOUNDRY_PROJECT_ENDPOINT  — Microsoft Foundry project endpoint
//     MCP_CONNECTION_ID         — Connection ID for key-based MCP (key-auth, filtered, multi)
//     MCP_OAUTH_CONNECTION_ID   — Connection ID for OAuth MCP
//     VECTOR_STORE_ID           — Vector store ID for File Search
//     AI_SEARCH_CONNECTION_ID   — Project connection ID for Azure AI Search index
//     AI_SEARCH_INDEX_NAME      — Azure AI Search index name
//     OPENAPI_CONNECTION_ID     — Project connection ID for OpenAPI key auth
//     A2A_BASE_URL              — Base URL of the remote A2A agent
//     A2A_CONNECTION_ID         — Project connection ID for A2A agent (optional)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using OpenAI.Responses;

// ═══════════════════════════════════════════════════════════════════════════
// Configuration
// ═══════════════════════════════════════════════════════════════════════════
var projectEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("Set FOUNDRY_PROJECT_ENDPOINT");

var credential = new DefaultAzureCredential();
var projectClient = new AIProjectClient(endpoint: new Uri(projectEndpoint), tokenProvider: credential);
var toolboxClient = projectClient.AgentAdministrationClient.GetAgentToolboxes();

// ═══════════════════════════════════════════════════════════════════════════
// MCP Validation Helpers (tools/list + tools/call via REST)
// ═══════════════════════════════════════════════════════════════════════════
string McpEndpoint(string toolboxName)
    => $"{projectEndpoint}/toolboxes/{toolboxName}/mcp?api-version=v1";

async Task<string> GetBearerTokenAsync()
{
    var tokenResult = await credential.GetTokenAsync(
        new Azure.Core.TokenRequestContext(new[] { "https://ai.azure.com/.default" }));
    return tokenResult.Token;
}

async Task<List<JsonElement>> McpToolsListAsync(string toolboxName)
{
    using var http = new HttpClient();
    var token = await GetBearerTokenAsync();
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    http.DefaultRequestHeaders.Add("Foundry-Features", "Toolboxes=V1Preview");

    var payload = JsonSerializer.Serialize(new
    {
        jsonrpc = "2.0",
        id = 1,
        method = "tools/list",
        @params = new { }
    });

    var resp = await http.PostAsync(
        McpEndpoint(toolboxName),
        new StringContent(payload, Encoding.UTF8, "application/json"));
    resp.EnsureSuccessStatusCode();

    var body = await resp.Content.ReadAsStringAsync();
    var doc = JsonDocument.Parse(body);

    // Handle JSON-RPC error responses (e.g. auth failures)
    if (doc.RootElement.TryGetProperty("error", out var err))
    {
        var msg = err.TryGetProperty("message", out var m) ? m.GetString() : "unknown error";
        Console.WriteLine($"  tools/list → ERROR: {msg}");
        return new List<JsonElement>();
    }

    var tools = doc.RootElement
        .GetProperty("result")
        .GetProperty("tools")
        .EnumerateArray()
        .ToList();

    Console.WriteLine($"  tools/list → {tools.Count} tool(s)");
    foreach (var t in tools.Take(5))
    {
        var name = t.TryGetProperty("name", out var n) ? n.GetString() : "?";
        Console.WriteLine($"    - {name}");
    }
    return tools;
}

async Task McpToolsCallAsync(string toolboxName, string toolName, object arguments)
{
    using var http = new HttpClient();
    var token = await GetBearerTokenAsync();
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    http.DefaultRequestHeaders.Add("Foundry-Features", "Toolboxes=V1Preview");

    var payload = JsonSerializer.Serialize(new
    {
        jsonrpc = "2.0",
        id = 2,
        method = "tools/call",
        @params = new { name = toolName, arguments }
    });

    var resp = await http.PostAsync(
        McpEndpoint(toolboxName),
        new StringContent(payload, Encoding.UTF8, "application/json"));
    resp.EnsureSuccessStatusCode();

    var body = await resp.Content.ReadAsStringAsync();
    var doc = JsonDocument.Parse(body);

    // Handle JSON-RPC error responses
    if (doc.RootElement.TryGetProperty("error", out var callErr))
    {
        var msg = callErr.TryGetProperty("message", out var m) ? m.GetString() : "unknown error";
        Console.WriteLine($"  tools/call({toolName}) → ERROR: {msg}");
        return;
    }

    var content = doc.RootElement
        .GetProperty("result")
        .GetProperty("content")
        .EnumerateArray()
        .ToList();

    Console.WriteLine($"  tools/call({toolName}) → {content.Count} content block(s)");
    if (content.Count > 0)
    {
        var text = content[0].TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
        Console.WriteLine($"    preview: {text[..Math.Min(200, text.Length)]}...");
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// Full CRUD lifecycle helper
// ═══════════════════════════════════════════════════════════════════════════
async Task<bool> FullLifecycleAsync(
    string toolboxName,
    IEnumerable<ProjectsAgentTool> tools,
    (string toolName, object args)? validateCall = null)
{
    Console.WriteLine($"\n{"".PadRight(60, '=')}");
    Console.WriteLine($"LIFECYCLE: {toolboxName}");
    Console.WriteLine($"{"".PadRight(60, '=')}");

    // 0. Clean up if leftover from a previous run
    try { await toolboxClient.DeleteToolboxAsync(toolboxName); } catch { }

    try
    {
        // 1. Create version 1
        var v1Result = await toolboxClient.CreateToolboxVersionAsync(
            toolboxName: toolboxName,
            tools: tools,
            description: $"{toolboxName} v1");
        ToolboxVersion v1 = v1Result.Value;
        Console.WriteLine($"  1. CreateToolboxVersion → version={v1.Version}, name={v1.Name}");

        // 2. Get toolbox record
        var recordResult = await toolboxClient.GetToolboxAsync(toolboxName);
        ToolboxRecord record = recordResult.Value;
        Console.WriteLine($"  2. GetToolbox → name={record.Name}, default_version={record.DefaultVersion}");

        // 3. MCP tools/list validation
        var listed = await McpToolsListAsync(toolboxName);

        // 4. Optional tools/call
        if (validateCall is not null && listed.Count > 0)
        {
            var match = listed.FirstOrDefault(t =>
                t.TryGetProperty("name", out var n) && n.GetString() == validateCall.Value.toolName);
            if (match.ValueKind != JsonValueKind.Undefined)
            {
                await McpToolsCallAsync(toolboxName, validateCall.Value.toolName, validateCall.Value.args);
            }
            else
            {
                Console.WriteLine($"  ⚠ tool '{validateCall.Value.toolName}' not found — skipping call");
            }
        }

        // 5. Create version 2
        var v2Result = await toolboxClient.CreateToolboxVersionAsync(
            toolboxName: toolboxName,
            tools: tools,
            description: $"{toolboxName} v2 (promoted)");
        ToolboxVersion v2 = v2Result.Value;
        Console.WriteLine($"  5. CreateToolboxVersion → version={v2.Version}");

        // 6. List versions
        var versions = new List<ToolboxVersion>();
        await foreach (var v in toolboxClient.GetToolboxVersionsAsync(toolboxName))
            versions.Add(v);
        Console.WriteLine($"  6. ListVersions → {versions.Count} version(s): [{string.Join(", ", versions.Select(v => v.Version))}]");

        // 7. Promote v2 to default
        await toolboxClient.UpdateToolboxAsync(toolboxName, v2.Version);
        var updatedResult = await toolboxClient.GetToolboxAsync(toolboxName);
        ToolboxRecord updated = updatedResult.Value;
        Console.WriteLine($"  7. UpdateToolbox (promote) → default_version={updated.DefaultVersion}");

        // 8. Get version v2 detail
        var v2DetailResult = await toolboxClient.GetToolboxVersionAsync(toolboxName, v2.Version);
        ToolboxVersion v2Detail = v2DetailResult.Value;
        Console.WriteLine($"  8. GetToolboxVersion → version={v2Detail.Version}, desc={v2Detail.Description}");

        // 9. Delete v1 (non-default)
        await toolboxClient.DeleteToolboxVersionAsync(toolboxName, v1.Version);
        Console.WriteLine($"  9. DeleteToolboxVersion v1 → OK");
    }
    finally
    {
        // 10. Delete the entire toolbox (always clean up)
        try
        {
            await toolboxClient.DeleteToolboxAsync(toolboxName);
            Console.WriteLine($" 10. DeleteToolbox → OK");
        }
        catch { Console.WriteLine($" 10. DeleteToolbox → already gone or failed"); }
    }

    return true;
}

// ═══════════════════════════════════════════════════════════════════════════
// Individual tool samples
// ═══════════════════════════════════════════════════════════════════════════

// ---------------------------------------------------------------------------
// 1. MCP — No Auth (public server, e.g. gitmcp.io)
// ---------------------------------------------------------------------------
async Task SampleMcpNoAuth()
{
    var tool = ProjectsAgentTool.AsProjectTool(ResponseTool.CreateMcpTool(
        serverLabel: "gitmcp",
        serverUri: new Uri("https://gitmcp.io/Azure-Samples/agent-openai-python-prompty")));

    await FullLifecycleAsync("mcp-noauth-sample", new[] { tool });
}

// ---------------------------------------------------------------------------
// 2. MCP — Key Auth
// ---------------------------------------------------------------------------
async Task SampleMcpKeyAuth()
{
    var connId = Environment.GetEnvironmentVariable("MCP_CONNECTION_ID")
        ?? throw new InvalidOperationException("Set MCP_CONNECTION_ID");

    var tool = ProjectsAgentTool.AsProjectTool(ResponseTool.CreateMcpTool(
        serverLabel: "github",
        serverUri: new Uri("https://api.githubcopilot.com/mcp"),
        headers: new Dictionary<string, string>
        {
            ["project_connection_id"] = connId
        }));

    await FullLifecycleAsync("mcp-keyauth-sample", new[] { tool });
}

// ---------------------------------------------------------------------------
// 3. MCP — OAuth
// ---------------------------------------------------------------------------
async Task SampleMcpOAuth()
{
    var connId = Environment.GetEnvironmentVariable("MCP_OAUTH_CONNECTION_ID")
        ?? throw new InvalidOperationException("Set MCP_OAUTH_CONNECTION_ID");

    var tool = ProjectsAgentTool.AsProjectTool(ResponseTool.CreateMcpTool(
        serverLabel: "github-oauth",
        serverUri: new Uri("https://api.githubcopilot.com/mcp"),
        headers: new Dictionary<string, string>
        {
            ["project_connection_id"] = connId
        }));

    await FullLifecycleAsync("mcp-oauth-sample", new[] { tool });
}

// ---------------------------------------------------------------------------
// 4. MCP — Filtered tools
// ---------------------------------------------------------------------------
async Task SampleMcpFiltered()
{
    var connId = Environment.GetEnvironmentVariable("MCP_CONNECTION_ID")
        ?? throw new InvalidOperationException("Set MCP_CONNECTION_ID");

    var tool = ProjectsAgentTool.AsProjectTool(ResponseTool.CreateMcpTool(
        serverLabel: "github-filtered",
        serverUri: new Uri("https://api.githubcopilot.com/mcp"),
        headers: new Dictionary<string, string>
        {
            ["project_connection_id"] = connId
        }));

    await FullLifecycleAsync("mcp-filtered-sample", new[] { tool });
}

// ---------------------------------------------------------------------------
// 5. File Search
// ---------------------------------------------------------------------------
async Task SampleFileSearch()
{
    var vectorStoreId = Environment.GetEnvironmentVariable("VECTOR_STORE_ID")
        ?? throw new InvalidOperationException("Set VECTOR_STORE_ID");

    var tool = ProjectsAgentTool.AsProjectTool(ResponseTool.CreateFileSearchTool(
        vectorStoreIds: new[] { vectorStoreId }));

    await FullLifecycleAsync("filesearch-sample", new[] { tool });
}

// ---------------------------------------------------------------------------
// 6. Web Search
// ---------------------------------------------------------------------------
async Task SampleWebSearch()
{
    var tool = ProjectsAgentTool.AsProjectTool(ResponseTool.CreateWebSearchTool());
    await FullLifecycleAsync("websearch-sample", new[] { tool });
}

// ---------------------------------------------------------------------------
// 7. Code Interpreter
// ---------------------------------------------------------------------------
async Task SampleCodeInterpreter()
{
    var tool = ProjectsAgentTool.AsProjectTool(ResponseTool.CreateCodeInterpreterTool());
    await FullLifecycleAsync("codeinterp-sample", new[] { tool });
}

// ---------------------------------------------------------------------------
// 8. Azure AI Search
// ---------------------------------------------------------------------------
async Task SampleAzureAiSearch()
{
    var connectionId = Environment.GetEnvironmentVariable("AI_SEARCH_CONNECTION_ID")
        ?? throw new InvalidOperationException("Set AI_SEARCH_CONNECTION_ID");
    var indexName = Environment.GetEnvironmentVariable("AI_SEARCH_INDEX_NAME")
        ?? throw new InvalidOperationException("Set AI_SEARCH_INDEX_NAME");

    var tool = new AzureAISearchTool(
        new AzureAISearchToolOptions(new[]
        {
            new AzureAISearchToolIndex(
                projectConnectionId: connectionId,
                indexName: indexName,
                queryType: null,
                topK: null,
                filter: null,
                indexAssetId: null,
                additionalBinaryDataProperties: null)
        }));

    await FullLifecycleAsync("aisearch-sample", new[] { tool });
}

// ---------------------------------------------------------------------------
// 9. OpenAPI — No Auth (public API, no credentials required)
// ---------------------------------------------------------------------------
async Task SampleOpenApiNoAuth()
{
    // Minimal OpenAPI 3.0 spec for JSON Placeholder (public REST API)
    var spec = JsonSerializer.Serialize(new
    {
        openapi = "3.0.0",
        info = new { title = "JSON Placeholder", version = "1.0" },
        servers = new[] { new { url = "https://jsonplaceholder.typicode.com" } },
        paths = new Dictionary<string, object>
        {
            ["/posts/{id}"] = new
            {
                get = new
                {
                    operationId = "getPost",
                    summary = "Get a post by ID",
                    parameters = new[]
                    {
                        new { name = "id", @in = "path", required = true, schema = new { type = "integer" } }
                    },
                    responses = new Dictionary<string, object>
                    {
                        ["200"] = new { description = "A post object" }
                    }
                }
            }
        }
    });

    var tool = new OpenAPITool(
        new OpenApiFunctionDefinition(
            "jsonplaceholder",
            BinaryData.FromString(spec),
            new OpenAPIAnonymousAuthenticationDetails()));

    await FullLifecycleAsync("openapi-noauth-sample", new[] { tool });
}

// ---------------------------------------------------------------------------
// 10. OpenAPI — Project Connection Auth (API key stored in a Foundry connection)
// ---------------------------------------------------------------------------
async Task SampleOpenApiWithConnection()
{
    var connectionId = Environment.GetEnvironmentVariable("OPENAPI_CONNECTION_ID")
        ?? throw new InvalidOperationException("Set OPENAPI_CONNECTION_ID");

    // Example spec for TripAdvisor Location Search API (key via project connection)
    var spec = JsonSerializer.Serialize(new
    {
        openapi = "3.0.1",
        info = new { title = "TripAdvisor API", version = "1.0" },
        servers = new[] { new { url = "https://api.content.tripadvisor.com/api/v1" } },
        paths = new Dictionary<string, object>
        {
            ["/location/search"] = new
            {
                get = new
                {
                    operationId = "searchLocations",
                    summary = "Search for locations",
                    parameters = new[]
                    {
                        new { name = "searchQuery", @in = "query", required = true, schema = new { type = "string" } },
                        new { name = "language", @in = "query", schema = new { type = "string" } }
                    },
                    responses = new Dictionary<string, object>
                    {
                        ["200"] = new { description = "Search results" }
                    },
                    security = new[] { new Dictionary<string, object> { ["apiKey"] = new string[0] } }
                }
            }
        },
        components = new
        {
            securitySchemes = new Dictionary<string, object>
            {
                ["apiKey"] = new { type = "apiKey", name = "key", @in = "query" }
            }
        }
    });

    var tool = new OpenAPITool(
        new OpenApiFunctionDefinition(
            "tripadvisor",
            BinaryData.FromString(spec),
            new OpenApiProjectConnectionAuthenticationDetails(
                new OpenApiProjectConnectionSecurityScheme(connectionId))));

    await FullLifecycleAsync("openapi-connection-sample", new[] { tool });
}

// ---------------------------------------------------------------------------
// 11. A2A — Agent-to-Agent
// ---------------------------------------------------------------------------
async Task SampleA2A()
{
    var baseUrl = Environment.GetEnvironmentVariable("A2A_BASE_URL")
        ?? throw new InvalidOperationException("Set A2A_BASE_URL to the remote agent base URL");
    var connectionId = Environment.GetEnvironmentVariable("A2A_CONNECTION_ID");

    var tool = new A2APreviewTool(new Uri(baseUrl));
    if (!string.IsNullOrEmpty(connectionId))
        tool.ProjectConnectionId = connectionId;

    await FullLifecycleAsync("a2a-sample", new[] { tool });
}

// ---------------------------------------------------------------------------
// 12. Multi-Tool (MCP + MCP)
// ---------------------------------------------------------------------------
async Task SampleMultiTool()
{
    var connId = Environment.GetEnvironmentVariable("MCP_CONNECTION_ID")
        ?? throw new InvalidOperationException("Set MCP_CONNECTION_ID");

    var tool1 = ProjectsAgentTool.AsProjectTool(ResponseTool.CreateMcpTool(
        serverLabel: "gitmcp",
        serverUri: new Uri("https://gitmcp.io/Azure-Samples/agent-openai-python-prompty")));

    var tool2 = ProjectsAgentTool.AsProjectTool(ResponseTool.CreateMcpTool(
        serverLabel: "github",
        serverUri: new Uri("https://api.githubcopilot.com/mcp"),
        headers: new Dictionary<string, string>
        {
            ["project_connection_id"] = connId
        }));

    await FullLifecycleAsync("multi-tool-sample", new[] { tool1, tool2 });
}

// ---------------------------------------------------------------------------
// 13. List all toolboxes
// ---------------------------------------------------------------------------
async Task SampleListAll()
{
    var toolboxes = new List<ToolboxRecord>();
    await foreach (var tb in toolboxClient.GetToolboxesAsync())
        toolboxes.Add(tb);

    Console.WriteLine($"\n{toolboxes.Count} toolbox(es):");
    foreach (var tb in toolboxes)
        Console.WriteLine($"  {tb.Name}  default_version={tb.DefaultVersion}");
}

// ═══════════════════════════════════════════════════════════════════════════
// Runner
// ═══════════════════════════════════════════════════════════════════════════
var samples = new Dictionary<string, Func<Task>>
{
    ["mcp-noauth"]         = SampleMcpNoAuth,
    ["mcp-keyauth"]        = SampleMcpKeyAuth,
    ["mcp-oauth"]          = SampleMcpOAuth,
    ["mcp-filtered"]       = SampleMcpFiltered,
    ["filesearch"]         = SampleFileSearch,
    ["websearch"]          = SampleWebSearch,
    ["code-interpreter"]   = SampleCodeInterpreter,
    ["azure-ai-search"]    = SampleAzureAiSearch,
    ["openapi-noauth"]     = SampleOpenApiNoAuth,
    ["openapi-conn"]       = SampleOpenApiWithConnection,
    ["a2a"]                = SampleA2A,
    ["multi"]              = SampleMultiTool,
    ["list"]               = SampleListAll,
};

if (args.Length >= 1 && args[0] == "all")
{
    var results = new Dictionary<string, string>();
    foreach (var (name, fn) in samples)
    {
        if (name == "list") continue;
        try
        {
            await fn();
            results[name] = "PASS";
        }
        catch (Exception ex)
        {
            results[name] = $"FAIL: {ex.Message}";
            Console.Error.WriteLine(ex);
        }
    }

    Console.WriteLine($"\n{"".PadRight(60, '=')}");
    Console.WriteLine("CRUD TEST REPORT");
    Console.WriteLine($"{"".PadRight(60, '=')}");
    foreach (var (name, status) in results)
    {
        var mark = status == "PASS" ? "✓" : "✗";
        Console.WriteLine($"  {mark} {name}: {status}");
    }
    var passed = results.Values.Count(v => v == "PASS");
    Console.WriteLine($"\n  {passed}/{results.Count} passed");
}
else if (args.Length >= 1 && samples.ContainsKey(args[0]))
{
    await samples[args[0]]();
}
else
{
    Console.WriteLine($"Usage: dotnet run -- <sample|all>");
    Console.WriteLine($"Samples: {string.Join(", ", samples.Keys)}");
}
