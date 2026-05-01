// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Invoke ('tab/fetch') request value payload.
    /// </summary>
    public class TabRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TabRequest"/> class.
        /// </summary>
        public TabRequest()
        {
        }

        /// <summary>
        /// Gets or sets current tab entity request context.
        /// </summary>
        /// <value>
        /// Tab context for this <see cref="Microsoft.Agents.Extensions.Teams.Models.TabRequest"/>.
        /// </value>
        public TabEntityContext TabEntityContext { get; set; }

        /// <summary>
        /// Gets or sets current user context, i.e., the current theme.
        /// </summary>
        /// <value>
        /// Current user context, i.e., the current theme.
        /// </value>
        public TabContext Context { get; set; }

        /// <summary>
        /// Gets or sets state, which is the magic code for OAuth Flow.
        /// </summary>
        /// <value>
        /// State, which is the magic code for OAuth Flow.
        /// </value>
        public string State { get; set; }
    }
}
