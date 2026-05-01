// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Describes a member.
    /// </summary>
    public class TeamMember
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TeamMember"/> class.
        /// </summary>
        public TeamMember()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TeamMember"/> class.
        /// </summary>
        /// <param name="id">Unique identifier representing a member (user or channel).</param>
        public TeamMember(string id = default)
        {
            Id = id;
        }

        /// <summary>
        /// Gets or sets unique identifier representing a member (user or channel).
        /// </summary>
        /// <value>The member ID.</value>
        public string Id { get; set; }
    }
}
