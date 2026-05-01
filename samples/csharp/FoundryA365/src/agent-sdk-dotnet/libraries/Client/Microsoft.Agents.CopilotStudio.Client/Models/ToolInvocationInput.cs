// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.CopilotStudio.Client.Models
{
    /// <summary>
    /// Input parameters for a tool invocation in an orchestrated conversation.
    /// </summary>
#if !NETSTANDARD
    public record ToolInvocationInput
#else
    public class ToolInvocationInput
#endif
    {
        /// <summary>
        /// The schema name of the tool (topic) to invoke.
        /// </summary>
        [JsonPropertyName("toolSchemaName")]
#if !NETSTANDARD
        public string? ToolSchemaName { get; init; }
#else
        public string? ToolSchemaName { get; set; }
#endif

        /// <summary>
        /// The parameters to pass to the tool. Kept as <see cref="object"/> for maximum
        /// compatibility with arbitrary JSON payloads.
        /// </summary>
        [JsonPropertyName("parameters")]
#if !NETSTANDARD
        public object? Parameters { get; init; }
#else
        public object? Parameters { get; set; }
#endif
    }
}
