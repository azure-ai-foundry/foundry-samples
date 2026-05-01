// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

/// <summary>
/// For conveying structured JSON data. Useful for forms, parameters, or any machine-readable information.
/// </summary>
public sealed class DataPart : Part
{
    /// <summary>
    /// The structured JSON data payload (an object or an array).
    /// </summary>
    [JsonPropertyName("data")]
    public required object Data { get; set; }
}