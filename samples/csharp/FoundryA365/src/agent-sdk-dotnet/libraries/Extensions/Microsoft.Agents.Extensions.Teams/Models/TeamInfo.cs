// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Describes a team.
    /// </summary>
    public class TeamInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TeamInfo"/> class.
        /// </summary>
        public TeamInfo()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TeamInfo"/> class.
        /// </summary>
        /// <param name="id">Unique identifier representing a team.</param>
        /// <param name="name">Name of team.</param>
        public TeamInfo(string id = default, string name = default)
        {
            Id = id;
            Name = name;
        }

        /// <summary>
        /// Gets or sets unique identifier representing a team.
        /// </summary>
        /// <value>The team ID.</value>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets name of team.
        /// </summary>
        /// <value>The team name.</value>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the Azure AD Teams group ID.
        /// </summary>
        /// <value>The Azure Active Directory Teams group ID.</value>
        public string AadGroupId { get; set; }

        /// <summary>
        /// Gets or sets the TenantId.
        /// </summary>
        /// <value>The TenantId.</value>
        public string? TenantId { get; set; }
    }
}
