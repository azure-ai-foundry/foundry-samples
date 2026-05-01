// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    public enum MembershipTypes
    {
        /// <summary>
        /// Indicates that the user is a member of the channel.
        /// </summary>
        Direct,

        /// <summary>
        /// Indicates that the user is a member of the team.
        /// </summary>
        Transitive
    }
}