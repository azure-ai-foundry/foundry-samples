// Copyright (c) Microsoft. All rights reserved.

using System.Net.Http.Headers;
using Azure.Core;
using ModelContextProtocol.Client;

namespace ClawSample;

/// <summary>
/// Helpers for wiring centrally-managed <b>Foundry skills</b> into the claw via a Foundry Toolbox
/// MCP endpoint. These are opt-in: skills published to the toolbox are discovered at runtime, so
/// they can be managed and updated without changing or redeploying the agent.
/// </summary>
internal static class FoundrySkills
{
    /// <summary>
    /// Connects to a Foundry Toolbox MCP endpoint and returns a connected <see cref="McpClient"/>.
    /// The caller owns the returned client and its HTTP client.
    /// </summary>
    /// <param name="toolboxMcpServerUrl">The Foundry Toolbox MCP server URL.</param>
    /// <param name="credential">Credential used to obtain a bearer token for the toolbox.</param>
    /// <returns>The connected MCP client and the underlying HTTP client; both must be disposed by the caller.</returns>
    public static async Task<(McpClient McpClient, HttpClient HttpClient)> ConnectAsync(
        string toolboxMcpServerUrl,
        TokenCredential credential)
    {
        var endpointBuilder = new UriBuilder(toolboxMcpServerUrl);
        string[] queryParameters = endpointBuilder.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries);
        int apiVersionIndex = Array.FindIndex(
            queryParameters,
            parameter => parameter.Split('=', 2)[0].Equals("api-version", StringComparison.OrdinalIgnoreCase));

        if (apiVersionIndex >= 0)
        {
            queryParameters[apiVersionIndex] = "api-version=v1";
        }
        else
        {
            queryParameters = [.. queryParameters, "api-version=v1"];
        }

        endpointBuilder.Query = string.Join('&', queryParameters);

        var httpClient = new HttpClient(new BearerTokenHandler(credential, "https://ai.azure.com/.default")
        {
            InnerHandler = new HttpClientHandler
            {
                CheckCertificateRevocationList = true,
            },
        });

        try
        {
            McpClient mcpClient = await McpClient.CreateAsync(
                new HttpClientTransport(
                    new HttpClientTransportOptions
                    {
                        Endpoint = endpointBuilder.Uri,
                        Name = "foundry_toolbox",
                        TransportMode = HttpTransportMode.StreamableHttp,
                    },
                    httpClient));

            return (mcpClient, httpClient);
        }
        catch
        {
            // The MCP client never took ownership of the HTTP client, so dispose it here.
            httpClient.Dispose();
            throw;
        }
    }

    private sealed class BearerTokenHandler(TokenCredential credential, string scope) : DelegatingHandler
    {
        private readonly TokenRequestContext _tokenContext = new([scope]);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AccessToken token = await credential.GetTokenAsync(this._tokenContext, cancellationToken).ConfigureAwait(false);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }
}
