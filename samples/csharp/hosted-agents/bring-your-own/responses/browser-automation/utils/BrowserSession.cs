// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics;

namespace BrowserAutomation;

/// <summary>
/// Manages a playwright-cli session against a remote CDP browser.
/// </summary>
public class BrowserSession
{
    public string SessionId { get; }
    public bool Connected { get; private set; }

    private readonly int _timeoutSeconds;
    private readonly ILogger? _logger;

    public BrowserSession(string sessionId, int timeoutSeconds = 180, ILogger? logger = null)
    {
        SessionId = sessionId;
        _timeoutSeconds = timeoutSeconds;
        _logger = logger;
    }

    /// <summary>Attach to a remote browser via CDP URL.</summary>
    public async Task<(bool Success, string Output)> ConnectAsync(string cdpUrl)
    {
        var result = await RunCommandAsync($"attach --cdp={cdpUrl}");
        Connected = result.Success;
        return result;
    }

    /// <summary>Run a playwright-cli command in this session.</summary>
    public async Task<(bool Success, string Output)> RunAsync(string command, string[] args)
    {
        if (!Connected)
            return (false, "Browser not connected. Session may need to be recreated.");

        var fullCmd = command;
        if (args.Length > 0)
        {
            var quoted = args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a);
            fullCmd += " " + string.Join(" ", quoted);
        }
        return await RunCommandAsync(fullCmd);
    }

    /// <summary>Detach from the browser session.</summary>
    public async Task CloseAsync()
    {
        if (Connected)
            await RunCommandAsync("detach");
        Connected = false;
    }

    private async Task<(bool Success, string Output)> RunCommandAsync(string command)
    {
        var cli = FindCli();
        var processArgs = $"-s={SessionId} {command}";
        _logger?.LogInformation("[pw-cli] {Cli} -s={SessionId} {Command}", cli, SessionId, Redaction.Redact(command));

        ProcessStartInfo psi = new()
        {
            FileName = cli,
            Arguments = processArgs,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start playwright-cli");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSeconds));
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                return (false, $"Command timed out after {_timeoutSeconds} seconds.");
            }

            var stdout = Redaction.Redact(Truncate(await stdoutTask));
            var stderr = Redaction.Redact(Truncate(await stderrTask));
            var success = process.ExitCode == 0;

            var output = $"exit_code: {process.ExitCode}\nstdout:\n{(string.IsNullOrEmpty(stdout) ? "<empty>" : stdout)}";
            if (!string.IsNullOrEmpty(stderr))
                output += $"\n\nstderr:\n{stderr}";

            return (success, output);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[pw-cli] Failed to run command");
            return (false, $"Error: Failed to run playwright-cli: {ex.Message}");
        }
    }

    private static string FindCli()
    {
        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        foreach (var dir in pathDirs)
        {
            var candidate = Path.Combine(dir, "playwright-cli");
            if (File.Exists(candidate)) return candidate;
            if (File.Exists(candidate + ".exe")) return candidate + ".exe";
            if (File.Exists(candidate + ".cmd")) return candidate + ".cmd";
        }
        return "playwright-cli";
    }

    private static string Truncate(string text, int maxLen = 12000) =>
        text.Length <= maxLen ? text : text[..maxLen] + "\n...[truncated]";
}
