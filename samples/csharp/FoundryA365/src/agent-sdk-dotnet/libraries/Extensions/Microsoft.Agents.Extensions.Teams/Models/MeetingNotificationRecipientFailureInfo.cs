// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Information regarding failure to notify a recipient of a meeting notification.
    /// </summary>
    public class MeetingNotificationRecipientFailureInfo
    {
        /// <summary>
        /// Gets or sets the mri for a recipient meeting notification failure.
        /// </summary>
        /// <value>The type of this notification container.</value>
        public string RecipientMri { get; set; }

        /// <summary>
        /// Gets or sets the error code for a meeting notification.
        /// </summary>
        /// <value>The error code for a meeting notification.</value>
        public string ErrorCode { get; set; }

        /// <summary>
        /// Gets or sets the failure reason for a meeting notification failure.
        /// </summary>
        /// <value>The reason why a participant meeting notification failed.</value>
        public string FailureReason { get; set; }
    }
}
