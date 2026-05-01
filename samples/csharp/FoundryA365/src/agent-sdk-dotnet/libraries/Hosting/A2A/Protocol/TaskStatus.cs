// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

/// <summary>
/// Represents the current state and associated context (e.g., a message from the agent) of a Task.
/// </summary>
public sealed class TaskStatus
{
    /// <summary>
    /// Current lifecycle state of the task.
    /// </summary>
    [JsonPropertyName("state")]
    public required TaskState State { get; set; }

    /// <summary>
    /// Optional message providing context for the current status.
    /// </summary>
    [JsonPropertyName("message")]
    public Message? Message { get; set; }

    /// <summary>
    /// Timestamp (UTC recommended) when this status was recorded.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset? Timestamp { get; set; }
}