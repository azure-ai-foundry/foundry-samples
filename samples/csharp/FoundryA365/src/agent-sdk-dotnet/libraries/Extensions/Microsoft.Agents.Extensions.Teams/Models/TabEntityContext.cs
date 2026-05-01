// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Current TabRequest entity context, or 'tabEntityId'.
    /// </summary>
    public class TabEntityContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TabEntityContext"/> class.
        /// </summary>
        public TabEntityContext()
        {
        }

        /// <summary>
        /// Gets or sets the entity id of the tab.
        /// </summary>
        /// <value>
        /// The entity id of the tab.
        /// </value>
        public string TabEntityId { get; set; }
    }
}
