// Copyright (c) Microsoft. All rights reserved.

/*
 * Local browser ↔ Foundry WebSocket bridge.
 *
 * Browsers cannot set Authorization headers on WebSocket upgrade requests.
 * This proxy:
 *
 *   • serves the static page (chat_client/index.html) on /  and /index.html;
 *   • upgrades inbound /invocations_ws to a WebSocket;
 *   • opens an outbound WebSocket to the Foundry hosted agent endpoint
 *     with Authorization: Bearer <Entra token> + Foundry-Features headers;
 *   • forwards binary and text frames in both directions verbatim.
 *
 * Run:
 *
 *   dotnet run --project chat_client/Proxy -- \
 *     --foundry https://<account>.services.ai.azure.com/api/projects/<proj> \
 *     [--agent hello-world-dotnet-invocations-ws]
 *
 * Then open http://localhost:8765/ in a browser.
 */

using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;
using System.Web;

string? foundry = null;
string agent = "hello-world-dotnet-invocations-ws";
int port = 8765;
string apiVersion = "v1";
string resource = "https://ai.azure.com";

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--foundry":     foundry = args[++i]; break;
        case "--agent":       agent = args[++i]; break;
        case "--port":        port = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--api-version": apiVersion = args[++i]; break;
        case "--resource":    resource = args[++i]; break;
        case "--help" or "-h":
            Console.WriteLine("Usage: dotnet run --project chat_client/Proxy -- --foundry URL [options]");
            Console.WriteLine("  --foundry URL          Foundry project endpoint (required)");
            Console.WriteLine("                         e.g. https://<account>.services.ai.azure.com/api/projects/<proj>");
            Console.WriteLine($"  --agent NAME           Hosted agent name (default: {agent})");
            Console.WriteLine($"  --port PORT            Local HTTP port (default: {port})");
            Console.WriteLine($"  --api-version VERSION  Foundry API version (default: {apiVersion})");
            Console.WriteLine($"  --resource URL         Entra resource for the token (default: {resource})");
            return 0;
        default:
            Console.Error.WriteLine($"unknown argument: {args[i]}");
            return 2;
    }
}

if (string.IsNullOrWhiteSpace(foundry))
{
    Console.Error.WriteLine("error: --foundry is required");
    return 2;
}
foundry = foundry.TrimEnd('/');

var builder = WebApplication.CreateBuilder();
builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "HH:mm:ss ";
});
builder.WebHost.ConfigureKestrel(o => o.ListenLocalhost(port));

var app = builder.Build();
app.UseWebSockets();

var indexPath = LocateIndexHtml()
    ?? throw new FileNotFoundException("could not locate chat_client/index.html");

app.MapGet("/", () => Results.File(indexPath, "text/html; charset=utf-8"));
app.MapGet("/index.html", () => Results.File(indexPath, "text/html; charset=utf-8"));

app.Map("/invocations_ws", async (HttpContext ctx, ILoggerFactory loggerFactory) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest)
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var log = loggerFactory.CreateLogger("proxy");
    using var browser = await ctx.WebSockets.AcceptWebSocketAsync();
    var sessionId = $"chat-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
    var foundryWsUrl = BuildFoundryUrl(foundry, agent, sessionId, apiVersion);

    log.LogInformation("opening upstream {url}", foundryWsUrl);
    string token;
    try
    {
        token = await GetEntraTokenAsync(resource);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "failed to obtain Entra token");
        await browser.CloseAsync(WebSocketCloseStatus.PolicyViolation, "auth", CancellationToken.None);
        return;
    }

    using var upstream = new ClientWebSocket();
    upstream.Options.SetRequestHeader("Authorization", $"Bearer {token}");
    upstream.Options.SetRequestHeader("Foundry-Features", "HostedAgents=V1Preview");
    try
    {
        await upstream.ConnectAsync(new Uri(foundryWsUrl), ctx.RequestAborted);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "upstream connect failed");
        await browser.CloseAsync(WebSocketCloseStatus.InternalServerError, "upstream", CancellationToken.None);
        return;
    }

    log.LogInformation("bridge open: browser ↔ {sessionId}", sessionId);
    using var pumpCts = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
    var b2u = PumpAsync(browser, upstream, "browser→upstream", log, pumpCts);
    var u2b = PumpAsync(upstream, browser, "upstream→browser", log, pumpCts);
    await Task.WhenAny(b2u, u2b);
    pumpCts.Cancel();
    try { await Task.WhenAll(b2u, u2b); } catch { /* ignore */ }
    log.LogInformation("bridge closed: {sessionId}", sessionId);
});

Console.WriteLine($"[proxy] serving http://localhost:{port}/  (Ctrl-C to stop)");
Console.WriteLine($"[proxy] bridging /invocations_ws → {foundry}  agent={agent}");
await app.RunAsync();
return 0;

static string? LocateIndexHtml()
{
    foreach (var dir in CandidateDirs())
    {
        var p = Path.Combine(dir, "index.html");
        if (File.Exists(p)) return Path.GetFullPath(p);
    }
    return null;

    static IEnumerable<string> CandidateDirs()
    {
        // Project layout: chat_client/index.html sits two levels above the Proxy project dir.
        var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        yield return Path.Combine(projectDir, "chat_client");
        yield return Path.Combine(Directory.GetCurrentDirectory(), "chat_client");
        yield return Path.Combine(Directory.GetCurrentDirectory(), "..");
    }
}

static async Task PumpAsync(
    WebSocket src,
    WebSocket dst,
    string label,
    ILogger log,
    CancellationTokenSource cts)
{
    var buffer = new byte[64 * 1024];
    try
    {
        while (src.State == WebSocketState.Open && !cts.IsCancellationRequested)
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await src.ReceiveAsync(buffer, cts.Token);
                if (result.MessageType == WebSocketMessageType.Close) break;
                ms.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                if (dst.State == WebSocketState.Open)
                {
                    await dst.CloseAsync(
                        result.CloseStatus ?? WebSocketCloseStatus.NormalClosure,
                        result.CloseStatusDescription,
                        CancellationToken.None);
                }
                return;
            }

            await dst.SendAsync(
                ms.GetBuffer().AsMemory(0, (int)ms.Length),
                result.MessageType,
                endOfMessage: true,
                cts.Token);
        }
    }
    catch (OperationCanceledException) { /* shutdown */ }
    catch (WebSocketException ex)
    {
        log.LogInformation("{label}: closed ({reason})", label, ex.Message);
    }
}

static string BuildFoundryUrl(string projectEndpoint, string agent, string sessionId, string apiVersion)
{
    var parts = new Uri(projectEndpoint);
    var project = parts.AbsolutePath.TrimEnd('/').Split('/')[^1];
    var qs = HttpUtility.ParseQueryString(string.Empty);
    qs["api-version"] = apiVersion;
    qs["agent_session_id"] = sessionId;
    var scheme = parts.Scheme is "https" or "wss" ? "wss" : "ws";
    var path = $"/api/projects/{Uri.EscapeDataString(project)}/agents/{Uri.EscapeDataString(agent)}/endpoint/protocols/invocations_ws";
    return $"{scheme}://{parts.Host}{path}?{qs}";
}

static async Task<string> GetEntraTokenAsync(string resource)
{
    var psi = new ProcessStartInfo("az",
        ["account", "get-access-token", "--resource", resource, "-o", "json"])
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    using var proc = Process.Start(psi)
        ?? throw new InvalidOperationException("failed to start `az`");
    var stdout = await proc.StandardOutput.ReadToEndAsync();
    var stderr = await proc.StandardError.ReadToEndAsync();
    await proc.WaitForExitAsync();
    if (proc.ExitCode != 0)
        throw new InvalidOperationException($"`az account get-access-token` failed: {stderr.Trim()}");
    using var doc = JsonDocument.Parse(stdout);
    return doc.RootElement.GetProperty("accessToken").GetString()
        ?? throw new InvalidOperationException("accessToken missing from `az` output");
}
