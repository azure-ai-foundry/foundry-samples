// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.SharePoint.Models
{
    /// <summary>
    /// SharePoint property pane toggle properties object.
    /// </summary>
    public class PropertyPaneToggleProperties : IPropertyPaneFieldProperties
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.PropertyPaneToggleProperties"/> class.
        /// </summary>
        public PropertyPaneToggleProperties()
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or Sets optional ariaLabel flag. Text for screen-reader to announce regardless of toggle state. Of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the aria label of the toggle field.</value>
        public string AriaLabel { get; set; }

        /// <summary>
        /// Gets or Sets the label of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the label of the toggle field.</value>
        public string Label { get; set; }

        /// <summary>
        /// Gets or Sets a value indicating whether this control is enabled or not of type <see cref="System.Boolean"/>.
        /// </summary>
        /// <value>This value indicates whether the toggle field is disabled.</value>
        public bool Disabled { get; set; }

        /// <summary>
        /// Gets or Sets a value indicating whether the property pane checkbox is checked or not of type <see cref="System.Boolean"/>.
        /// </summary>
        /// <value>This value indicates whether the toggle field is checked.</value>
        public bool Checked { get; set; }

        /// <summary>
        /// Gets or Sets a key to uniquely identify the field of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the key of the toggle field.</value>
        public string Key { get; set; }

        /// <summary>
        /// Gets or Sets text to display when toggle is OFF of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the label of the toggle field when off.</value>
        public string OffText { get; set; }

        /// <summary>
        /// Gets or Sets text to display when toggle is ON of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the label of the toggle field when on.</value>
        public string OnText { get; set; }

        /// <summary>
        /// Gets or Sets text for screen-reader to announce when toggle is OFF of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the aria label of the toggle field when off.</value>
        public string OffAriaLabel { get; set; }

        /// <summary>
        /// Gets or Sets text for screen-reader to announce when toggle is ON of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the aria label of the toggle field when on.</value>
        public string OnAriaLabel { get; set; }
    }
}
