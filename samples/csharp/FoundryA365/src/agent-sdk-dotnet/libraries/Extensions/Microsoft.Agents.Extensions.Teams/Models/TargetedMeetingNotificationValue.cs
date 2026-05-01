// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Specifies the targeted meeting notification value, including recipients and surfaces.
    /// </summary>
    public class TargetedMeetingNotificationValue
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TargetedMeetingNotificationValue"/> class.
        /// </summary>
        public TargetedMeetingNotificationValue()
        {
        }

        /// <summary>
        /// Gets or sets the collection of recipients of the targeted meeting notification.
        /// </summary>
        /// <value>
        /// The collection of recipients of the targeted meeting notification.
        /// </value>
        public IList<string> Recipients { get; set; }

        /// <summary>
        /// Gets or sets the collection of surfaces on which to show the notification.
        /// If a bot wants its content to be rendered in different surfaces areas, it can specific a list of UX areas. 
        /// But please note that only one instance of surface type is allowed per request. 
        /// </summary>
        /// <value>
        /// The collection of surfaces on which to show the notification.
        /// </value>
        public IList<Surface> Surfaces { get; set; }
    }
}
