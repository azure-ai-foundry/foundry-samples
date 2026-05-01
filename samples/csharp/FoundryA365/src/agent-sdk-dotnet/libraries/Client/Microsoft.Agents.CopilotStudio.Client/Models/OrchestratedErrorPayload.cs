// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.CopilotStudio.Client.Models
{
    /// <summary>
    /// Error details returned from an externally orchestrated conversation turn.
    /// </summary>
#if !NETSTANDARD
    public record OrchestratedErrorPayload
#else
    public class OrchestratedErrorPayload
#endif
    {
        /// <summary>
        /// The error code identifying the type of failure.
        /// </summary>
        [JsonPropertyName("code")]
#if !NETSTANDARD
        public string? Code { get; init; }
#else
        public string? Code { get; set; }
#endif

        /// <summary>
        /// A human-readable description of the error.
        /// </summary>
        [JsonPropertyName("message")]
#if !NETSTANDARD
        public string? Message { get; init; }
#else
        public string? Message { get; set; }
#endif

        /// <summary>
        /// The parsed error code as a strongly-typed enum.
        /// Returns <see cref="Microsoft.Agents.CopilotStudio.Client.Models.OrchestratedErrorCode.Unknown"/> if the <see cref="Microsoft.Agents.CopilotStudio.Client.Models.OrchestratedErrorPayload.Code"/> is null, empty, or not recognized.
        /// </summary>
        [JsonIgnore]
        public OrchestratedErrorCode ErrorCode => Code.ToOrchestratedErrorCode();
    }

    /// <summary>
    /// Envelope for error responses in the SSE stream.
    /// </summary>
#if !NETSTANDARD
    internal record OrchestratedErrorEnvelope
#else
    internal class OrchestratedErrorEnvelope
#endif
    {
        [JsonPropertyName("error")]
#if !NETSTANDARD
        public OrchestratedErrorPayload? Error { get; init; }
#else
        public OrchestratedErrorPayload? Error { get; set; }
#endif
    }
}
