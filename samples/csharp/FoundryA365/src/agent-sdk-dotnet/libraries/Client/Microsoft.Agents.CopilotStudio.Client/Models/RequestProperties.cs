// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.CopilotStudio.Client.Models
{
    /// <summary>
    /// Optional properties that are mapped to HTTP headers on each request.
    /// </summary>
    public class RequestProperties
    {
        /// <summary>
        /// Correlation ID for end-to-end request tracing.
        /// Sent as the <c>x-ms-correlation-id</c> header.
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Agent version string.
        /// Sent as the <c>x-cci-agent-version</c> header.
        /// </summary>
        public string? AgentVersion { get; set; }

        /// <summary>
        /// Preferred natural language for the response (e.g. <c>en-US</c>).
        /// Sent as the <c>Accept-Language</c> header.
        /// </summary>
        public string? AcceptLanguage { get; set; }
    }
}
