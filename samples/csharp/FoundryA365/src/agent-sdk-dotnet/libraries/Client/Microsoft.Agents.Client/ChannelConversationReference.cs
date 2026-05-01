// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// A conversation reference type for an Agent.
    /// </summary>
    public class ChannelConversationReference
    {
        /// <summary>
        /// Gets or sets the conversation reference.
        /// </summary>
        public ConversationReference ConversationReference { get; set; }

        /// <summary>
        /// Gets or sets the OAuth scope.
        /// </summary>
        public string OAuthScope { get; set; }

        /// <summary>
        /// The name of the Agent.
        /// </summary>
        public string AgentName { get; set; }

        public string AgentConversationId { get; set; }
    }
}
