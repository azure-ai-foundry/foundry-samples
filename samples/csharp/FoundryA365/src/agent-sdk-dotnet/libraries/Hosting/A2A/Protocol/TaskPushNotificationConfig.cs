// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

/// <summary>
/// A container associating a push notification configuration with a specific task.
/// </summary>
public sealed class TaskPushNotificationConfig
{
    /// <summary>
    /// The unique identifier of the task.
    /// </summary>
    [JsonPropertyName("taskId")]
    public required string TaskId { get; set; }

    /// <summary>
    /// Configuration for the agent to send push notifications for updates after the initial response.
    /// </summary>
    [JsonPropertyName("pushNotificationConfig")]
    public required PushNotificationConfig PushNotificationConfig { get; set; }
}
