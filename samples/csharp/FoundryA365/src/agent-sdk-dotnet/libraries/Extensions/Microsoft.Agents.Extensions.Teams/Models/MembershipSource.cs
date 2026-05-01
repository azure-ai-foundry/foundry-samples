// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Represents a source of membership for a user in a given channel or team.
    /// </summary>
    public class MembershipSource
    {
        /// <summary>
        /// The type of roster the user is a member of.
        /// </summary>
        public MembershipSourceTypes SourceType { get; set; }

        /// <summary>
        /// The unique identifier of the membership source.
        /// </summary>
        public string Id { get; set; } = null!;

        /// <summary>
        /// The user's relationship to the current channel.
        /// </summary>
        public MembershipTypes MembershipType { get; set; }

        /// <summary>
        /// The group ID of the team associated with this membership source.
        /// </summary>
        public string TeamGroupId { get; set; } = null!;

        /// <summary>
        /// The tenant ID for the user (if applicable).
        /// </summary>
        public string TenantId { get; set; }
    }
}

