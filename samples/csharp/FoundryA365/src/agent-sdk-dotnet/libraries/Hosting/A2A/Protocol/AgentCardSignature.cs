// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

/// <summary>
/// AgentCardSignature represents a JWS signature of an AgentCard. This follows the JSON format of an RFC 7515 JSON Web Signature (JWS).
/// </summary>
public sealed class AgentCardSignature
{
    /// <summary>
    /// The protected JWS header for the signature. This is a Base64url-encoded JSON object, as per RFC 7515.
    /// </summary>
    [JsonPropertyName("protected")]
    public required string Protected { get; set; }

    /// <summary>
    /// The computed signature, Base64url-encoded.
    /// </summary>
    [JsonPropertyName("signature")]
    public required string Signature { get; set; }

    /// <summary>
    /// The unprotected JWS header values.
    /// </summary>
    [JsonPropertyName("header")]
    public IReadOnlyDictionary<string, object>? Header { get; set; }
}
