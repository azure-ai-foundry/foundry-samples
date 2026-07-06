// Copyright (c) Microsoft. All rights reserved.

using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace BrowserAutomation;

/// <summary>
/// All middleware for the browser-automation agent:
/// 1. FunctionInvocationMiddleware — logs tool calls + intercepts create_session results
/// 2. LiveViewUrlMiddleware — injects live_view_url into response post-call
///
/// Mirrors Python's tool_logging_middleware + live_view_url_scrub_middleware.
/// </summary>
public static class Middlewares
{
    private static ILogger _logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger("Middlewares");

    /// <summary>Sets the logger (call from Program.cs after building the host if needed).</summary>
    public static void SetLogger(ILoggerFactory loggerFactory) =>
        _logger = loggerFactory.CreateLogger("Middlewares");

    private static readonly JsonSerializerOptions UnescapedJsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // ── Function Invocation Middleware ────────────────────────────────────────
    // Equivalent to Python's @function_middleware (tool_logging + create_session interception)

    /// <summary>
    /// Intercepts every tool call: logs with redacted args, and for create_session
    /// stores cdp_url + live_view_url server-side, returning a short result to the model.
    /// </summary>
    public static async ValueTask<object?> FunctionInvocationMiddleware(
        AIAgent agent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        var functionName = context.Function.Name;
        var args = context.CallContent?.Arguments;
        var safeArgs = args != null ? Redaction.Redact(string.Join(", ", args.Select(kv => $"{kv.Key}={kv.Value}"))) : "";

        // Central logging
        if (functionName.Contains("create_session", StringComparison.OrdinalIgnoreCase))
            _logger.LogInformation("[toolbox] create_session arguments={Args}", safeArgs);
        else if (functionName == "run_playwright_cli")
            _logger.LogInformation("[run_playwright_cli] arguments={Args}", safeArgs);
        else if (functionName == "close_browser_session")
            _logger.LogInformation("[close_browser_session] arguments={Args}", safeArgs);
        else if (functionName == "get_live_view_url")
            _logger.LogInformation("[get_live_view_url]");
        else if (functionName == "load_skill")
            _logger.LogInformation("[skill] load_skill arguments={Args}", safeArgs);

        var result = await next(context, cancellationToken);

        // Intercept create_session results — store URLs server-side
        if (functionName.Contains("create_session", StringComparison.OrdinalIgnoreCase))
        {
            var resultStr = result?.ToString() ?? "";
            try
            {
                using var doc = JsonDocument.Parse(resultStr);
                var root = doc.RootElement;

                if (root.TryGetProperty("cdp_url", out var cdp))
                    Tools.SetCdpUrl(cdp.GetString());
                if (root.TryGetProperty("live_view_url", out var lv))
                    Tools.SetLiveViewUrl(lv.GetString());

                _logger.LogInformation("[create_session] cdp_url stored (length={CdpLen})", Tools.GetStoredCdpUrl()?.Length ?? 0);
                _logger.LogInformation("[create_session] live_view_url stored (length={LvLen})", Tools.GetStoredLiveViewUrl()?.Length ?? 0);

                return JsonSerializer.Serialize(new
                {
                    status = "session_created",
                    note = "CDP URL stored server-side. Call run_playwright_cli with sessionId='browser' and command='open about:blank'.",
                }, UnescapedJsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "[create_session] JSON parse error");
                // Not JSON or no cdp_url — pass through
            }
        }

        return result;
    }

    // ── Agent-Level Middleware (LiveViewUrl injection) ────────────────────────
    // Equivalent to Python's @chat_middleware (live_view_url_scrub_middleware post-call inject)

    /// <summary>
    /// Agent middleware: passes through to inner agent, then injects live_view_url post-call.
    /// The model never sees the URL — it goes directly to the user.
    /// </summary>
    public static async Task<AgentResponse> LiveViewUrlMiddleware(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        CancellationToken cancellationToken)
    {
        var response = await innerAgent.RunAsync(messages, session, options, cancellationToken);

        var liveViewUrl = Tools.GetStoredLiveViewUrl();
        if (!string.IsNullOrEmpty(liveViewUrl))
        {
            // Check if URL is already present in any message
            var alreadyPresent = response.Messages.Any(m =>
                m.Role == ChatRole.Assistant &&
                (m.Text ?? "").Contains("live.playwright.microsoft.com"));

            if (!alreadyPresent)
            {
                // Always append — even if there's no prior assistant message
                response.Messages.Add(new ChatMessage(
                    ChatRole.Assistant,
                    $"\n\n🔴 [Browser Live View]({liveViewUrl})"));
            }
        }

        return response;
    }

    /// <summary>
    /// Streaming variant: yields all updates from inner agent, injecting live_view_url
    /// as soon as it becomes available (prepend) and again at the end (append).
    /// This is what the hosting layer actually uses for SSE responses.
    /// Mirrors Python's prepend + append pattern.
    /// </summary>
    public static async IAsyncEnumerable<AgentResponseUpdate> LiveViewUrlStreamingMiddleware(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var liveViewUrlBefore = Tools.GetStoredLiveViewUrl();
        bool prepended = !string.IsNullOrEmpty(liveViewUrlBefore); // already known = already shown previously

        // Yield all updates from the inner agent
        await foreach (var update in innerAgent.RunStreamingAsync(messages, session, options, cancellationToken))
        {
            // Check if live_view_url just became available (after create_session)
            if (!prepended)
            {
                var url = Tools.GetStoredLiveViewUrl();
                if (!string.IsNullOrEmpty(url))
                {
                    _logger.LogDebug("[streaming-middleware] Prepending live_view_url");
                    yield return new AgentResponseUpdate(
                        ChatRole.Assistant,
                        [new TextContent($"🔴 Created Browser Session [Live View]({url})\n\n")]);
                    prepended = true;
                }
            }

            yield return update;
        }

        // Append live_view_url at end so it shows in every response
        var liveViewUrl = Tools.GetStoredLiveViewUrl();
        if (!string.IsNullOrEmpty(liveViewUrl))
        {
            _logger.LogDebug("[streaming-middleware] Appending live_view_url");
            yield return new AgentResponseUpdate(
                ChatRole.Assistant,
                [new TextContent($"\n\n🔴 [Browser Live View]({liveViewUrl})")])
            { FinishReason = ChatFinishReason.Stop };
        }
    }
}
