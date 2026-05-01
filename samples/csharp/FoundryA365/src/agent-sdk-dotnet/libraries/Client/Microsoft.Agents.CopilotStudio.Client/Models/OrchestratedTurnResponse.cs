// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.CopilotStudio.Client.Models
{
    /// <summary>
    /// Response from an externally orchestrated conversation turn (non-streaming JSON fallback).
    /// </summary>
#if !NETSTANDARD
    public record OrchestratedTurnResponse : ResponseBase
#else
    public class OrchestratedTurnResponse : ResponseBase
#endif
    {
        /// <summary>
        /// The agent state information after the turn.
        /// </summary>
        [JsonPropertyName("agentState")]
#if !NETSTANDARD
        public AgentStatePayload? AgentState { get; init; }
#else
        public AgentStatePayload? AgentState { get; set; }
#endif
    }
}
