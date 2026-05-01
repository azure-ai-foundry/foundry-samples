// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

/// <summary>
/// Defines parameters for deleting a specific push notification configuration for a task.
/// </summary>
public sealed class GetTaskPushNotificationConfigParams
{
    /// <summary>
    /// The unique identifier of the task.
    /// </summary>
    [JsonPropertyName("id")]
    public required string TaskId { get; set; }

    /// <summary>
    /// The ID of the push notification configuration to delete.
    /// </summary>
    [JsonPropertyName("pushNotificationConfigId")]
    public required string PushNotificationConfigId { get; set; }

    /// <summary>
    /// Optional metadata associated with the request.
    /// </summary>
    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, object>? Metadata { get; set; }
}
