// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Current tab request context, i.e., the current theme.
    /// </summary>
    public class TabContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TabContext"/> class.
        /// </summary>
        public TabContext()
        {
        }

        /// <summary>
        /// Gets or sets the current user's theme.
        /// </summary>
        /// <value>
        /// The current user's theme.
        /// </value>
        public string Theme { get; set; }
    }
}
