// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

/// <summary>
/// Defines configuration options for a `message/send` or `message/stream` request.
/// </summary>
public sealed class MessageSendConfiguration
{
    /// <summary>
    /// A list of output MIME types the client is prepared to accept in the response.
    /// </summary>
    [JsonPropertyName("acceptedOutputModes")]
    public ImmutableArray<string>? AcceptedOutputModes { get; set; }

    /// <summary>
    /// If true, the client will wait for the task to complete. The server may reject this if the task is long-running.
    /// </summary>
    [JsonPropertyName("blocking")]
    public bool? Blocking { get; set; }

    /// <summary>
    /// The number of most recent messages from the task's history to retrieve in the response.
    /// </summary>
    [JsonPropertyName("historyLength")]
    public int? HistoryLength { get; set; }

    /// <summary>
    /// Configuration for the agent to send push notifications for updates after the initial response.
    /// </summary>
    [JsonPropertyName("pushNotificationConfig")]
    public PushNotificationConfig? PushNotificationConfig { get; set; }
}