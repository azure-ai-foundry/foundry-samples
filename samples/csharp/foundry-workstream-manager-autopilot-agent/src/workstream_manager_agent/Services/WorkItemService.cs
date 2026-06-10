namespace WorkstreamManager.Services;

using Azure.Data.Tables;
using Azure.Identity;
using System.Text.Json;

/// <summary>
/// Entity representing a work item stored in Azure Table Storage.
/// PartitionKey = "{tenantId}:{agentUserId}", RowKey = GUID.
/// </summary>
public class WorkItemEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public Azure.ETag ETag { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string OwnerAadObjectId { get; set; } = string.Empty;
    public string ETA { get; set; } = string.Empty;
    public string DateCreated { get; set; } = string.Empty;
    public string Status { get; set; } = "open";
    public string Changelog { get; set; } = "[]";
}

/// <summary>
/// Service for managing work items in Azure Table Storage.
/// Provides CRUD operations with changelog tracking.
/// </summary>
public class WorkItemService
{
    private readonly TableClient? _tableClient;
    private readonly ILogger<WorkItemService> _logger;
    private readonly string _tableName;

    public WorkItemService(IConfiguration configuration, ILogger<WorkItemService> logger)
    {
        _logger = logger;
        _tableName = configuration["WorkItemsTableName"] ?? "workitems";

        var tableServiceUri = configuration["WorkItemsTableServiceUri"];
        if (!string.IsNullOrEmpty(tableServiceUri))
        {
            var instanceClientId = Environment.GetEnvironmentVariable("FOUNDRY_AGENT_DEFAULT_INSTANCE_CLIENT_ID");
            var credential = !string.IsNullOrEmpty(instanceClientId)
                ? new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = instanceClientId })
                : new DefaultAzureCredential();

            var serviceClient = new TableServiceClient(new Uri(tableServiceUri), credential);
            _tableClient = serviceClient.GetTableClient(_tableName);
            _tableClient.CreateIfNotExists();
            _logger.LogInformation("WorkItemService initialized with table: {TableName} at {Uri}", _tableName, tableServiceUri);
        }
        else
        {
            _logger.LogWarning("WorkItemsTableServiceUri not configured. Work item operations will fail.");
        }
    }

    /// <summary>
    /// Creates a new work item.
    /// </summary>
    public async Task<string> CreateWorkItemAsync(string partitionKey, string name, string description, string owner, string ownerAadObjectId, string eta)
    {
        if (_tableClient == null)
            return "Error: WorkItemsTableServiceUri is not configured.";

        try
        {
            var rowKey = Guid.NewGuid().ToString();
            var now = DateTimeOffset.UtcNow.ToString("o");

            var changelogEntry = new[]
            {
                new { timestamp = now, field = "Status", oldValue = "", newValue = "open", note = "Work item created" }
            };

            var entity = new WorkItemEntity
            {
                PartitionKey = partitionKey,
                RowKey = rowKey,
                Name = name,
                Description = description,
                Owner = owner,
                OwnerAadObjectId = ownerAadObjectId,
                ETA = eta,
                DateCreated = now,
                Status = "open",
                Changelog = JsonSerializer.Serialize(changelogEntry)
            };

            await _tableClient.AddEntityAsync(entity);
            _logger.LogInformation("Created work item {RowKey} in partition {PartitionKey}", rowKey, partitionKey);
            return JsonSerializer.Serialize(new { success = true, id = rowKey, message = $"Work item '{name}' created successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating work item");
            return $"Error creating work item: {ex.Message}";
        }
    }

    /// <summary>
    /// Lists work items with optional filtering by status, owner, or name.
    /// </summary>
    public async Task<string> ListWorkItemsAsync(string partitionKey, string? statusFilter = null, string? ownerFilter = null, string? nameFilter = null)
    {
        if (_tableClient == null)
            return "Error: WorkItemsTableServiceUri is not configured.";

        try
        {
            var filter = $"PartitionKey eq '{partitionKey}'";

            if (!string.IsNullOrEmpty(statusFilter))
                filter += $" and Status eq '{statusFilter}'";

            if (!string.IsNullOrEmpty(ownerFilter))
                filter += $" and Owner eq '{ownerFilter}'";

            var items = new List<object>();
            await foreach (var row in _tableClient.QueryAsync<TableEntity>(filter))
            {
                var entity = MapTableEntity(row);

                // Client-side name filter (Table Storage doesn't support contains)
                if (!string.IsNullOrEmpty(nameFilter) &&
                    !entity.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                items.Add(new
                {
                    id = entity.RowKey,
                    name = entity.Name,
                    description = entity.Description,
                    owner = entity.Owner,
                    eta = entity.ETA,
                    dateCreated = entity.DateCreated,
                    status = entity.Status,
                    lastModified = entity.Timestamp?.ToString("o") ?? ""
                });
            }

            _logger.LogInformation("Listed {Count} work items for partition {PartitionKey}", items.Count, partitionKey);
            return JsonSerializer.Serialize(new { count = items.Count, items });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing work items");
            return $"Error listing work items: {ex.Message}";
        }
    }

    /// <summary>
    /// Updates a work item's fields and appends to its changelog.
    /// </summary>
    public async Task<string> UpdateWorkItemAsync(string partitionKey, string rowKey, string? name = null, string? description = null, string? owner = null, string? ownerAadObjectId = null, string? eta = null, string? status = null)
    {
        if (_tableClient == null)
            return "Error: WorkItemsTableServiceUri is not configured.";

        try
        {
            var response = await _tableClient.GetEntityAsync<TableEntity>(partitionKey, rowKey);
            var row = response.Value;
            var entity = MapTableEntity(row);

            var now = DateTimeOffset.UtcNow.ToString("o");
            var changelog = JsonSerializer.Deserialize<List<object>>(entity.Changelog) ?? new List<object>();
            var changes = new List<object>();

            if (name != null && name != entity.Name)
            {
                changes.Add(new { timestamp = now, field = "Name", oldValue = entity.Name, newValue = name });
                row["Name"] = name;
            }
            if (description != null && description != entity.Description)
            {
                changes.Add(new { timestamp = now, field = "Description", oldValue = entity.Description, newValue = description });
                row["Description"] = description;
            }
            if (owner != null && owner != entity.Owner)
            {
                changes.Add(new { timestamp = now, field = "Owner", oldValue = entity.Owner, newValue = owner });
                row["Owner"] = owner;
            }
            if (ownerAadObjectId != null && ownerAadObjectId != entity.OwnerAadObjectId)
            {
                changes.Add(new { timestamp = now, field = "OwnerAadObjectId", oldValue = entity.OwnerAadObjectId, newValue = ownerAadObjectId });
                row["OwnerAadObjectId"] = ownerAadObjectId;
            }
            if (eta != null && eta != entity.ETA)
            {
                changes.Add(new { timestamp = now, field = "ETA", oldValue = entity.ETA, newValue = eta });
                row["ETA"] = eta;
            }
            if (status != null && status != entity.Status)
            {
                changes.Add(new { timestamp = now, field = "Status", oldValue = entity.Status, newValue = status });
                row["Status"] = status;
            }

            if (changes.Count == 0)
                return JsonSerializer.Serialize(new { success = true, message = "No changes detected." });

            changelog.AddRange(changes);
            row["Changelog"] = JsonSerializer.Serialize(changelog);

            await _tableClient.UpdateEntityAsync(row, row.ETag, TableUpdateMode.Replace);
            _logger.LogInformation("Updated work item {RowKey} with {ChangeCount} changes", rowKey, changes.Count);
            return JsonSerializer.Serialize(new { success = true, message = $"Work item updated with {changes.Count} change(s).", changes });
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return $"Error: Work item with id '{rowKey}' not found.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating work item {RowKey}", rowKey);
            return $"Error updating work item: {ex.Message}";
        }
    }

    /// <summary>
    /// Closes a work item by setting status to "closed".
    /// </summary>
    public async Task<string> CloseWorkItemAsync(string partitionKey, string rowKey)
    {
        return await UpdateWorkItemAsync(partitionKey, rowKey, status: "closed");
    }

    /// <summary>
    /// Lists all open work items as structured entities (for reminder service).
    /// </summary>
    public async Task<List<WorkItemEntity>> ListOpenWorkItemEntitiesAsync(string partitionKey)
    {
        if (_tableClient == null)
            return [];

        var items = new List<WorkItemEntity>();
        var filter = $"PartitionKey eq '{partitionKey}' and Status ne 'closed'";
        await foreach (var row in _tableClient.QueryAsync<TableEntity>(filter))
        {
            items.Add(MapTableEntity(row));
        }
        return items;
    }

    /// <summary>
    /// Lists work items modified since a given timestamp (for reminder digest).
    /// </summary>
    public async Task<List<WorkItemEntity>> ListWorkItemsModifiedSinceAsync(string partitionKey, DateTimeOffset since)
    {
        if (_tableClient == null)
            return [];

        var items = new List<WorkItemEntity>();
        var filter = $"PartitionKey eq '{partitionKey}' and Timestamp ge datetime'{since:o}'";
        await foreach (var row in _tableClient.QueryAsync<TableEntity>(filter))
        {
            items.Add(MapTableEntity(row));
        }
        return items;
    }

    /// <summary>
    /// Gets a single work item by ID, including its full changelog.
    /// </summary>
    public async Task<string> GetWorkItemAsync(string partitionKey, string rowKey)
    {
        if (_tableClient == null)
            return "Error: WorkItemsTableServiceUri is not configured.";

        try
        {
            var response = await _tableClient.GetEntityAsync<TableEntity>(partitionKey, rowKey);
            var entity = MapTableEntity(response.Value);

            var result = new
            {
                id = entity.RowKey,
                name = entity.Name,
                description = entity.Description,
                owner = entity.Owner,
                ownerAadObjectId = entity.OwnerAadObjectId,
                eta = entity.ETA,
                dateCreated = entity.DateCreated,
                status = entity.Status,
                lastModified = entity.Timestamp?.ToString("o") ?? "",
                changelog = JsonSerializer.Deserialize<object>(entity.Changelog)
            };

            return JsonSerializer.Serialize(result);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return $"Error: Work item with id '{rowKey}' not found.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting work item {RowKey}", rowKey);
            return $"Error getting work item: {ex.Message}";
        }
    }

    private static WorkItemEntity MapTableEntity(TableEntity row)
    {
        return new WorkItemEntity
        {
            PartitionKey = row.PartitionKey,
            RowKey = row.RowKey,
            Timestamp = row.Timestamp,
            ETag = row.ETag,
            Name = row.GetString("Name") ?? string.Empty,
            Description = row.GetString("Description") ?? string.Empty,
            Owner = row.GetString("Owner") ?? string.Empty,
            OwnerAadObjectId = row.GetString("OwnerAadObjectId") ?? string.Empty,
            ETA = row.TryGetValue("ETA", out var etaVal) ? etaVal?.ToString() ?? string.Empty : string.Empty,
            DateCreated = row.GetString("DateCreated") ?? string.Empty,
            Status = row.GetString("Status") ?? "open",
            Changelog = row.GetString("Changelog") ?? "[]"
        };
    }
}

