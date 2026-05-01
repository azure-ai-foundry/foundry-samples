// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

/// <summary>
/// Defines parameters for fetching a specific push notification configuration for a task.
/// </summary>
public sealed class DeleteTaskPushNotificationConfigParams
{
    /// <summary>
    /// The unique identifier of the task.
    /// </summary>
    [JsonPropertyName("id")]
    public required string TaskId { get; set; }

    /// <summary>
    /// The ID of the push notification configuration to retrieve.
    /// </summary>
    [JsonPropertyName("pushNotificationConfigId")]
    public string? PushNotificationConfigId { get; set; }

    /// <summary>
    /// Optional metadata associated with the request.
    /// </summary>
    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, object>? Metadata { get; set; }
}
