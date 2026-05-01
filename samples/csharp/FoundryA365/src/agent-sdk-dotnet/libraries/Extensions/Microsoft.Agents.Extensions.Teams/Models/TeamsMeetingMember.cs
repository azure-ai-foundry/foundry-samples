// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Data about the meeting participants.
    /// </summary>
    public class TeamsMeetingMember
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TeamsMeetingMember"/> class.
        /// </summary>
        /// <param name="user">The channel user data.</param>
        /// <param name="meeting">The user meeting details.</param>
        public TeamsMeetingMember(TeamsChannelAccount user, UserMeetingDetails meeting) 
        {
            User = user;
            Meeting = meeting;
        }

        /// <summary>
        /// Gets or sets the meeting participant.
        /// </summary>
        /// <value>
        /// The joined participant account.
        /// </value>
        public TeamsChannelAccount User { get; set; }

        /// <summary>
        /// Gets or sets the user meeting details.
        /// </summary>
        /// <value>
        /// The users meeting details.
        /// </value>
        public UserMeetingDetails Meeting { get; set; }
    }
}
