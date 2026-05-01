// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.CopilotStudio.Client.Models
{
    /// <summary>
    /// Represents the status of an externally orchestrated agent.
    /// Uses a string value for forward compatibility with new status types.
    /// </summary>
    [JsonConverter(typeof(AgentStatusJsonConverter))]
    public readonly struct AgentStatus(string? value)
    {
        public static readonly AgentStatus Completed = new("Completed");
        public static readonly AgentStatus WaitingForUserInput = new("WaitingForUserInput");
        public static readonly AgentStatus InProgress = new("InProgress");
        public static readonly AgentStatus Unknown = new(null);

        /// <summary>
        /// Gets the string value of the status.
        /// </summary>
        public string Value { get; } = value ?? string.Empty;

        /// <inheritdoc/>
        public override string ToString() => Value;
    }

    internal class AgentStatusJsonConverter : JsonConverter<AgentStatus>
    {
        public override AgentStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => new(reader.GetString());

        public override void Write(Utf8JsonWriter writer, AgentStatus value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.Value);
    }
}
