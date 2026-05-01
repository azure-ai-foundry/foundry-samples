// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.CopilotStudio.Client.Models
{
    /// <summary>
    /// Describes the orchestration metadata for an externally orchestrated turn.
    /// </summary>
#if !NETSTANDARD
    public record OrchestrationRequest
#else
    public class OrchestrationRequest
#endif
    {
        /// <summary>
        /// The type of orchestration operation to perform.
        /// </summary>
        [JsonPropertyName("operation")]
#if !NETSTANDARD
        public OrchestrationOperation Operation { get; init; }
#else
        public OrchestrationOperation Operation { get; set; }
#endif

        /// <summary>
        /// Tool invocation details. Required when <see cref="Microsoft.Agents.CopilotStudio.Client.Models.OrchestrationRequest.Operation"/> is <see cref="Microsoft.Agents.CopilotStudio.Client.Models.OrchestrationOperation.InvokeTool"/>.
        /// </summary>
        [JsonPropertyName("toolInputs")]
#if !NETSTANDARD
        public ToolInvocationInput? ToolInputs { get; init; }
#else
        public ToolInvocationInput? ToolInputs { get; set; }
#endif
    }
}
