namespace WorkstreamManager.AgentLogic.ResponsesApi;

using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using WorkstreamManager.Models;
using WorkstreamManager.Services;
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
        => await CreateForAgentAsync(agent);

    public async Task<IAgentLogicService> CreateForAgentAsync(AgentMetadata agent)
    {
        // Acquire token for MCP servers. Use a fresh AgentTokenCredential instance per scope:
        // AgentTokenCredential.cachedToken does NOT key on the requested scope, so reusing a
        // single instance across different audiences returns the first-acquired token for every
        // subsequent call — Graph would then reject our MCP-audience token with "Invalid audience".
        var mcpRequestContext = new TokenRequestContext(["ea9ffc3e-8a23-4a7d-836d-234d7c7565c1/.default"]);
        var mcpTokenCredential = new AgentTokenCredential(tokenHelper, agent);
        var accessToken = await mcpTokenCredential.GetTokenAsync(mcpRequestContext, CancellationToken.None);

        logger.LogInformation("Acquired token for Responses API MCP tools. Expires at: {Expiration}", accessToken.ExpiresOn);

        // Acquire a Microsoft Graph token alongside the MCP token so the agent logic can call
        // Graph (e.g. to resolve user identifiers for access control and work item assignment).
        // Same AgentTokenCredential pattern that A365AgentApplication already uses for setReaction.
        // We tolerate failure here — Graph lookups are an enhancement; the agent still works without them.
        string? graphAccessToken = null;
        try
        {
            var graphRequestContext = new TokenRequestContext(["https://graph.microsoft.com/.default"]);
            var graphTokenCredential = new AgentTokenCredential(tokenHelper, agent);
            var graphToken = await graphTokenCredential.GetTokenAsync(graphRequestContext, CancellationToken.None);
            graphAccessToken = graphToken.Token;
            logger.LogInformation("Acquired Graph token for chat-members lookup. Expires at: {Expiration}", graphToken.ExpiresOn);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to acquire Graph token; Graph-dependent features will be disabled.");
        }

        var mcpServers = await GetMcpServersAsync(agent.AgentId, accessToken.Token);

        IAgentLogicService service = new ResponsesApiAgentLogicService(
            agent,
            configuration,
            logger,
            accessToken.Token,
            mcpServers,
            graphAccessToken);

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

