// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.SharePoint.Models
{
    /// <summary>
    /// SharePoint property pane checkbox properties object.
    /// </summary>
    public class PropertyPaneCheckboxProperties : IPropertyPaneFieldProperties
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.PropertyPaneCheckboxProperties"/> class.
        /// </summary>
        public PropertyPaneCheckboxProperties()
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or Sets the label to display next to the checkbox of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the text of the checkbox property.</value>
        public string Text { get; set; }

        /// <summary>
        /// Gets or Sets a value indicating whether this control is enabled or not of type <see cref="System.Boolean"/>.
        /// </summary>
        /// <value>This value indicates if the control is disabled.</value>
        public bool Disabled { get; set; }

        /// <summary>
        /// Gets or Sets a value indicating whether the property pane checkbox is checked or not of type <see cref="System.Boolean"/>.
        /// </summary>
        /// <value>This value indicates if the control is checked.</value>
        public bool Checked { get; set; }
    }
}
