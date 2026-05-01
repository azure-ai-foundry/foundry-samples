// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

/// <summary>
/// Defines the configuration for setting up push notifications for task updates.
/// </summary>
public sealed class PushNotificationConfig
{
    /// <summary>
    /// The callback URL where the agent should send push notifications.
    /// </summary>
    [JsonPropertyName("url")]
    public required string Url { get; set; }

    /// <summary>
    /// A unique ID for the push notification configuration, set by the client\nto support multiple notification callbacks.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// A unique token for this task or session to validate incoming push notifications.
    /// </summary>
    [JsonPropertyName("token")]
    public string? Token { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("authentication")]
    public PushNotificationAuthenticationInfo? Authentication {  get; set; }
}
