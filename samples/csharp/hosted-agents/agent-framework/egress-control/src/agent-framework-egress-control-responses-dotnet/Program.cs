// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using System.Net.Security;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.AI.AgentServer.Core;
using Azure.AI.Projects;
using Azure.Identity;
using DotNetEnv;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;

Env.NoClobber().TraversePath().Load();

var projectEndpoint = new Uri(Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT environment variable is not set."));
var deployment = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME") ?? "gpt-4.1";

AIAgent agent = new AIProjectClient(projectEndpoint, new DefaultAzureCredential())
    .AsAIAgent(
        model: deployment,
        instructions: """
            You are an egress control test agent. Call the EgressTest tool with the user's
            complete message as the command argument. Return the tool output verbatim.
            If the user does not provide a recognized command, call the tool with "help".
            """,
        name: "egress-control",
        description: "Tests managed egress policies from a Foundry hosted agent",
        tools:
        [
            AIFunctionFactory.Create(
                EgressControlTool.ExecuteAsync,
                "EgressTest",
                "Runs an outbound HTTP request for an egress-control test command.")
        ]);

var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();

static class EgressControlTool
{
    private const int MaximumBodyLength = 4000;
    private const string HelpText = """
        **Egress Test Agent — Supported Commands**

        | Command | Description |
        |---------|-------------|
        | `test egress to <url>` | GET request — returns status + body |
        | `test headers to <url>` | GET request — returns response headers + body |
        | `test response headers from <url>` | GET request — returns response headers only |
        | `test post to <url> <json>` | POST with JSON body |
        | `test connectivity` | Probe httpbin.org, example.com, google.com |
        | `help` | Show this help message |
        """;

    private static readonly Regex HeadersPattern = new(
        @"^test\s+headers\s+to\s+(\S+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ResponseHeadersPattern = new(
        @"^test\s+response\s+headers\s+from\s+(\S+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PostPattern = new(
        @"^test\s+post\s+to\s+(\S+)\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex EgressPattern = new(
        @"^test\s+egress\s+to\s+(\S+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [Description("Parses and executes an egress test command.")]
    public static async Task<string> ExecuteAsync(
        [Description("The complete user command, such as 'test egress to https://httpbin.org/get'.")]
        string command,
        CancellationToken cancellationToken = default)
    {
        var text = command.Trim();
        if (text.Equals("help", StringComparison.OrdinalIgnoreCase) || text == "?")
        {
            return HelpText;
        }

        if (text.Equals("test connectivity", StringComparison.OrdinalIgnoreCase))
        {
            return await TestConnectivityAsync(cancellationToken);
        }

        var match = HeadersPattern.Match(text);
        if (match.Success)
        {
            var url = match.Groups[1].Value;
            return $"**Headers test to `{url}`**\n\n{FormatResult(await SendAsync(HttpMethod.Get, url, null, cancellationToken), true)}";
        }

        match = ResponseHeadersPattern.Match(text);
        if (match.Success)
        {
            var url = match.Groups[1].Value;
            var result = await SendAsync(HttpMethod.Get, url, null, cancellationToken);
            return result.Error is not null
                ? $"**Error**: {result.Error}"
                : $"**Response headers from `{url}`**\n\n```json\n{JsonSerializer.Serialize(result.Headers, JsonOptions)}\n```";
        }

        match = PostPattern.Match(text);
        if (match.Success)
        {
            var url = match.Groups[1].Value;
            JsonElement body;
            try
            {
                body = JsonSerializer.Deserialize<JsonElement>(match.Groups[2].Value);
            }
            catch (JsonException exception)
            {
                return $"**Error**: invalid JSON body — {exception.Message}";
            }

            return $"**POST to `{url}`**\n\n{FormatResult(await SendAsync(HttpMethod.Post, url, body, cancellationToken))}";
        }

        match = EgressPattern.Match(text);
        if (match.Success)
        {
            var url = match.Groups[1].Value;
            return $"**Egress test to `{url}`**\n\n{FormatResult(await SendAsync(HttpMethod.Get, url, null, cancellationToken))}";
        }

        return $"Unknown command: `{text}`\n\nSend `help` to see supported commands.";
    }

    private static async Task<string> TestConnectivityAsync(CancellationToken cancellationToken)
    {
        string[] targets =
        [
            "https://httpbin.org/get",
            "https://example.com",
            "https://www.google.com"
        ];

        var results = await Task.WhenAll(
            targets.Select(target => SendAsync(HttpMethod.Get, target, null, cancellationToken)));
        var output = new StringBuilder(
            "**Connectivity probe results**\n\n| | Target | Result |\n|---|--------|--------|\n");

        for (var index = 0; index < targets.Length; index++)
        {
            var result = results[index];
            var succeeded = result.StatusCode is >= 200 and < 400;
            var detail = result.Error ?? $"status={result.StatusCode}";
            output.AppendLine($"| {(succeeded ? "PASS" : "FAIL")} | `{targets[index]}` | {detail} |");
        }

        return output.ToString();
    }

    private static async Task<HttpResult> SendAsync(
        HttpMethod method,
        string url,
        JsonElement? body,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            if (!Uri.TryCreate($"https://{url}", UriKind.Absolute, out uri))
            {
                return new HttpResult(url, null, null, null, "Invalid URL.");
            }
        }

        using var request = new HttpRequestMessage(method, uri);
        request.Headers.UserAgent.ParseAdd("egress-test-agent-dotnet/1.0");
        request.Headers.Add("X-Test-Marker", "egress-header-test");
        if (body.HasValue)
        {
            request.Content = JsonContent.Create(body.Value);
        }

        try
        {
            using var response = await HttpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var headers = response.Headers
                .Concat(response.Content.Headers)
                .GroupBy(header => header.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => string.Join(", ", group.SelectMany(header => header.Value)),
                    StringComparer.OrdinalIgnoreCase);

            return new HttpResult(
                response.RequestMessage?.RequestUri?.ToString() ?? uri.ToString(),
                (int)response.StatusCode,
                headers,
                responseBody[..Math.Min(responseBody.Length, MaximumBodyLength)],
                null);
        }
        catch (HttpRequestException exception)
        {
            return new HttpResult(uri.ToString(), null, null, null, $"{exception.GetType().Name}: {exception.Message}");
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new HttpResult(uri.ToString(), null, null, null, $"{exception.GetType().Name}: request timed out");
        }
    }

    private static string FormatResult(HttpResult result, bool includeHeaders = false)
    {
        if (result.Error is not null)
        {
            return $"**Error**: {result.Error}";
        }

        var output = new StringBuilder($"**Status**: {result.StatusCode}");
        if (includeHeaders)
        {
            output.Append($"\n\n**Response headers**:\n```json\n{JsonSerializer.Serialize(result.Headers, JsonOptions)}\n```");
        }

        if (!string.IsNullOrEmpty(result.Body))
        {
            output.Append($"\n\n**Body** (first {MaximumBodyLength} chars):\n```\n{result.Body}\n```");
        }

        return output.ToString();
    }

    private static HttpClient CreateHttpClient()
    {
        var verifyTls = !string.Equals(
            Environment.GetEnvironmentVariable("EGRESS_TEST_VERIFY_TLS"),
            "false",
            StringComparison.OrdinalIgnoreCase);
        var handler = new HttpClientHandler();
        if (!verifyTls)
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }
        else if (Environment.GetEnvironmentVariable("SSL_CERT_FILE") is { Length: > 0 } certFile)
        {
            if (!File.Exists(certFile))
            {
                throw new InvalidOperationException(
                    $"SSL_CERT_FILE points to a missing certificate bundle: {certFile}");
            }

            var trustedRoots = new X509Certificate2Collection();
            trustedRoots.ImportFromPemFile(certFile);
            handler.ServerCertificateCustomValidationCallback =
                (_, certificate, chain, policyErrors) =>
                    ValidateCertificate(certificate, chain, policyErrors, trustedRoots);
        }

        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
    }

    private static bool ValidateCertificate(
        X509Certificate2? certificate,
        X509Chain? presentedChain,
        SslPolicyErrors policyErrors,
        X509Certificate2Collection trustedRoots)
    {
        if (policyErrors == SslPolicyErrors.None)
        {
            return true;
        }

        if (certificate is null ||
            policyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
        {
            return false;
        }

        using var customChain = new X509Chain();
        customChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        customChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        customChain.ChainPolicy.CustomTrustStore.AddRange(trustedRoots);

        if (presentedChain is not null)
        {
            foreach (var element in presentedChain.ChainElements.Cast<X509ChainElement>().Skip(1))
            {
                customChain.ChainPolicy.ExtraStore.Add(element.Certificate);
            }
        }

        return customChain.Build(certificate);
    }

    private sealed record HttpResult(
        string Url,
        int? StatusCode,
        IReadOnlyDictionary<string, string>? Headers,
        string? Body,
        string? Error);
}
