// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Specific details of a Teams meeting.
    /// </summary>
    public class MeetingDetails : MeetingDetailsBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.MeetingDetails"/> class.
        /// </summary>
        public MeetingDetails()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.MeetingDetails"/> class.
        /// </summary>
        /// <param name="id">The meeting's Id, encoded as a BASE64 string.</param>
        /// <param name="msGraphResourceId">The MsGraphResourceId, used specifically for MS Graph API calls.</param>
        /// <param name="scheduledStartTime">The meeting's scheduled start time, in UTC.</param>
        /// <param name="scheduledEndTime">The meeting's scheduled end time, in UTC.</param>
        /// <param name="joinUrl">The URL used to join the meeting.</param>
        /// <param name="title">The title of the meeting.</param>
        /// <param name="type">The meeting's type.</param>
        public MeetingDetails(
            string id,
            string msGraphResourceId = null,
            DateTime? scheduledStartTime = null,
            DateTime? scheduledEndTime = null,
            Uri joinUrl = null,
            string title = null,
            string type = "Scheduled")
            : base(id, joinUrl, title)
        {
            MsGraphResourceId = msGraphResourceId;
            ScheduledStartTime = scheduledStartTime;
            ScheduledEndTime = scheduledEndTime;
            Type = type;
        }

        /// <summary>
        /// Gets or sets the MsGraphResourceId, used specifically for MS Graph API calls.
        /// </summary>
        /// <value>
        /// The MsGraphResourceId, used specifically for MS Graph API calls.
        /// </value>
        public string MsGraphResourceId { get; set; }

        /// <summary>
        /// Gets or sets the meeting's scheduled start time, in UTC.
        /// </summary>
        /// <value>
        /// The meeting's scheduled start time, in UTC.
        /// </value>
        public DateTime? ScheduledStartTime { get; set; }

        /// <summary>
        /// Gets or sets the meeting's scheduled end time, in UTC.
        /// </summary>
        /// <value>
        /// The meeting's scheduled end time, in UTC.
        /// </value>
        public DateTime? ScheduledEndTime { get; set; }

        /// <summary>
        /// Gets or sets the meeting's type.
        /// </summary>
        /// <value>
        /// The meeting's type.
        /// </value>
        public string Type { get; set; }
    }
}
