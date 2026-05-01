// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.SharePoint.Models
{
    /// <summary>
    /// SharePoint property pane page header object.
    /// </summary>
    public class PropertyPanePageHeader
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.PropertyPanePageHeader"/> class.
        /// </summary>
        public PropertyPanePageHeader()
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or Sets the description of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the description of the property pane page header.</value>
        public string Description { get; set; }
    }
}
