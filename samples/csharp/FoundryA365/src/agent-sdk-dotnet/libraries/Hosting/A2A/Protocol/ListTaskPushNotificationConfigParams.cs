// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

/// <summary>
/// Defines parameters for listing all push notification configurations associated with a task.
/// </summary>
public sealed class ListTaskPushNotificationConfigParams
{
    /// <summary>
    /// The unique identifier of the task.
    /// </summary>
    [JsonPropertyName("id")]
    public required string TaskId { get; set; }

    /// <summary>
    /// Optional metadata associated with the request.
    /// </summary>
    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, object>? Metadata { get; set; }
}
