// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.SharePoint.Models
{
    /// <summary>
    /// SharePoint property pane choice group image size object.
    /// </summary>
    public class PropertyPaneChoiceGroupImageSize
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.PropertyPaneChoiceGroupImageSize"/> class.
        /// </summary>
        public PropertyPaneChoiceGroupImageSize()
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or Sets the width of the image of type <see cref="System.Int32"/>.
        /// </summary>
        /// <value>This value is the width of the choice group.</value>
        public int Width { get; set; }

        /// <summary>
        /// Gets or Sets the height of the image of type <see cref="System.Int32"/>.
        /// </summary>
        /// <value>This value is the height of the choice group.</value>
        public int Height { get; set; }
    }
}
