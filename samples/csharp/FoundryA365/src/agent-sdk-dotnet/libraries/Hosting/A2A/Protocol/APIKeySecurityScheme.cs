// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

/// <summary>
/// Defines a security scheme using an API key.
/// </summary>
public sealed class APIKeySecurityScheme : SecurityScheme
{
    /// <summary>
    /// The location of the API key: cookie, header, query
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// The name of the header, query, or cookie parameter to be used.
    /// </summary>
    [JsonPropertyName("in")]
    public required string In { get; set; }
}
