// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

public sealed class TaskIdParams
{
    /// <summary>
    /// The ID of the task whose current state is to be retrieved.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// Request-specific metadata.
    /// </summary>
    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, object>? Metadata { get; set; }
}
