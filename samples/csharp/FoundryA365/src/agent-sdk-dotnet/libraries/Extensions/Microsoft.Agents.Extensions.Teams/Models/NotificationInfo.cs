// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Specifies if a notification is to be sent for the mentions.
    /// </summary>
    public class NotificationInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.NotificationInfo"/> class.
        /// </summary>
        public NotificationInfo()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.NotificationInfo"/> class.
        /// </summary>
        /// <param name="alert">true if notification is to be sent to the user,
        /// false otherwise.</param>
        public NotificationInfo(bool? alert = default)
        {
            Alert = alert;
        }

        /// <summary>
        /// Gets or sets true if notification is to be sent to the user, false
        /// otherwise.
        /// </summary>
        /// <value>Boolean indicating if notification is sent to the user.</value>
        public bool? Alert { get; set; }

        /// <summary>
        /// Gets or sets the value indicating if a notification is to be shown to the user while in a meeting,
        /// false otherwise.
        /// </summary>
        /// <value>Boolean indicating if a notification is to be shown to the user while in a meeting.</value>
        public bool? AlertInMeeting { get; set; }

        /// <summary>
        /// Gets or sets the value of the notification's external resource URL.
        /// </summary>
        /// <value>The value of the notification's external resource URL.</value>
        public string ExternalResourceUrl { get; set; }
    }
}
