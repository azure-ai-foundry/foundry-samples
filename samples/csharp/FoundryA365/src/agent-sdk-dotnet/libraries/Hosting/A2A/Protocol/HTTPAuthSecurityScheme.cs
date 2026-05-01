// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

/// <summary>
/// HTTP Authentication security scheme.
/// </summary>
public sealed class HTTPAuthSecurityScheme : SecurityScheme
{
    /// <summary>
    /// A hint to the client to identify how the bearer token is formatted (e.g., \"JWT\").  This is primarily for documentation purposes.
    /// </summary>
    [JsonPropertyName("bearerFormat")]
    public string? BearerFormat { get; set; }

    /// <summary>
    /// The name of the HTTP Authentication scheme to be used in the Authorization header,\nas defined in RFC7235 (e.g., \"Bearer\"). This value should be registered in the IANA Authentication Scheme registry.
    /// </summary>
    [JsonPropertyName("scheme")]
    public required string Scheme { get; set; }
}
