// Copyright (c) Microsoft. All rights reserved.

using System.Net.Http.Headers;
using System.Text.Json;

namespace BrowserAutomation;

/// <summary>
/// Lightweight MCP client for Foundry Toolbox browser session management.
/// Mirrors the Python ToolboxClient — initialize, discover tools, call tools.
/// </summary>
public class ToolboxClient
{
    private const string ToolboxFeatures = "Toolboxes=V1Preview";

    private readonly string _endpoint;
    private readonly Func<string> _getToken;
    private readonly ILogger _logger;
    private string? _sessionId;
    private int _reqId;
    private bool _initialized;
    private readonly Dictionary<string, string> _toolNames = new(); // suffix -> full name

    public ToolboxClient(string endpoint, Func<string> tokenProvider, ILogger logger)
    {
        _endpoint = endpoint;
        _getToken = tokenProvider;
        _logger = logger;
    }

    /// <summary>Send MCP initialize + notification, then discover tools.</summary>
    public async Task<string> InitializeAsync()
    {
        if (_initialized) return "already-initialized";

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        // Initialize
        var initResp = await PostAsync(client, new
        {
            jsonrpc = "2.0",
            id = NextId(),
            method = "initialize",
            @params = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new { name = "browser-automation-agent", version = "1.0.0" },
            },
        });

        // Capture session ID
        if (initResp.Headers.TryGetValues("mcp-session-id", out var sidValues))
        {
            var sid = sidValues.FirstOrDefault();
            if (!string.IsNullOrEmpty(sid) && sid != "None")
                _sessionId = sid;
        }

        var initData = await ReadJsonAsync(initResp);

        // Notification
        await PostAsync(client, new
        {
            jsonrpc = "2.0",
            method = "notifications/initialized",
        });

        // Discover tools
        var listResp = await PostAsync(client, new
        {
            jsonrpc = "2.0",
            id = NextId(),
            method = "tools/list",
            @params = new { },
        });

        var listData = await ReadJsonAsync(listResp);
        var tools = listData.GetProperty("result").GetProperty("tools");
        foreach (var tool in tools.EnumerateArray())
        {
            var name = tool.GetProperty("name").GetString() ?? "";
            var parts = name.Split("___", 2);
            var suffix = parts.Length == 2 ? parts[1] : name;
            _toolNames[suffix] = name;
        }

        _logger.LogInformation("Toolbox tools discovered: {Tools}", string.Join(", ", _toolNames.Values));
        _initialized = true;

        var serverName = initData.GetProperty("result")
            .GetProperty("serverInfo")
            .GetProperty("name")
            .GetString() ?? "unknown";
        return serverName;
    }

    /// <summary>Call a Toolbox tool and return the parsed JSON result.</summary>
    public async Task<JsonElement> CallToolAsync(string name, Dictionary<string, object>? arguments = null)
    {
        await InitializeAsync();

        // Resolve suffix to full name
        if (!name.Contains("___") && _toolNames.TryGetValue(name, out var fullName))
            name = fullName;

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        var resp = await PostAsync(client, new
        {
            jsonrpc = "2.0",
            id = NextId(),
            method = "tools/call",
            @params = new { name, arguments = arguments ?? new Dictionary<string, object>() },
        });

        var data = await ReadJsonAsync(resp);

        if (data.TryGetProperty("error", out var error))
        {
            var msg = error.TryGetProperty("message", out var m) ? m.GetString() : "Unknown MCP error";
            throw new InvalidOperationException($"Toolbox error: {msg}");
        }

        var result = data.GetProperty("result");
        if (result.TryGetProperty("content", out var content))
        {
            foreach (var item in content.EnumerateArray())
            {
                if (item.TryGetProperty("type", out var type) && type.GetString() == "text"
                    && item.TryGetProperty("text", out var text))
                {
                    try
                    {
                        return JsonDocument.Parse(text.GetString()!).RootElement;
                    }
                    catch (JsonException)
                    {
                        return item;
                    }
                }
            }
        }

        return result;
    }

    private async Task<HttpResponseMessage> PostAsync(HttpClient client, object body)
    {
        var json = JsonSerializer.Serialize(body);
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _getToken());
        request.Headers.TryAddWithoutValidation("Foundry-Features", ToolboxFeatures);
        if (_sessionId != null)
            request.Headers.TryAddWithoutValidation("mcp-session-id", _sessionId);

        var resp = await client.SendAsync(request);
        resp.EnsureSuccessStatusCode();
        return resp;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage resp)
    {
        var body = await resp.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement;
    }

    private int NextId() => ++_reqId;
}
