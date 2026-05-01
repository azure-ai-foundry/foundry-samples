// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.SharePoint.Models
{
    /// <summary>
    /// SharePoint property pane choice group icon properties object.
    /// </summary>
    public class PropertyPaneChoiceGroupIconProperties
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.PropertyPaneChoiceGroupIconProperties"/> class.
        /// </summary>
        public PropertyPaneChoiceGroupIconProperties()
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or Sets the name of the icon to use from the Office Fabric icon set of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the office fabric icon font name of the choice group.</value>
        public string OfficeFabricIconFontName { get; set; }
    }
}
