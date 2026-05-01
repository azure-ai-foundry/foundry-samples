// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Data about the meeting participants.
    /// </summary>
    public class MeetingParticipantsEventDetails
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.MeetingParticipantsEventDetails"/> class.
        /// </summary>
        public MeetingParticipantsEventDetails()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.MeetingParticipantsEventDetails"/> class.
        /// </summary>
        /// <param name="members">The members involved in the meeting event.</param>
        public MeetingParticipantsEventDetails(
            IList<TeamsMeetingMember> members = default)
        { 
            Members = members;
        }
     
        /// <summary>
        /// Gets the meeting participants info.
        /// </summary>
        /// <value>
        /// The participant accounts info.
        /// </value>
        public IList<TeamsMeetingMember> Members { get; set; } = new List<TeamsMeetingMember>();
    }
}
