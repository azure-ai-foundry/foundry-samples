// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Details related to a team.
    /// </summary>
    public class TeamDetails
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TeamDetails"/> class.
        /// </summary>
        public TeamDetails()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TeamDetails"/> class.
        /// </summary>
        /// <param name="id">Unique identifier representing a team.</param>
        /// <param name="name">Name of team.</param>
        /// <param name="aadGroupId">Azure Active Directory (AAD) Group Id.</param>
        public TeamDetails(string id = default, string name = default, string aadGroupId = default)
        {
            Id = id;
            Name = name;
            AadGroupId = aadGroupId;
        }

        /// <summary>
        /// Gets or sets unique identifier representing a team.
        /// </summary>
        /// /// <value>
        /// The team Id.
        /// </value>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets name of team.
        /// </summary>
        /// <value>
        /// Name of team.
        /// </value>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets azure Active Directory (AAD) Group Id for the team.
        /// </summary>
        /// <value>
        /// The AAD Group Id.
        /// </value>
        public string AadGroupId { get; set; }

        /// <summary>
        /// Gets or sets the number of channels in the team.
        /// </summary>
        /// <value>
        /// The number of channels in the team.
        /// </value>
        public int ChannelCount { get; set; }

        /// <summary>
        /// Gets or sets the number of members in the team.
        /// </summary>
        /// <value>
        /// The number of members in the team.
        /// </value>
        public int MemberCount { get; set; }

        /// <summary>
        /// Gets or sets type of the team. Valid values are standard, sharedChannel and privateChannel.
        /// </summary>
        /// <value>The team type.</value>
        public string Type { get; set; }
    }
}
