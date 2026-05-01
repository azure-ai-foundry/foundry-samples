// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Extensions.SharePoint.Models
{
    /// <summary>
    /// SharePoint property pane drop down properties object.
    /// </summary>
    public class PropertyPaneDropDownProperties : IPropertyPaneFieldProperties
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.PropertyPaneDropDownProperties"/> class.
        /// </summary>
        public PropertyPaneDropDownProperties()
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or Sets optional ariaLabel flag. Text for screen-reader to announce regardless of toggle state. Of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the aria label of the drop down.</value>
        public string AriaLabel { get; set; }

        /// <summary>
        /// Gets or Sets an element's number or position in the current set of controls. Maps to native aria-positionset attribute. It starts from 1 of type <see cref="System.Int32"/>.
        /// </summary>
        /// <value>This value is the aria position in set of the drop down.</value>
        public int AriaPositionInSet { get; set; }

        /// <summary>
        /// Gets or Sets the number of items in the current set of controls. Maps to native aria-setsize attribute of type <see cref="System.Int32"/>.
        /// </summary>
        /// <value>This value is the aria set size of the drop down.</value>
        public int AriaSetSize { get; set; }

        /// <summary>
        /// Gets or Sets the label of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the label of the drop down.</value>
        public string Label { get; set; }

        /// <summary>
        /// Gets or Sets a value indicating whether this control is enabled or not of type <see cref="System.Boolean"/>.
        /// </summary>
        /// <value>This value indicates whether the property is disabled.</value>
        public bool Disabled { get; set; }

        /// <summary>
        /// Gets or Sets the error message of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the error message of the drop down.</value>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or Sets the key of the initially selected option of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the selected key of the drop down.</value>
        public string SelectedKey { get; set; }

        /// <summary>
        /// Gets or Sets the collection of options for this Dropdown of type <see cref="Microsoft.Agents.Extensions.SharePoint.Models.PropertyPaneDropDownOption"/>.
        /// </summary>
        /// <value>This value is the options of the drop down.</value>
        public IEnumerable<PropertyPaneDropDownOption> Options { get; set; }
    }
}
