// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

/// <summary>
/// Carries information about a change in the task's status during streaming. 
/// This is one of the possible result types in a <see cref="Microsoft.Agents.Hosting.A2A.Protocol.SendStreamingMessageResponse"/>.
/// </summary>
public sealed class TaskStatusUpdateEvent
{
    [JsonPropertyName("kind")]
    public string Kind { get; } = "status-update";

    /// <summary>
    /// Task ID being updated
    /// </summary>
    [JsonPropertyName("taskId")]
    public required string TaskId { get; set; }

    /// <summary>
    /// Context ID the task is associated with
    /// </summary>
    [JsonPropertyName("contextId")]
    public required string ContextId { get; set; }

    /// <summary>
    /// The new TaskStatus object.
    /// </summary>
    [JsonPropertyName("status")]
    public required TaskStatus Status { get; set; }

    /// <summary>
    /// If true, indicates this is the terminal status update for the current stream cycle. 
    /// The server typically closes the SSE connection after this.
    /// </summary>
    [JsonPropertyName("final")]
    public bool? Final { get; set; }

    /// <summary>
    /// Event-specific metadata.
    /// </summary>
    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, object>? Metadata { get; set; }
}