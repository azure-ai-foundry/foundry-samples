// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Messaging extension query options.
    /// </summary>
    public class MessagingExtensionQueryOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.MessagingExtensionQueryOptions"/> class.
        /// </summary>
        public MessagingExtensionQueryOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.MessagingExtensionQueryOptions"/> class.
        /// </summary>
        /// <param name="skip">Number of entities to skip.</param>
        /// <param name="count">Number of entities to fetch.</param>
        public MessagingExtensionQueryOptions(int? skip = default, int? count = default)
        {
            Skip = skip;
            Count = count;
        }

        /// <summary>
        /// Gets or sets number of entities to skip.
        /// </summary>
        /// <value>The number of entities to skip.</value>
        public int? Skip { get; set; }

        /// <summary>
        /// Gets or sets number of entities to fetch.
        /// </summary>
        /// <value>The number of entities to fetch.</value>
        public int? Count { get; set; }
    }
}
