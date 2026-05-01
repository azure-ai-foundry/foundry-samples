// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.SharePoint.Models
{
    /// <summary>
    /// SharePoint property pane link properties object.
    /// </summary>
    public class PropertyPaneLinkProperties : IPropertyPaneFieldProperties
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.PropertyPaneLinkProperties"/> class.
        /// </summary>
        public PropertyPaneLinkProperties()
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or Sets optional ariaLabel flag. Text for screen-reader to announce regardless of toggle state. Of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the aria label of the property pane link.</value>
        public string AriaLabel { get; set; }

        /// <summary>
        /// Gets or Sets a value indicating whether this control is enabled or not of type <see cref="System.Boolean"/>.
        /// </summary>
        /// <value>This value indicates whether the property pane link is disabled.</value>
        public bool Disabled { get; set; }

        /// <summary>
        /// Gets or Sets the location to which the link is targeted to of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the href of the property pane link.</value>
        public string Href { get; set; }

        /// <summary>
        /// Gets or Sets the props of popup window. of type <see cref="Microsoft.Agents.Extensions.SharePoint.Models.PropertyPaneLinkPopupWindowProperties"/>.
        /// </summary>
        /// <value>This value is the popup window properties of the property pane link.</value>
        public PropertyPaneLinkPopupWindowProperties PopupWindowProps { get; set; }

        /// <summary>
        /// Gets or Sets where to display the linked resource of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the target of the property pane link.</value>
        public string Target { get; set; }

        /// <summary>
        /// Gets or Sets the display text for the link of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the text of the property pane link.</value>
        public string Text { get; set; }
    }
}
