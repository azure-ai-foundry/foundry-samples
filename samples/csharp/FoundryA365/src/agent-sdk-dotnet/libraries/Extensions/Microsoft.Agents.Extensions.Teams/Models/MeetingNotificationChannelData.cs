// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Specify Teams Bot meeting notification channel data.
    /// </summary>
    public class MeetingNotificationChannelData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.MeetingNotificationChannelData"/> class.
        /// </summary>
        public MeetingNotificationChannelData()
        {
        }

        /// <summary>
        /// Gets or sets the OnBehalfOf list for user attribution.
        /// </summary>
        /// <value>The Teams Bot meeting notification's OnBehalfOf list.</value>
        public IList<OnBehalfOf> OnBehalfOfList { get; set; }
    }
}
