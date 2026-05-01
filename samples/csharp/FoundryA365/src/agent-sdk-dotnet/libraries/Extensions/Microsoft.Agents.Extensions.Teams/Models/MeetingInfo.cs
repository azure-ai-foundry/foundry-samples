// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// General information about a Teams meeting.
    /// </summary>
    public class MeetingInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.MeetingInfo"/> class.
        /// </summary>
        public MeetingInfo()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.MeetingInfo"/> class.
        /// </summary>
        /// <param name="details">The meeting's detailed information.</param>
        /// <param name="conversation">Conversation Account for the meeting.</param>
        /// <param name="organizer">Information specific to this organizer of the specific meeting.</param>
        /// <param name="id">Unique identifier representing the meeting.</param>
        public MeetingInfo(MeetingDetails details, ConversationAccount conversation = null, TeamsChannelAccount organizer = null, string id = null)
        {
            Id = id;
            Details = details;
            Conversation = conversation;
            Organizer = organizer;
        }

        /// <summary>
        /// Unique identifier representing a meeting
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Gets or sets the specific details of a Teams meeting.
        /// </summary>
        /// <value>
        /// The specific details of a Teams meeting.
        /// </value>
        public MeetingDetails Details { get; set; }

        /// <summary>
        /// Gets or sets the Conversation Account for the meeting.
        /// </summary>
        /// <value>
        /// The Conversation Account for the meeting.
        /// </value>
        public ConversationAccount Conversation { get; set; }

        /// <summary>
        /// Gets or sets the meeting organizer's user information.
        /// </summary>
        /// <value>
        /// The organizer's user information.
        /// </value>
        public TeamsChannelAccount Organizer { get; set; }
    }
}
