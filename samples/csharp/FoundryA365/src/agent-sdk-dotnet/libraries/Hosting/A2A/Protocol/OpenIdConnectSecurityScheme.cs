// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

/// <summary>
/// Defines a security scheme using OpenID Connect.
/// </summary>
public sealed class OpenIdConnectSecurityScheme : SecurityScheme
{
    /// <summary>
    /// The OpenID Connect Discovery URL for the OIDC provider's metadata.
    /// </summary>
    [JsonPropertyName("openIdConnectUrl")]
    public required string OpenIdConnectUrl { get; set; }
}
