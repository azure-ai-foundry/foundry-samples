// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Specifies Bot meeting notification base including channel data and type.
    /// </summary>
    public class MeetingNotificationBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.MeetingNotificationBase"/> class.
        /// </summary>
        protected MeetingNotificationBase()
        {
        }

        /// <summary>
        /// Gets or sets type of Bot meeting notification.
        /// </summary>
        /// <value>
        /// Bot meeting notification type.
        /// </value>
        public string Type { get; set; }
    }
}
