// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

/// <summary>
/// Base properties shared by all security schemes.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(APIKeySecurityScheme), typeDiscriminator: "apiKey")]
[JsonDerivedType(typeof(HTTPAuthSecurityScheme), typeDiscriminator: "http")]
[JsonDerivedType(typeof(OAuth2SecurityScheme), typeDiscriminator: "oauth2")]
[JsonDerivedType(typeof(OpenIdConnectSecurityScheme), typeDiscriminator: "openIdConnect")]
[JsonDerivedType(typeof(MutualTLSSecurityScheme), typeDiscriminator: "mutualTLS")]
public abstract class SecurityScheme
{
    /// <summary>
    /// Description of this security scheme.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
