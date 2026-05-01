// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    /// <summary>
    /// A <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeConnectorRequest"/> that traces a send-to-conversation connector request,
    /// recording the conversation ID and optional activity ID as span tags.
    /// </summary>
    internal class ScopeSendToConversation : ScopeConnectorRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeSendToConversation"/> class.
        /// </summary>
        /// <param name="conversationId">The ID of the conversation to send the activity to.</param>
        /// <param name="activityId">The activity ID to associate with the span, or <see langword="null"/>.</param>
        public ScopeSendToConversation(string conversationId, string? activityId)
            : base(Constants.ScopeSendToConversation, conversationId, activityId)
        { }
    }
}
