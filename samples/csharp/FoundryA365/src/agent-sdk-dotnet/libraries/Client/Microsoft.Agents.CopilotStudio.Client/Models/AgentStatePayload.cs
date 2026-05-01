// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.CopilotStudio.Client.Models
{
    /// <summary>
    /// Payload containing agent state information returned from an externally orchestrated turn.
    /// </summary>
#if !NETSTANDARD
    public record AgentStatePayload
#else
    public class AgentStatePayload
#endif
    {
        /// <summary>
        /// The status of the agent after processing the turn.
        /// </summary>
        [JsonPropertyName("status")]
#if !NETSTANDARD
        public AgentStatus Status { get; init; }
#else
        public AgentStatus Status { get; set; }
#endif

        /// <summary>
        /// The resolved agent instructions, if any.
        /// </summary>
        [JsonPropertyName("resolvedAgentInstructions")]
#if !NETSTANDARD
        public string? ResolvedAgentInstructions { get; init; }
#else
        public string? ResolvedAgentInstructions { get; set; }
#endif

        /// <summary>
        /// The schema names of tools (topics) enabled for this agent.
        /// </summary>
        [JsonPropertyName("enabledToolSchemaNames")]
#if !NETSTANDARD
        public string[] EnabledToolSchemaNames { get; init; } = [];
#else
        public string[] EnabledToolSchemaNames { get; set; } = [];
#endif
    }
}
