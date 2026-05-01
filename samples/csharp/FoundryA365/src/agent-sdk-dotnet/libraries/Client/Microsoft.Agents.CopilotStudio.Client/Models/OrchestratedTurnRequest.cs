// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.CopilotStudio.Client.Models
{
    /// <summary>
    /// Request body for an externally orchestrated conversation turn.
    /// Extends <see cref="Microsoft.Agents.CopilotStudio.Client.ExecuteTurnRequest"/> with orchestration metadata.
    /// </summary>
    public class OrchestratedTurnRequest : ExecuteTurnRequest
    {
        /// <summary>
        /// Orchestration metadata describing the operation to perform.
        /// </summary>
        [JsonPropertyName("orchestration")]
#if !NETSTANDARD
        public OrchestrationRequest? Orchestration { get; init; }
#else
        public OrchestrationRequest? Orchestration { get; set; }
#endif

        /// <summary>
        /// An optional value payload for the orchestrated turn (e.g. initial context for StartConversation).
        /// </summary>
        [JsonPropertyName("value")]
#if !NETSTANDARD
        public object? Value { get; init; }
#else
        public object? Value { get; set; }
#endif
    }
}
