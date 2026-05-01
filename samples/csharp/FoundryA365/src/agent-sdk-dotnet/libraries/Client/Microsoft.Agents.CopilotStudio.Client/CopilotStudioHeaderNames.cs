// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.CopilotStudio.Client
{
    /// <summary>
    /// Header name constants used in HTTP requests and responses for Copilot Studio communication.
    /// </summary>
    internal static class CopilotStudioHeaderNames
    {
        /// <summary>
        /// Header for the conversation ID in outgoing orchestrated requests.
        /// </summary>
        internal const string ConversationId = "x-ms-conversation-id";

        /// <summary>
        /// Header for client request correlation ID.
        /// Used for request tracing and diagnostics.
        /// </summary>
        internal const string ClientRequestId = "x-ms-client-request-id";

        /// <summary>
        /// Header for the conversation ID returned in Direct-to-Engine responses.
        /// </summary>
        internal const string D2EConversationId = "x-ms-conversationid";

        /// <summary>
        /// Header for the island experimental URL returned in Direct-to-Engine responses.
        /// </summary>
        internal const string D2EExperimentalUrl = "x-ms-d2e-experimental";

        /// <summary>
        /// Header for the correlation ID used for end-to-end request tracing.
        /// </summary>
        internal const string CorrelationId = "x-ms-correlation-id";

        /// <summary>
        /// Header for the agent version.
        /// </summary>
        internal const string AgentVersion = "x-cci-agent-version";

        /// <summary>
        /// Header for the preferred natural language of the response.
        /// </summary>
        internal const string AcceptLanguage = "Accept-Language";
    }
}
