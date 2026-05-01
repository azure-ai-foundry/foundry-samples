// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// List of channels under a team.
    /// </summary>
    public class ConversationList
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.ConversationList"/> class.
        /// </summary>
        public ConversationList()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.ConversationList"/> class.
        /// </summary>
        /// <param name="conversations">The IList of conversations.</param>
        public ConversationList(IList<ChannelInfo> conversations = default)
        {
            Conversations = conversations;
        }

        /// <summary>
        /// Gets or sets the conversations.
        /// </summary>
        /// <value>The conversations.</value>
        public IList<ChannelInfo> Conversations { get; set; }
    }
}
