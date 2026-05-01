// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

public sealed class MessageSendParams
{
    [JsonPropertyName("message")]
    public required Message Message { get; set; }

    [JsonPropertyName("configuration")]
    public MessageSendConfiguration? Configuration { get; set; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, object>? Metadata { get; set; }
}