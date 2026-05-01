// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Invoke ('tab/submit') request value payload.
    /// </summary>
    public class TabSubmit
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TabSubmit"/> class.
        /// </summary>
        public TabSubmit()
        {
        }

        /// <summary>
        /// Gets or sets current tab entity request context.
        /// </summary>
        /// <value>
        /// Tab context for this <see cref="Microsoft.Agents.Extensions.Teams.Models.TabSubmit"/>.
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
        /// Gets or sets user input data. Free payload containing properties of key-value pairs.
        /// </summary>
        /// <value>
        /// User input data. Free payload containing properties of key-value pairs.
        /// </value>
        public TabSubmitData Data { get; set; }
    }
}
