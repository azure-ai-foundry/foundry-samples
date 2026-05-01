// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using Microsoft.Agents.Core.Models;
using System.Collections.Generic;

namespace Microsoft.Agents.Connector.Types
{
    /// <summary> Conversation and its members. </summary>
    public class ConversationMembers
    {
        /// <summary> Initializes a new instance of ConversationMembers. </summary>
        public ConversationMembers()
        {
            Members = new List<ChannelAccount>();
        }

        /// <summary> Initializes a new instance of ConversationMembers. </summary>
        /// <param name="id"> Conversation ID. </param>
        /// <param name="members"> List of members in this conversation. </param>
        public ConversationMembers(string id = default, IList<ChannelAccount> members = default)
        {
            Id = id;
            Members = members ?? new List<ChannelAccount>();
        }

        /// <summary> Conversation ID. </summary>
        public string Id { get; set; }

        /// <summary> List of members in this conversation. </summary>
        public IList<ChannelAccount> Members { get; set; }
    }
}
