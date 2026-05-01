// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

/// <summary>
/// Defines authentication details for a push notification endpoint.
/// </summary>
public sealed class PushNotificationAuthenticationInfo
{
    /// <summary>
    /// A list of supported authentication schemes (e.g., 'Basic', 'Bearer')
    /// </summary>
    [JsonPropertyName("schemes")]
    public required ImmutableArray<string> Schemes { get; set; }

    /// <summary>
    /// Optional credentials required by the push notification endpoint.
    /// </summary>
    [JsonPropertyName("credentials")]
    public string? Credentials { get; set; }
}
