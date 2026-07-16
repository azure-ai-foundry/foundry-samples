// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace BrowserAutomation;

/// <summary>
/// Factory methods for creating local agent tools.
/// Single-session design: one browser session at a time.
/// </summary>
public static class Tools
{
    // Server-side URL storage — prevents model corruption of long base64 tokens.
    private static string? _cdpUrl;
    private static string? _liveViewUrl;

    public static string? GetStoredLiveViewUrl() => _liveViewUrl;
    public static string? GetStoredCdpUrl() => _cdpUrl;
    public static void SetLiveViewUrl(string? url) => _liveViewUrl = url;
    public static void SetCdpUrl(string? url) => _cdpUrl = url;

    /// <summary>
    /// Create the run_playwright_cli tool. CDP URL is injected from server-side storage.
    /// </summary>
    public static AITool MakeRunPlaywrightCli(int timeoutSeconds)
    {
        return AIFunctionFactory.Create(
            async ([Description("Local playwright-cli session name")] string sessionId,
                   [Description("playwright-cli command (e.g. 'goto https://example.com', 'snapshot', 'click e3')")] string command) =>
            {
                var sid = sessionId.Trim();
                var session = new BrowserSession(sid, timeoutSeconds);
                return await session.RunCommandAsync(command, _cdpUrl);
            },
            "run_playwright_cli",
            "Run a playwright-cli command.");
    }

    /// <summary>
    /// Create the close_browser_session tool.
    /// </summary>
    public static AITool MakeCloseBrowserSession(int timeoutSeconds)
    {
        return AIFunctionFactory.Create(
            async ([Description("Local playwright-cli session name")] string sessionId) =>
            {
                var sid = sessionId.Trim();

                if (string.IsNullOrEmpty(_cdpUrl))
                    return """{"error": "No CDP URL available to close the browser."}""";

                var session = new BrowserSession(sid, timeoutSeconds);
                var result = await session.CloseAsync(_cdpUrl);

                _cdpUrl = null;
                _liveViewUrl = null;

                return result;
            },
            "close_browser_session",
            "Close a browser session. Detaches playwright-cli and closes the remote browser.");
    }

    /// <summary>
    /// Create the get_live_view_url tool. Returns a placeholder — real URL is injected by middleware.
    /// </summary>
    public static AITool MakeGetLiveViewUrl()
    {
        return AIFunctionFactory.Create(
            () =>
            {
                if (!string.IsNullOrEmpty(_liveViewUrl))
                {
                    return "Live view URL is available. It will be injected at the end of your response automatically.";
                }
                return "No live view URL available for this session.";
            },
            "get_live_view_url",
            "Get the live view URL for the current browser session so the user can interact directly (e.g. for CAPTCHA, MFA, login).");
    }
}
