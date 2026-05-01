// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.SharePoint.Models.Actions
{
    /// <summary>
    /// SharePoint parameters for an quick view action.
    /// </summary>
    public class QuickViewActionParameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.Actions.QuickViewActionParameters"/> class.
        /// </summary>
        public QuickViewActionParameters()
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or Sets the quick view id to be opened as part of the action of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is quick view id to open.</value>
        public string View { get; set; }
    }
}
