// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    /// <summary>
    /// A <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeConnectorRequest"/> that traces a get-conversation-members connector request,
    /// recording the conversation ID as a span tag.
    /// </summary>
    internal class ScopeGetConversationMembers : ScopeConnectorRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeGetConversationMembers"/> class.
        /// </summary>
        /// <param name="conversationId">The ID of the conversation whose members are being retrieved.</param>
        public ScopeGetConversationMembers(string conversationId) : base(Constants.ScopeGetConversationMembers, conversationId)
        { }
    }
}
