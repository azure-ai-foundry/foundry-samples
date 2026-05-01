// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Specifies Bot meeting notification including meeting notification value.
    /// </summary>
    /// <typeparam name="T">The first generic type parameter.</typeparam>.
    public class MeetingNotification<T> : MeetingNotificationBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.MeetingNotification{T}"/> class.
        /// </summary>
        protected MeetingNotification()
        {
        }

        /// <summary>
        /// Gets or sets Teams Bot meeting notification value.
        /// </summary>
        /// <value>
        /// Teams Bot meeting notification value.
        /// </value>
        public T Value { get; set; }
    }
}
