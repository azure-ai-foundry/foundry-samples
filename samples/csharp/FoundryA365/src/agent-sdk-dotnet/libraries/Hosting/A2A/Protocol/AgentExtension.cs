// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

/// <summary>
/// A declaration of a protocol extension supported by an Agent.
/// </summary>
public sealed class AgentExtension
{
    /// <summary>
    /// The unique URI identifying the extension.
    /// </summary>
    [JsonPropertyName("url")]
    public required string Url { get; set; }

    /// <summary>
    /// If true, the client must understand and comply with the extension's requirements to interact with the agent.
    /// </summary>
    [JsonPropertyName("required")]
    public bool? Required { get; set; } = false;

    /// <summary>
    /// A human-readable description of how this agent uses the extension.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Optional, extension-specific configuration parameters.
    /// </summary>
    [JsonPropertyName("params")]
    public object? Params { get; set; }
}
