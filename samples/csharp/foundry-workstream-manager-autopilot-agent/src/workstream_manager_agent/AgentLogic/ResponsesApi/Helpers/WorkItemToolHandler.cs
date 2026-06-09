namespace WorkstreamManager.AgentLogic.ResponsesApi.Helpers;

using WorkstreamManager.Models;
using WorkstreamManager.Services;
using Microsoft.Agents.Builder;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Handles work item tool definitions, execution, and the /workstreamsummary command.
/// </summary>
internal class WorkItemToolHandler
{
    private readonly AgentMetadata _agentMetadata;
    private readonly ILogger _logger;
    private readonly string? _graphAccessToken;
    private readonly HttpClient _httpClient;
    private readonly WorkItemService? _workItemService;

    private string? _currentActivityId;
    private string? _currentConversationId;

    public WorkItemToolHandler(
        AgentMetadata agentMetadata,
        ILogger logger,
        string? graphAccessToken,
        HttpClient httpClient,
        WorkItemService? workItemService)
    {
        _agentMetadata = agentMetadata;
        _logger = logger;
        _graphAccessToken = graphAccessToken;
        _httpClient = httpClient;
        _workItemService = workItemService;
    }

    /// <summary>
    /// Sets the current activity context so the 📌 reaction can target the correct message.
    /// Call this at the start of each turn before invoking the Responses API.
    /// </summary>
    public void SetCurrentActivityContext(string? activityId, string? conversationId)
    {
        _currentActivityId = activityId;
        _currentConversationId = conversationId;
    }

    /// <summary>
    /// Returns the JSON tool definitions for work item CRUD operations.
    /// Returns an empty list if WorkItemService is not configured.
    /// </summary>
    public List<JsonNode> GetToolDefinitions()
    {
        if (_workItemService == null)
        {
            return [];
        }

        var tools = new List<JsonNode>();

        tools.Add(JsonNode.Parse("""
        {
            "type": "function",
            "name": "create_work_item",
            "description": "Creates a new work item to track an action item, task, or deliverable.",
            "parameters": {
                "type": "object",
                "properties": {
                    "name": { "type": "string", "description": "Short title of the work item" },
                    "description": { "type": "string", "description": "Detailed description of what needs to be done" },
                    "owner": { "type": "string", "description": "Display name of the person responsible" },
                    "eta": { "type": "string", "format": "date-time", "description": "Expected completion date in ISO 8601 format (e.g. 2026-06-15T17:00:00Z)" }
                },
                "required": ["name", "description", "owner"],
                "additionalProperties": false
            }
        }
        """)!);

        tools.Add(JsonNode.Parse("""
        {
            "type": "function",
            "name": "list_work_items",
            "description": "Lists tracked work items. Can filter by status (open/closed), owner name, or name keyword.",
            "parameters": {
                "type": "object",
                "properties": {
                    "status_filter": { "type": "string", "description": "Filter by status: open, closed, or omit for all" },
                    "owner_filter": { "type": "string", "description": "Filter by exact owner display name" },
                    "name_filter": { "type": "string", "description": "Filter by keyword in work item name" }
                },
                "additionalProperties": false
            }
        }
        """)!);

        tools.Add(JsonNode.Parse("""
        {
            "type": "function",
            "name": "update_work_item",
            "description": "Updates an existing work item. Provide the work item ID and fields to change.",
            "parameters": {
                "type": "object",
                "properties": {
                    "id": { "type": "string", "description": "The work item ID (GUID)" },
                    "name": { "type": "string", "description": "New title" },
                    "description": { "type": "string", "description": "New description" },
                    "owner": { "type": "string", "description": "New owner display name" },
                    "eta": { "type": "string", "format": "date-time", "description": "New ETA in ISO 8601 format (e.g. 2026-06-15T17:00:00Z)" },
                    "status": { "type": "string", "description": "New status (open/closed)" }
                },
                "required": ["id"],
                "additionalProperties": false
            }
        }
        """)!);

        tools.Add(JsonNode.Parse("""
        {
            "type": "function",
            "name": "close_work_item",
            "description": "Closes/completes a work item by setting its status to closed.",
            "parameters": {
                "type": "object",
                "properties": {
                    "id": { "type": "string", "description": "The work item ID (GUID) to close" }
                },
                "required": ["id"],
                "additionalProperties": false
            }
        }
        """)!);

        return tools;
    }

    /// <summary>
    /// Attempts to execute a local work item tool call. Returns null if the tool name is unrecognized.
    /// </summary>
    public async Task<string?> TryExecuteAsync(string toolName, string arguments)
    {
        if (_workItemService == null)
        {
            return null;
        }

        var partitionKey = $"{_agentMetadata.TenantId:D}:{_agentMetadata.UserId:D}";

        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(arguments) ? "{}" : arguments);
            var args = doc.RootElement;

            switch (toolName)
            {
                case "create_work_item":
                {
                    var name = args.GetProperty("name").GetString() ?? string.Empty;
                    var description = args.TryGetProperty("description", out var descriptionProp) ? descriptionProp.GetString() ?? string.Empty : string.Empty;
                    var owner = args.TryGetProperty("owner", out var ownerProp) ? ownerProp.GetString() ?? string.Empty : string.Empty;
                    var eta = args.TryGetProperty("eta", out var etaProp) ? etaProp.GetString() ?? string.Empty : string.Empty;
                    var ownerAadObjectId = await ResolveUserAadObjectIdAsync(owner) ?? string.Empty;
                    var result = await _workItemService.CreateWorkItemAsync(partitionKey, name, description, owner, ownerAadObjectId, eta);

                    await TrySetPinReactionAsync();

                    return result;
                }
                case "list_work_items":
                {
                    var statusFilter = args.TryGetProperty("status_filter", out var statusProp) ? statusProp.GetString() : null;
                    var ownerFilter = args.TryGetProperty("owner_filter", out var ownerProp) ? ownerProp.GetString() : null;
                    var nameFilter = args.TryGetProperty("name_filter", out var nameProp) ? nameProp.GetString() : null;
                    return await _workItemService.ListWorkItemsAsync(partitionKey, statusFilter, ownerFilter, nameFilter);
                }
                case "update_work_item":
                {
                    var id = args.GetProperty("id").GetString() ?? string.Empty;
                    var name = args.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                    var description = args.TryGetProperty("description", out var descriptionProp) ? descriptionProp.GetString() : null;
                    var owner = args.TryGetProperty("owner", out var ownerProp) ? ownerProp.GetString() : null;
                    var eta = args.TryGetProperty("eta", out var etaProp) ? etaProp.GetString() : null;
                    var status = args.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;

                    string? ownerAadObjectId = null;
                    if (owner != null)
                    {
                        ownerAadObjectId = await ResolveUserAadObjectIdAsync(owner);
                    }

                    return await _workItemService.UpdateWorkItemAsync(partitionKey, id, name, description, owner, ownerAadObjectId, eta, status);
                }
                case "close_work_item":
                {
                    var id = args.GetProperty("id").GetString() ?? string.Empty;
                    return await _workItemService.CloseWorkItemAsync(partitionKey, id);
                }
                default:
                    return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing local tool {ToolName}", toolName);
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Handles the /workstreamsummary command. Returns true if the command was recognized and handled.
    /// </summary>
    public async Task<bool> TryHandleSummaryCommandAsync(
        ITurnContext turnContext,
        string commandPrefix,
        Func<ITurnContext, string, CancellationToken, Task> sendResponse,
        CancellationToken cancellationToken)
    {
        var text = turnContext.Activity.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (!text.StartsWith(commandPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var subCommand = text[commandPrefix.Length..].Trim();
        if (!string.Equals(subCommand, "run", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(subCommand))
        {
            await sendResponse(
                turnContext,
                $"Usage: {commandPrefix} run\n\nGenerates a summary of all open work items grouped by owner.",
                cancellationToken);
            return true;
        }

        if (_workItemService == null)
        {
            await sendResponse(
                turnContext,
                "Work item tracking is not configured. Please set WorkItemsTableServiceUri.",
                cancellationToken);
            return true;
        }

        var partitionKey = $"{_agentMetadata.TenantId:D}:{_agentMetadata.UserId:D}";
        var openItems = await _workItemService.ListOpenWorkItemEntitiesAsync(partitionKey);

        if (openItems.Count == 0)
        {
            await sendResponse(
                turnContext,
                "📋 **Workstream Summary**\n\nNo open work items found.",
                cancellationToken);
            return true;
        }

        var grouped = openItems.GroupBy(item => string.IsNullOrEmpty(item.Owner) ? "(unassigned)" : item.Owner);
        var sb = new StringBuilder();
        sb.AppendLine("📋 **Workstream Summary**");
        sb.AppendLine();
        sb.AppendLine($"Total open items: {openItems.Count}");
        sb.AppendLine();

        foreach (var group in grouped.OrderBy(group => group.Key))
        {
            sb.AppendLine($"**{group.Key}** ({group.Count()} items):");
            foreach (var item in group.OrderByDescending(workItem => workItem.Timestamp ?? DateTimeOffset.MinValue))
            {
                var eta = string.IsNullOrEmpty(item.ETA) ? string.Empty : $" (ETA: {item.ETA})";
                sb.AppendLine($"  • {item.Name}{eta} — {item.Status}");
            }

            sb.AppendLine();
        }

        await sendResponse(
            turnContext,
            sb.ToString().TrimEnd(),
            cancellationToken);
        return true;
    }

    private async Task<string?> ResolveUserAadObjectIdAsync(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(_graphAccessToken))
        {
            return null;
        }

        try
        {
            var filter = $"startswith(displayName,'{displayName.Replace("'", "''")}')";
            var url = $"https://graph.microsoft.com/v1.0/users?$filter={Uri.EscapeDataString(filter)}&$select=id,displayName&$top=1";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _graphAccessToken);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Graph user resolution failed for '{DisplayName}': {Status}", displayName, response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("value", out var users) && users.ValueKind == JsonValueKind.Array && users.GetArrayLength() > 0)
            {
                var userId = users[0].GetProperty("id").GetString();
                _logger.LogInformation("Resolved '{DisplayName}' to AAD ID: {AadId}", displayName, userId);
                return userId;
            }

            _logger.LogWarning("No Graph user found for display name '{DisplayName}'", displayName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error resolving user AAD ID for '{DisplayName}'", displayName);
            return null;
        }
    }

    /// <summary>
    /// Sets a 📌 reaction on the current message via Graph.
    /// </summary>
    private async Task TrySetPinReactionAsync()
    {
        if (string.IsNullOrWhiteSpace(_graphAccessToken) ||
            string.IsNullOrWhiteSpace(_currentActivityId) ||
            string.IsNullOrWhiteSpace(_currentConversationId))
        {
            _logger.LogDebug("Skipping 📌 reaction: missing Graph token or activity context.");
            return;
        }

        try
        {
            var setReactionUrl =
                $"https://graph.microsoft.com/v1.0/chats/{_currentConversationId}/messages/{_currentActivityId}/setReaction";

            using var request = new HttpRequestMessage(HttpMethod.Post, setReactionUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _graphAccessToken);
            request.Content = new StringContent(
                "{\"reactionType\": \"\\uD83D\\uDCCC\"}",
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("📌 reaction set on message {ActivityId}", _currentActivityId);
            }
            else
            {
                _logger.LogWarning("Failed to set 📌 reaction: {Status} {Reason}",
                    response.StatusCode, await response.Content.ReadAsStringAsync());
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error setting 📌 reaction on message {ActivityId}", _currentActivityId);
        }
    }
}

