// Copyright (c) Microsoft. All rights reserved.

/*
 * Synthetic end-to-end test for the Voice Live hello-world agent.
 *
 * Connects to ws://<host>:<port>/invocations_ws and sends a JSON text
 * message ({"type":"text","content":"..."}). This bypasses Voice Live's
 * server-VAD so the test does not need a real spoken utterance. Asserts:
 *
 *   • a session_started JSON event arrives;
 *   • at least one binary audio frame is returned (assistant speech);
 *   • a response_done event arrives;
 *   • the connection closes cleanly.
 *
 * Run:
 *
 *   dotnet run --project E2ELocal                 # against ws://localhost:8088
 *   dotnet run --project E2ELocal -- --url ws://...
 *
 * Requires the agent process to be running and the standard
 * AZURE_VOICELIVE_* env vars to be set so the agent can reach Voice Live.
 */

using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Web;

const string DefaultPrompt = "Say hello in one short sentence.";

string? url = null;
double timeoutSeconds = 45.0;
string prompt = DefaultPrompt;
string? foundryEndpoint = null;
string agent = "hello-world-dotnet-invocations-ws";
bool idleMode = false;
string apiVersion = "v1";

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--url":         url = args[++i]; break;
        case "--timeout":     timeoutSeconds = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--prompt":      prompt = args[++i]; break;
        case "--foundry":     foundryEndpoint = args[++i]; break;
        case "--agent":       agent = args[++i]; break;
        case "--api-version": apiVersion = args[++i]; break;
        case "--idle":        idleMode = true; break;
        case "--help" or "-h":
            Console.WriteLine("Usage: dotnet run --project E2ELocal -- [options]");
            Console.WriteLine("  --url URL              WebSocket URL (default: ws://localhost:8088/invocations_ws,");
            Console.WriteLine("                         or the Foundry URL if --foundry/--agent are given)");
            Console.WriteLine("  --timeout SECONDS      Hard timeout (default: 45)");
            Console.WriteLine("  --prompt TEXT          Text prompt (default: \"Say hello in one short sentence.\")");
            Console.WriteLine("  --foundry URL          Foundry project endpoint; when set, builds the public WS URL");
            Console.WriteLine("                         and sends an Entra Bearer token");
            Console.WriteLine("  --agent NAME           Hosted agent name (Foundry mode, default: hello-world-dotnet-invocations-ws)");
            Console.WriteLine("  --api-version VERSION  Foundry API version (default: v1)");
            Console.WriteLine("  --idle                 Idle re-engagement test: send no input and assert a second");
            Console.WriteLine("                         response_done arrives after the proactive greeting");
            return 0;
        default:
            Console.Error.WriteLine($"unknown argument: {args[i]}");
            return 2;
    }
}

var headers = new Dictionary<string, string>();
string targetUrl;
if (!string.IsNullOrEmpty(foundryEndpoint))
{
    var sessionId = $"e2e-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
    targetUrl = url ?? BuildFoundryUrl(foundryEndpoint.TrimEnd('/'), agent, sessionId, apiVersion);
    var token = await GetEntraTokenAsync();
    headers["Authorization"] = $"Bearer {token}";
    headers["Foundry-Features"] = "HostedAgents=V1Preview";
}
else
{
    targetUrl = url ?? "ws://localhost:8088/invocations_ws";
}

return await RunAsync(targetUrl, TimeSpan.FromSeconds(timeoutSeconds), prompt, headers, idleMode);

static async Task<int> RunAsync(
    string url,
    TimeSpan timeout,
    string prompt,
    IDictionary<string, string> headers,
    bool idleMode)
{
    Console.WriteLine($"[e2e] connecting {url} ...");

    var gotSession = false;
    var gotAudioBytes = 0;
    var responseDoneCount = 0;
    string? gotError = null;

    using var deadlineCts = new CancellationTokenSource(timeout);
    using var ws = new ClientWebSocket();
    ws.Options.SetRequestHeader("User-Agent", "voicelive-hello-world-e2e/1.0");
    foreach (var (k, v) in headers) ws.Options.SetRequestHeader(k, v);

    try
    {
        await ws.ConnectAsync(new Uri(url), deadlineCts.Token);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        Console.Error.WriteLine($"[e2e] connect failed: {ex.Message}");
        return 1;
    }

    Task? sender = null;
    if (!idleMode)
    {
        sender = Task.Run(async () =>
        {
            // Wait for the session_started signal (best-effort) before sending text.
            for (var i = 0; i < 100 && !deadlineCts.IsCancellationRequested; i++)
            {
                if (Volatile.Read(ref gotSession)) break;
                await Task.Delay(50, deadlineCts.Token);
            }
            var payload = JsonSerializer.SerializeToUtf8Bytes(new { type = "text", content = prompt });
            Console.WriteLine($"[e2e] -> text: \"{prompt}\"");
            await ws.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, deadlineCts.Token);
        }, deadlineCts.Token);
    }

    try
    {
        var buffer = new byte[64 * 1024];
        using var ms = new MemoryStream();
        while (ws.State == WebSocketState.Open && !deadlineCts.IsCancellationRequested)
        {
            ms.SetLength(0);
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(buffer, deadlineCts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
                ms.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close) break;

            if (result.MessageType == WebSocketMessageType.Binary)
            {
                if (ms.Length > 8)
                {
                    var data = ms.GetBuffer();
                    var sr = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0, 4));
                    var ch = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4, 4));
                    var pcmLen = (int)ms.Length - 8;
                    gotAudioBytes += pcmLen;
                    if (gotAudioBytes <= 4096)
                    {
                        Console.WriteLine($"[e2e] audio frame sr={sr} ch={ch} +{pcmLen}B (total {gotAudioBytes}B)");
                    }
                }
                continue;
            }

            var text = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(text);
            }
            catch (JsonException)
            {
                Console.WriteLine($"[e2e] non-json text: {text}");
                continue;
            }
            using (doc)
            {
                Console.WriteLine($"[e2e] event: {text}");
                if (!doc.RootElement.TryGetProperty("type", out var t)) continue;
                switch (t.GetString())
                {
                    case "session_started":
                        Volatile.Write(ref gotSession, true);
                        break;
                    case "error":
                        gotError = text;
                        break;
                    case "response_done":
                        responseDoneCount++;
                        // Idle mode needs greeting + re-engagement = 2.
                        var target = idleMode ? 2 : 1;
                        if (responseDoneCount >= target && gotAudioBytes > 0)
                        {
                            deadlineCts.Cancel();
                        }
                        break;
                }
            }
        }
    }
    catch (OperationCanceledException)
    {
        // Either timeout or we triggered cancellation after success.
    }
    catch (WebSocketException ex)
    {
        Console.Error.WriteLine($"[e2e] websocket error: {ex.Message}");
    }
    finally
    {
        if (sender != null)
        {
            try { await sender; }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[e2e] sender error: {ex.Message}");
            }
        }
        if (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived)
        {
            try
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
            }
            catch { /* ignore */ }
        }
    }

    Console.WriteLine();
    Console.WriteLine($"[e2e] session_started:    {gotSession}");
    Console.WriteLine($"[e2e] audio_bytes recvd:  {gotAudioBytes}");
    Console.WriteLine($"[e2e] response_done seen: {responseDoneCount}");
    Console.WriteLine($"[e2e] error:              {gotError ?? "<none>"}");

    var minResponses = idleMode ? 2 : 1;
    var ok = gotSession
        && gotAudioBytes > 0
        && responseDoneCount >= minResponses
        && gotError is null;
    Console.WriteLine($"[e2e] result:             {(ok ? "PASS" : "FAIL")}");
    return ok ? 0 : 1;
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

static async Task<string> GetEntraTokenAsync(string resource = "https://ai.azure.com")
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
