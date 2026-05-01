// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Invoke request body type for app-based link query.
    /// </summary>
    public class AppBasedLinkQuery
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.AppBasedLinkQuery"/> class.
        /// </summary>
        public AppBasedLinkQuery()
        {
        }

        /// <summary>
        /// Initializes a new instance of the  <see cref="Microsoft.Agents.Extensions.Teams.Models.AppBasedLinkQuery"/> class.
        /// </summary>
        /// <param name="url">Url queried by user.</param>
        public AppBasedLinkQuery(string url = default)
        {
            Url = url;
        }

        /// <summary>
        /// Gets or sets url queried by user.
        /// </summary>
        /// <value>The URL queried by user.</value>
        public string Url { get; set; }

        /// <summary>
        /// Gets or sets state, which is the magic code for OAuth Flow.
        /// </summary>
        /// <value>The state, which is the magic code for OAuth Flow.</value>
        public string State { get; set; }
    }
}
