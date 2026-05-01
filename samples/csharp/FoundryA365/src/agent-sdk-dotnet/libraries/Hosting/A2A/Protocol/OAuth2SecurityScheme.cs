// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

/// <summary>
/// Defines a security scheme using OAuth 2.0.
/// </summary>
public sealed class OAuth2SecurityScheme : SecurityScheme
{
    /// <summary>
    /// An object containing configuration information for the supported OAuth 2.0 flows.
    /// </summary>
    public required OAuthFlows Flows { get; set; }

    /// <summary>
    /// URL to the oauth2 authorization server metadata\n[RFC8414](https://datatracker.ietf.org/doc/html/rfc8414). TLS is required.
    /// </summary>
    [JsonPropertyName("oauth2MetadataUrl")]
    public string? Oauth2MetadataUrl { get; set; }
}
