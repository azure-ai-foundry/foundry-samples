// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Specific details of a user in a Teams meeting.
    /// </summary>
    public class UserMeetingDetails
    {
        /// <summary>
        /// Gets or sets a value indicating whether the user is in the meeting.
        /// </summary>
        /// <value>
        /// The user in meeting indicator.
        /// </value>
        public bool InMeeting { get;  set; }

        /// <summary>
        /// Gets or sets the value of the user's role.
        /// </summary>
        /// <value>
        /// The user's role.
        /// </value>
        public string Role { get; set; }
    }
}
