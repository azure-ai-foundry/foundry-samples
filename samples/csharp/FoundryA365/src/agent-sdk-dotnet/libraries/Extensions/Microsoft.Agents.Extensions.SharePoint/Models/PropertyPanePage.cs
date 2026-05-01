// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Extensions.SharePoint.Models
{
    /// <summary>
    /// SharePoint property pane page object.
    /// </summary>
    public class PropertyPanePage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.PropertyPanePage"/> class.
        /// </summary>
        public PropertyPanePage()
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or Sets the groups of type <see cref="Microsoft.Agents.Extensions.SharePoint.Models.PropertyPaneGroup"/>.
        /// </summary>
        /// <value>This value is the groups of the property pane page.</value>
        public IEnumerable<IPropertyPaneGroupOrConditionalGroup> Groups { get; set; }

        /// <summary>
        /// Gets or Sets a value indicating whether the groups on the PropertyPanePage are displayed as accordion or not of type <see cref="System.Boolean"/>.
        /// </summary>
        /// <value>This value indicates whether the property pane page is displayed as an accordion.</value>
        public bool DisplayGroupsAsAccordion { get; set; }

        /// <summary>
        /// Gets or Sets the header for the property pane of type <see cref="Microsoft.Agents.Extensions.SharePoint.Models.PropertyPanePageHeader"/>.
        /// </summary>
        /// <value>This value is the header of the property pane page.</value>
        public PropertyPanePageHeader Header { get; set; }
    }
}
