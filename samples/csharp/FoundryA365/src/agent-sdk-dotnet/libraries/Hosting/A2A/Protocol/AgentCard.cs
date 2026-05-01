// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

/// <summary>
/// The AgentCard is a self-describing manifest for an agent. It provides essential metadata including the agent's identity, 
/// capabilities, skills, supported communication methods, and security requirements.
/// </summary>
public sealed class AgentCard
{
    /// <summary>
    /// A human-readable name for the agent.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// A human-readable description of the agent, assisting users and other agents in understanding its purpose.
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; set; }

    /// <summary>
    /// Base URL for the agent's A2A service. Must be absolute. HTTPS for production.
    /// </summary>
    [JsonPropertyName("url")]
    public required string Url { get; set; }

    /// <summary>
    /// An optional URL to an icon for the agent.
    /// </summary>
    [JsonPropertyName("iconUrl")]
    public string? IconUrl { get; set; }

    /// <summary>
    /// An optional URL to the agent's documentation.
    /// </summary>
    [JsonPropertyName("documentationUrl")]
    public string? DocumentationUrl { get; set; }

    /// <summary>
    /// Information about the agent's service provider.
    /// </summary>
    public AgentProvider? Provider { get; set; }

    /// <summary>
    /// The agent's own version number. The format is defined by the provider.
    /// </summary>
    [JsonPropertyName("version")]
    public required string Version { get; set; }

    /// <summary>
    /// The version of the A2A protocol this agent supports.
    /// </summary>
    [JsonPropertyName("protocolVersion")]
    public required string ProtocolVersion { get; set; }

    /// <summary>
    /// A declaration of optional capabilities supported by the agent.
    /// </summary>
    [JsonPropertyName("capabilities")]
    public required AgentCapabilities Capabilities { get; set; } = new AgentCapabilities();

    /// <summary>
    /// A list of security requirement objects that apply to all agent interactions. Each object lists security schemes that can be used. Follows the OpenAPI 3.0 Security Requirement Object.
    /// </summary>
    [JsonPropertyName("security")]
    public IReadOnlyDictionary<string, ImmutableArray<string>>? Security { get; set; }

    /// <summary>
    /// A declaration of the security schemes available to authorize requests. The key is the scheme name. Follows the OpenAPI 3.0 Security Scheme Object.
    /// </summary>
    [JsonPropertyName("securitySchemes")]
    public IReadOnlyDictionary<string, SecurityScheme>? SecuritySchemes { get; set;}

    /// <summary>
    /// JSON Web Signatures computed for this AgentCard.
    /// </summary>
    [JsonPropertyName("signatures")]
    public ImmutableArray<AgentCardSignature>? Signatures { get; set; }

    /// <summary>
    /// Default set of supported input MIME types for all skills, which can be overridden on a per-skill basis.
    /// </summary>
    [JsonPropertyName("defaultInputModes")]
    public required ImmutableArray<string> DefaultInputModes { get; set; }

    /// <summary>
    /// Default set of supported output MIME types for all skills, which can be overridden on a per-skill basis.
    /// </summary>
    [JsonPropertyName("defaultOutputModes")]
    public required ImmutableArray<string> DefaultOutputModes { get; set; }

    /// <summary>
    /// The set of skills, or distinct capabilities, that the agent can perform.
    /// </summary>
    [JsonPropertyName("skills")]
    public required ImmutableArray<AgentSkill> Skills { get; set; }


    /// <summary>
    /// A list of additional supported interfaces (transport and URL combinations). This allows agents to expose multiple transports, 
    /// potentially at different URLs.
    /// 
    /// <para>Best practices:- SHOULD include all supported transports for completeness\n- SHOULD include an entry matching the main 'url' and 'preferredTransport'\n- MAY reuse URLs if multiple transports are available at the same endpoint\n- MUST accurately declare the transport available at each URL\n\nClients can select any interface from this list based on their transport capabilities\nand preferences. This enables transport negotiation and fallback scenarios.</para>
    /// </summary>
    [JsonPropertyName("additionalInterfaces")]
    public required ImmutableArray<AgentInterface> AdditionalInterfaces { get; set; }

    /// <summary>
    /// The transport protocol for the preferred endpoint (the main 'url' field). If not specified, defaults to 'JSONRPC'.
    /// 
    /// <para>IMPORTANT: The transport specified here MUST be available at the main 'url'. 
    /// This creates a binding between the main URL and its supported transport protocol. Clients should prefer this transport and URL combination when both are supported.</para>
    /// </summary>
    [JsonPropertyName("preferredTransport")]
    public TransportProtocol PreferredTransport { get; set; } = TransportProtocol.JsonRpc;

    /// <summary>
    /// If true, the agent can provide an extended agent card with additional details to authenticated users. Defaults to false.
    /// </summary>
    [JsonPropertyName("supportsAuthenticatedExtendedCard")]
    public string? SupportsAuthenticatedExtendedCard { get; set; }
}