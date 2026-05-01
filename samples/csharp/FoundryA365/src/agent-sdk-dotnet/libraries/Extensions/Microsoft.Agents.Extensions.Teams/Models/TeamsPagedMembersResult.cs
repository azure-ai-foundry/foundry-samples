// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Represents a wrapper for a Teams members query result.
    /// </summary>
    public class TeamsPagedMembersResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TeamsPagedMembersResult"/> class.
        /// </summary>
        public TeamsPagedMembersResult()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TeamsPagedMembersResult"/> class
        /// using the given continuation token and members list.
        /// </summary>
        /// <param name="continuationToken">A paging token.</param>
        /// <param name="members">A list of channel accounts.</param>
        public TeamsPagedMembersResult(string continuationToken = default, IList<ChannelAccount> members = default)
        {
            ContinuationToken = continuationToken;
            var teamsChannelAccounts = members.Select(channelAccount => ProtocolJsonSerializer.ToObject<TeamsChannelAccount>(ProtocolJsonSerializer.ToJson(channelAccount)));
            Members = teamsChannelAccounts.ToList();
        }

        /// <summary>
        /// Gets or sets the paging token.
        /// </summary>
        /// <value>
        /// The paging token.
        /// </value>
        public string ContinuationToken { get; set; }

        /// <summary>
        /// Gets or sets the list of channel accounts.
        /// </summary>
        /// <value>
        /// The channel accounts.
        /// </value>
        public IList<TeamsChannelAccount> Members { get; set; }
    }
}
