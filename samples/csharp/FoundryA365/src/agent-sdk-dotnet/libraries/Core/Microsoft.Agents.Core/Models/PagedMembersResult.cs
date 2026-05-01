// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> Page of members. </summary>
    public class PagedMembersResult
    {
        /// <summary> Initializes a new instance of PagedMembersResult. </summary>
        public PagedMembersResult()
        {
            Members = [];
        }

        /// <summary> Initializes a new instance of PagedMembersResult. </summary>
        /// <param name="continuationToken"> Paging token. </param>
        /// <param name="members"> The Channel Accounts. </param>
        public PagedMembersResult(string continuationToken = default, IList<ChannelAccount> members = default)
        {
            ContinuationToken = continuationToken;
            Members = members ?? [];
        }

        /// <summary> Paging token. </summary>
        public string ContinuationToken { get; set; }
        /// <summary> The Channel Accounts. </summary>
        public IList<ChannelAccount> Members { get; set; }
    }
}
