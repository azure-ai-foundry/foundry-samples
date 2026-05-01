// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

/// <summary>
/// Declares a combination of a target URL and a transport protocol for interacting with the agent.
/// This allows agents to expose the same functionality over multiple transport mechanisms.
/// </summary>
public sealed class AgentInterface
{
    /// <summary>
    /// The transport protocol supported at this URL.
    /// <para>
    /// See <see cref="Microsoft.Agents.Hosting.A2A.Protocol.TransportProtocol"/> for the defined types.
    /// </para>
    /// </summary>
    [JsonPropertyName("transport")]
    public required TransportProtocol Transport { get; set; }

    /// <summary>
    /// The URL where this interface is available. Must be a valid absolute HTTPS URL in production.
    /// <para>
    /// Examples:
    /// <code>
    /// "https://api.example.com/a2a/v1",
    /// "https://grpc.example.com/a2a",
    /// "https://rest.example.com/v1"        
    /// </code>
    /// </para>
    /// </summary>
    [JsonPropertyName("url")]
    public required string Url { get; set; }
}
