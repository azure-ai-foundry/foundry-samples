// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Extensions.SharePoint.Models
{
    /// <summary>
    /// SharePoint property pane group object.
    /// </summary>
    public class PropertyPaneGroup : IPropertyPaneGroupOrConditionalGroup
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.PropertyPaneGroup"/> class.
        /// </summary>
        public PropertyPaneGroup()
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or Sets the group fields of type <see cref="Microsoft.Agents.Extensions.SharePoint.Models.PropertyPaneGroupField"/>.
        /// </summary>
        /// <value>This value is the group fields of the property pane group.</value>
        public IEnumerable<PropertyPaneGroupField> GroupFields { get; set; }

        /// <summary>
        /// Gets or Sets the group name of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the group name of the property pane group.</value>
        public string GroupName { get; set; }

        /// <summary>
        /// Gets or Sets a value indicating whether the PropertyPane group is collapsed or not of type <see cref="System.Boolean"/>.
        /// </summary>
        /// <value>This value indicates whether the property pane group is collapsed.</value>
        public bool IsCollapsed { get; set; }

        /// <summary>
        /// Gets or Sets a value indicating whether group name should be hidden of type <see cref="System.Boolean"/>.
        /// </summary>
        /// <value>This value indicates whether the property pane group is hidden.</value>
        public bool IsGroupNameHidden { get; set; }
    }
}
