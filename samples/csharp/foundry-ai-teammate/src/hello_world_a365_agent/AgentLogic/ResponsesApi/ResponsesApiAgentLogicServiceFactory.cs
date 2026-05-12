namespace HelloWorldA365.AgentLogic.ResponsesApi;

using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using HelloWorldA365.Models;
using HelloWorldA365.Services;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;

/// <summary>
/// Factory for creating ResponsesApiAgentLogicService instances.
/// Discovers MCP servers either from the Agent365 API or from a local ToolingManifest.json,
/// controlled by the "McpDiscoverySource" config setting ("API" or "Manifest").
/// </summary>
public sealed class ResponsesApiAgentLogicServiceFactory(
    IConfiguration configuration,
    ILogger<ResponsesApiAgentLogicServiceFactory> logger,
    AgentTokenHelper tokenHelper)
{
    private static readonly HttpClient HttpClient = new();

    public async Task<IAgentLogicService> CreateAsync(AgentMetadata agent, ITurnContext turnContext, UserAuthorization userAuthorization)
    {
        // Acquire token for MCP servers
        var requestContext = new TokenRequestContext(["ea9ffc3e-8a23-4a7d-836d-234d7c7565c1/.default"]);
        var tokenCredential = new AgentTokenCredential(tokenHelper, agent);
        var accessToken = await tokenCredential.GetTokenAsync(requestContext, CancellationToken.None);

        logger.LogInformation("Acquired token for Responses API MCP tools. Expires at: {Expiration}", accessToken.ExpiresOn);

        var mcpServers = await GetMcpServersAsync(agent.AgentId, accessToken.Token);

        IAgentLogicService service = new ResponsesApiAgentLogicService(
            agent,
            configuration,
            logger,
            accessToken.Token,
            mcpServers);

        return service;
    }

    private async Task<List<McpServerConfig>> GetMcpServersAsync(Guid agentInstanceId, string accessToken)
    {
        var source = configuration["McpDiscoverySource"] ?? "API";

        if (source.Equals("Manifest", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Loading MCP servers from ToolingManifest.json");
            return LoadFromManifest();
        }

        logger.LogInformation("Discovering MCP servers from API for agent {AgentId}", agentInstanceId);
        return await DiscoverFromApiAsync(agentInstanceId, accessToken);
    }

    private List<McpServerConfig> LoadFromManifest()
    {
        var manifestPath = Path.Combine(AppContext.BaseDirectory, "ToolingManifest.json");
        if (!File.Exists(manifestPath))
        {
            logger.LogWarning("ToolingManifest.json not found at {Path}", manifestPath);
            return [];
        }

        var json = File.ReadAllText(manifestPath);
        var manifest = JsonSerializer.Deserialize<ToolingManifest>(json);
        var servers = manifest?.McpServers ?? [];
        logger.LogInformation("Loaded {Count} MCP servers from ToolingManifest.json", servers.Count);
        return servers;
    }

    private async Task<List<McpServerConfig>> DiscoverFromApiAsync(Guid agentInstanceId, string accessToken)
    {
        var url = $"https://agent365.svc.cloud.microsoft/agents/v2/{agentInstanceId}/mcpServers";
        logger.LogInformation("Discovering MCP servers from {Url}", url);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await HttpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Failed to discover MCP servers. Status: {StatusCode}, Response: {Response}", response.StatusCode, responseContent);
            return [];
        }

        var servers = JsonSerializer.Deserialize<List<McpServerConfig>>(responseContent) ?? [];
        logger.LogInformation("Discovered {Count} MCP servers for agent {AgentId}", servers.Count, agentInstanceId);

        foreach (var server in servers)
        {
            logger.LogInformation("  MCP Server: {Name} ({Url})", server.McpServerName, server.Url);
        }

        return servers;
    }
}
