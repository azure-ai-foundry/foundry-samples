// Copyright (c) Microsoft. All rights reserved.

/*
 * Browser Automation — Bring Your Own Responses agent with Playwright via Toolbox (C#)
 *
 * Hosted agent that automates browser interactions using playwright-cli via
 * Foundry Toolbox MCP. Supports multiple concurrent browser sessions with
 * lazy initialization — sessions are only created when the model first needs
 * to interact with a browser.
 *
 * Required environment variables:
 *   FOUNDRY_PROJECT_ENDPOINT       — Foundry project endpoint (auto-injected)
 *   AZURE_AI_MODEL_DEPLOYMENT_NAME — Model deployment name
 */

using System.Runtime.CompilerServices;
using System.Text.Json;
using Azure.AI.AgentServer.Responses;
using Azure.AI.AgentServer.Responses.Models;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Responses;

namespace BrowserAutomation;

public class Program
{
    public static void Main(string[] args)
    {
        // Load environment variables from a .env file if present (for local development).
        Env.NoClobber().TraversePath().Load();

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
            Console.Error.WriteLine(
                "[WARNING] APPLICATIONINSIGHTS_CONNECTION_STRING not set — traces will not be sent " +
                "to Application Insights. Set it to enable local telemetry. " +
                "(This variable is auto-injected in hosted Foundry containers.)");

        var foundryEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
            ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT environment variable is not set.");
        var deployment = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME")
            ?? throw new InvalidOperationException("AZURE_AI_MODEL_DEPLOYMENT_NAME environment variable is not set.");

        var credential = new DefaultAzureCredential();
        var projectClient = new AIProjectClient(new Uri(foundryEndpoint), credential);
        var responsesClient = projectClient.ProjectOpenAIClient
            .GetProjectResponsesClientForModel(deployment);

        // Toolbox MCP endpoint
        var toolboxName = Environment.GetEnvironmentVariable("TOOLBOX_NAME");
        if (string.IsNullOrWhiteSpace(toolboxName))
            toolboxName = Constants.DefaultToolboxName;
        var toolboxEndpoint = $"{foundryEndpoint.TrimEnd('/')}/toolboxes/{toolboxName}/mcp?api-version=v1";

        ResponsesServer.Run<BrowserAutomationHandler>(configure: builder =>
        {
            builder.Services.AddSingleton(responsesClient);
            builder.Services.AddSingleton(new ToolboxConfig(toolboxEndpoint, credential));
        });
    }
}

/// <summary>Configuration for ToolboxClient creation per-request.</summary>
public record ToolboxConfig(string Endpoint, DefaultAzureCredential Credential);

/// <summary>Browser session state.</summary>
public record SessionState(BrowserSession Browser, string? LiveViewUrl);

// ──────────────────────────────────────────────────────────────────
// Handler
// ──────────────────────────────────────────────────────────────────

public class BrowserAutomationHandler : ResponseHandler
{
    private readonly ProjectResponsesClient _responsesClient;
    private readonly ToolboxConfig _toolboxConfig;
    private readonly ILogger<BrowserAutomationHandler> _logger;

    // Multi-session state — static so it persists across requests (matches Python module-level globals)
    private static readonly Dictionary<string, SessionState> _sessions = new();
    private static readonly HashSet<string> _usedSessions = new(); // sessions touched in current request
    private static string? _lastSession;
    private static ToolboxClient? _toolbox;
    private readonly int _browserTimeout;

    public BrowserAutomationHandler(
        ProjectResponsesClient responsesClient,
        ToolboxConfig toolboxConfig,
        ILogger<BrowserAutomationHandler> logger)
    {
        _responsesClient = responsesClient;
        _toolboxConfig = toolboxConfig;
        _logger = logger;
        _browserTimeout = int.TryParse(Environment.GetEnvironmentVariable("BROWSER_TIMEOUT_SECONDS"), out var t) ? t : 180;
    }

    private ToolboxClient GetToolbox()
    {
        _toolbox ??= new ToolboxClient(
            _toolboxConfig.Endpoint,
            () =>
            {
                var ctx = new Azure.Core.TokenRequestContext(new[] { Constants.AzureAiScope });
                return _toolboxConfig.Credential.GetToken(ctx, default).Token;
            },
            _logger);
        return _toolbox;
    }

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
        var userInput = await context.GetInputTextAsync(cancellationToken: cancellationToken) ?? "";
        if (string.IsNullOrWhiteSpace(userInput))
        {
            yield return "No input provided.";
            yield break;
        }

        // Check for /verbose flag
        var verboseMode = userInput.TrimStart().StartsWith("/verbose");
        if (verboseMode)
            userInput = userInput.Replace("/verbose", "", StringComparison.OrdinalIgnoreCase).TrimStart();

        _usedSessions.Clear();

        // Build input items with history
        var inputItems = new List<ResponseItem>();
        try
        {
            var history = await context.GetHistoryAsync(cancellationToken);
            foreach (var item in history)
            {
                if (item is OutputItemMessage { Content: { } contents })
                {
                    foreach (var content in contents)
                    {
                        switch (content)
                        {
                            case MessageContentOutputTextContent { Text: { } assistantText }:
                                inputItems.Add(ResponseItem.CreateAssistantMessageItem(assistantText));
                                break;
                            case MessageContentInputTextContent { Text: { } userText }:
                                inputItems.Add(ResponseItem.CreateUserMessageItem(userText));
                                break;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("get_history failed; continuing without: {Error}", ex.Message);
        }

        if (_sessions.Count > 0)
        {
            inputItems.Add(ResponseItem.CreateUserMessageItem(
                $"[Active sessions: [{string.Join(", ", _sessions.Keys)}], default: {_lastSession}]"));
        }
        inputItems.Add(ResponseItem.CreateUserMessageItem(userInput));

        var systemPrompt = Constants.GetSystemPrompt(Skills.ListSkills());
        var tools = BuildTools();

        var verboseText = new System.Text.StringBuilder();
        string? finalReply = null;

        // Agentic loop — runs until model produces a final response (no tool calls)
        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield return "⚠️ Cancelled.\n";
                yield break;
            }

            var options = new CreateResponseOptions { Instructions = systemPrompt };
            foreach (var tool in tools)
                options.Tools.Add(tool);
            foreach (var item in inputItems)
                options.InputItems.Add(item);

            var result = await _responsesClient.CreateResponseAsync(options, cancellationToken);

            var functionCalls = result.Value.OutputItems
                .OfType<FunctionCallResponseItem>()
                .ToList();

            if (functionCalls.Count == 0)
            {
                finalReply = result.Value.GetOutputText() ?? "(No response)";
                // Add output items to input for context
                foreach (var item in result.Value.OutputItems)
                    inputItems.Add(item);
                break;
            }

            // Process tool calls
            foreach (var fc in functionCalls)
            {
                var sessionsBefore = new HashSet<string>(_sessions.Keys);

                var toolResult = await HandleToolCallAsync(fc.FunctionName, fc.FunctionArguments);

                // Detect new sessions (covers both explicit create_session and lazy creation via run_browser/run_parallel)
                var newSessions = _sessions.Keys.Except(sessionsBefore);
                foreach (var newSess in newSessions)
                {
                    var url = _sessions[newSess].LiveViewUrl;
                    var sessionLog = !string.IsNullOrEmpty(url)
                        ? $"🌐 Created **{newSess}** → [Live View]({url})\n"
                        : $"🌐 Created **{newSess}** (session ready)\n";
                    yield return sessionLog;
                    verboseText.Append(sessionLog);
                }

                // Format verbose log
                var log = FormatToolLog(fc.FunctionName, fc.FunctionArguments, toolResult);
                if (log != null)
                {
                    if (verboseMode)
                    {
                        yield return log.Value.Text;
                        verboseText.Append(log.Value.Text);
                    }
                    else
                    {
                        // Heartbeat: emit empty string to keep SSE alive
                        yield return "";
                    }
                }

                inputItems.Add(ResponseItem.CreateFunctionCallItem(fc.CallId, fc.FunctionName, fc.FunctionArguments));
                inputItems.Add(ResponseItem.CreateFunctionCallOutputItem(fc.CallId, toolResult));
            }
        }

        if (verboseText.Length > 0)
            yield return "\n---\n\n";

        yield return finalReply;

        // Append active session live view links at the end for easy access
        if (_sessions.Count > 0)
        {
            var links = _sessions.Select(kv =>
            {
                var url = kv.Value.LiveViewUrl;
                return !string.IsNullOrEmpty(url)
                    ? $"- **{kv.Key}**: [Live View]({url})"
                    : $"- **{kv.Key}**: (no live view)";
            });
            var footer = $"\n\n---\n**Active Sessions:**\n{string.Join("\n", links)}";
            if (_usedSessions.Count > 0)
                footer += $"\n\n_Used Sessions in this response: {string.Join(", ", _usedSessions)}_";
            yield return footer + "\n";
        }
    }

    // ── Tool execution ───────────────────────────────────────────────

    private async Task<string> HandleToolCallAsync(string functionName, BinaryData arguments)
    {
        try
        {
            var args = JsonSerializer.Deserialize<JsonElement>(arguments);

            return functionName switch
            {
                "load_skill" => HandleLoadSkill(args),
                "create_session" => await HandleCreateSessionAsync(args),
                "end_session" => await HandleEndSessionAsync(args),
                "run_browser" => await HandleRunBrowserAsync(args),
                "run_parallel" => await HandleRunParallelAsync(args),
                "list_sessions" => HandleListSessions(),
                _ => JsonSerializer.Serialize(new { error = $"Unknown tool: {functionName}" }),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool '{Name}' failed", functionName);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private string HandleLoadSkill(JsonElement args)
    {
        var name = args.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        var content = Skills.LoadSkill(name);
        if (content == null)
            return JsonSerializer.Serialize(new { error = $"Skill '{name}' not found. Available: {string.Join(", ", Skills.ListSkills())}" });
        return JsonSerializer.Serialize(new { skill = name, instructions = content });
    }

    private async Task<string> HandleCreateSessionAsync(JsonElement args)
    {
        var name = args.TryGetProperty("name", out var n) ? n.GetString() ?? $"session-{_sessions.Count + 1}" : $"session-{_sessions.Count + 1}";

        if (_sessions.ContainsKey(name))
            return JsonSerializer.Serialize(new { status = "already_exists", session = name, live_view_url = _sessions[name].LiveViewUrl });

        var toolbox = GetToolbox();
        var result = await toolbox.CallToolAsync("create_session");

        var cdpUrl = result.TryGetProperty("cdp_url", out var cdp) ? cdp.GetString() : null;
        var liveViewUrl = result.TryGetProperty("live_view_url", out var lv) ? lv.GetString() : null;

        if (string.IsNullOrEmpty(cdpUrl))
            return JsonSerializer.Serialize(new { error = "No CDP URL returned from Toolbox" });

        var browser = new BrowserSession(name, _browserTimeout, _logger);
        var (success, output) = await browser.ConnectAsync(cdpUrl);
        if (!success)
            return JsonSerializer.Serialize(new { error = $"Browser connect failed: {output}" });

        _sessions[name] = new SessionState(browser, liveViewUrl);
        _lastSession = name;
        _logger.LogInformation("Session '{Name}' created (live_view: {HasUrl})", name, !string.IsNullOrEmpty(liveViewUrl));

        return JsonSerializer.Serialize(new { status = "created", session = name, live_view_url = liveViewUrl });
    }

    private async Task<string> HandleEndSessionAsync(JsonElement args)
    {
        var name = args.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";

        if (name == "all")
        {
            var ended = _sessions.Keys.ToList();
            foreach (var sn in ended)
            {
                try { await _sessions[sn].Browser.CloseAsync(); } catch { }
            }
            _sessions.Clear();
            _lastSession = null;
            return JsonSerializer.Serialize(new { status = "ended_all", sessions = ended });
        }

        if (!_sessions.ContainsKey(name))
            return JsonSerializer.Serialize(new { error = $"Session '{name}' not found. Available: [{string.Join(", ", _sessions.Keys)}]" });

        try { await _sessions[name].Browser.CloseAsync(); } catch { }
        _sessions.Remove(name);
        if (_lastSession == name)
            _lastSession = _sessions.Keys.FirstOrDefault();
        return JsonSerializer.Serialize(new { status = "ended", session = name, remaining = _sessions.Keys.ToList() });
    }

    private async Task<string> HandleRunBrowserAsync(JsonElement args)
    {
        var sessName = args.TryGetProperty("session", out var s) ? s.GetString() : null;
        sessName ??= _lastSession;

        // Lazy session creation
        if (_sessions.Count == 0)
        {
            var createResult = await HandleCreateSessionAsync(
                JsonDocument.Parse("""{"name":"default"}""").RootElement);
            if (createResult.Contains("error"))
                return createResult;
            sessName = "default";
        }
        else if (string.IsNullOrEmpty(sessName) || !_sessions.ContainsKey(sessName))
        {
            return JsonSerializer.Serialize(new { error = $"Session '{sessName}' not found. Available: [{string.Join(", ", _sessions.Keys)}]" });
        }

        var browser = _sessions[sessName].Browser;
        var command = args.TryGetProperty("command", out var c) ? c.GetString() ?? "" : "";
        var cmdArgs = args.TryGetProperty("args", out var a) && a.ValueKind == JsonValueKind.Array
            ? a.EnumerateArray().Select(x => x.GetString() ?? "").ToArray()
            : Array.Empty<string>();

        _lastSession = sessName;
        _usedSessions.Add(sessName);
        var (success, output) = await browser.RunAsync(command, cmdArgs);
        return JsonSerializer.Serialize(new { success, output });
    }

    private async Task<string> HandleRunParallelAsync(JsonElement args)
    {
        if (!args.TryGetProperty("tasks", out var tasksEl) || tasksEl.ValueKind != JsonValueKind.Array)
            return JsonSerializer.Serialize(new { error = "No tasks provided" });

        // Ensure default session
        if (_sessions.Count == 0)
        {
            var createResult = await HandleCreateSessionAsync(
                JsonDocument.Parse("""{"name":"default"}""").RootElement);
            if (createResult.Contains("error"))
                return createResult;
        }

        var tasks = new List<Task<object>>();
        foreach (var task in tasksEl.EnumerateArray())
        {
            tasks.Add(RunOneParallelTask(task));
        }

        var results = await Task.WhenAll(tasks);
        return JsonSerializer.Serialize(new { parallel_results = results });
    }

    private async Task<object> RunOneParallelTask(JsonElement task)
    {
        var sess = task.TryGetProperty("session", out var s) ? s.GetString() : null;
        sess ??= _lastSession ?? "default";

        if (!_sessions.ContainsKey(sess))
            return new { session = sess, error = $"Session '{sess}' not found" };

        var browser = _sessions[sess].Browser;
        var command = task.TryGetProperty("command", out var c) ? c.GetString() ?? "" : "";
        var cmdArgs = task.TryGetProperty("args", out var a) && a.ValueKind == JsonValueKind.Array
            ? a.EnumerateArray().Select(x => x.GetString() ?? "").ToArray()
            : Array.Empty<string>();

        _usedSessions.Add(sess);
        var (success, output) = await browser.RunAsync(command, cmdArgs);
        return new { session = sess, command, success, output };
    }

    private string HandleListSessions()
    {
        var info = new Dictionary<string, object>();
        foreach (var (name, state) in _sessions)
        {
            info[name] = new { live_view_url = state.LiveViewUrl, connected = state.Browser.Connected };
        }
        return JsonSerializer.Serialize(new { sessions = info, @default = _lastSession });
    }

    // ── Verbose logging ──────────────────────────────────────────────

    private (string Kind, string Text)? FormatToolLog(string name, BinaryData arguments, string resultText)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(arguments);

        return name switch
        {
            "end_session" => FormatEndSessionLog(args, resultText),
            "run_browser" => FormatRunBrowserLog(args),
            "run_parallel" => ("log", $"⚡ Running {(args.TryGetProperty("tasks", out var t) ? t.GetArrayLength() : 0)} tasks in parallel\n"),
            "load_skill" => ("log", $"📖 Loading skill: {(args.TryGetProperty("name", out var n) ? n.GetString() : "?")}\n"),
            "list_sessions" => ("log", "📋 Listed sessions\n"),
            _ => null,
        };
    }

    private (string Kind, string Text) FormatEndSessionLog(JsonElement args, string resultText)
    {
        var sessName = args.TryGetProperty("name", out var n) ? n.GetString() ?? "?" : "?";
        try
        {
            var result = JsonSerializer.Deserialize<JsonElement>(resultText);
            var status = result.TryGetProperty("status", out var s) ? s.GetString() : null;
            if (status == "ended_all") return ("log", "🔴 Ended ALL sessions\n");
            if (status == "ended") return ("log", $"🔴 Ended **{sessName}**\n");
        }
        catch { }
        return ("log", $"🔴 End {sessName}\n");
    }

    private (string Kind, string Text) FormatRunBrowserLog(JsonElement args)
    {
        var sess = args.TryGetProperty("session", out var s) ? s.GetString() : _lastSession ?? "?";
        var cmd = args.TryGetProperty("command", out var c) ? c.GetString() ?? "" : "";
        var cmdArgs = args.TryGetProperty("args", out var a) && a.ValueKind == JsonValueKind.Array
            ? a.EnumerateArray().Take(2).Select(x => Redaction.Redact(x.GetString() ?? "")).ToList()
            : new List<string>();
        return ("log", $"🔧 [{sess}] `{cmd} {string.Join(" ", cmdArgs)}`\n");
    }

    // ── Tool definitions ─────────────────────────────────────────────

    private static List<ResponseTool> BuildTools()
    {
        var defs = JsonSerializer.Deserialize<JsonElement>(Constants.ToolDefinitions);
        var tools = new List<ResponseTool>();
        foreach (var def in defs.EnumerateArray())
        {
            var name = def.GetProperty("name").GetString()!;
            var desc = def.GetProperty("description").GetString()!;
            var parameters = def.GetProperty("parameters");
            tools.Add(ResponseTool.CreateFunctionTool(
                functionName: name,
                functionDescription: desc,
                functionParameters: BinaryData.FromString(parameters.GetRawText()),
                strictModeEnabled: false));
        }
        return tools;
    }
}
