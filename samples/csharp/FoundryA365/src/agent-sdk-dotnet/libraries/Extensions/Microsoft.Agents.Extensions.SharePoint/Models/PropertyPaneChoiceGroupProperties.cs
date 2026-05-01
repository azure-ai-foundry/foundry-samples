// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Extensions.SharePoint.Models
{
    /// <summary>
    /// SharePoint property pane choice group properties object.
    /// </summary>
    public class PropertyPaneChoiceGroupProperties : IPropertyPaneFieldProperties
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.PropertyPaneChoiceGroupProperties"/> class.
        /// </summary>
        public PropertyPaneChoiceGroupProperties()
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or Sets the label of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the label of the choice group.</value>
        public string Label { get; set; }

        /// <summary>
        /// Gets or Sets the collection of options for this choice group of type <see cref="Microsoft.Agents.Extensions.SharePoint.Models.PropertyPaneChoiceGroupOption"/>.
        /// </summary>
        /// <value>This value is the icon properties of the choice group.</value>
        public IEnumerable<PropertyPaneChoiceGroupOption> Options { get; set; }
    }
}
