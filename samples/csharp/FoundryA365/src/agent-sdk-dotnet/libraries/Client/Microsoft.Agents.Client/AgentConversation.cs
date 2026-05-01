// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// Contains the Agent information for active Conversations.
    /// </summary>
    public class AgentConversation
    {
        public string AgentName { get; set; }

        /// <summary>
        /// This is the conversationId created with <see cref="Microsoft.Agents.Client.IAgentHost.GetOrCreateConversationAsync(Microsoft.Agents.Builder.ITurnContext, string, System.Threading.CancellationToken)"/>.
        /// </summary>
        public string AgentConversationId { get; set; }
    }
}
