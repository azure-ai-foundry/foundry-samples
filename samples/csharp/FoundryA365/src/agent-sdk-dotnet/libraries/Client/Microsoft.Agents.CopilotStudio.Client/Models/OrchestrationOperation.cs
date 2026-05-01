// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.CopilotStudio.Client.Models
{
    /// <summary>
    /// Represents the type of orchestration operation.
    /// Uses a string value for forward compatibility with new operation types.
    /// </summary>
    [JsonConverter(typeof(OrchestrationOperationJsonConverter))]
    public readonly struct OrchestrationOperation(string? value)
    {
        public static readonly OrchestrationOperation StartConversation = new("StartConversation");
        public static readonly OrchestrationOperation InvokeTool = new("InvokeTool");
        public static readonly OrchestrationOperation HandleUserResponse = new("HandleUserResponse");
        public static readonly OrchestrationOperation ConversationUpdate = new("ConversationUpdate");

        /// <summary>
        /// Gets the string value of the operation.
        /// </summary>
        public string Value { get; } = value ?? string.Empty;

        /// <inheritdoc/>
        public override string ToString() => Value;
    }

    internal class OrchestrationOperationJsonConverter : JsonConverter<OrchestrationOperation>
    {
        public override OrchestrationOperation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => new(reader.GetString());

        public override void Write(Utf8JsonWriter writer, OrchestrationOperation value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.Value);
    }
}
