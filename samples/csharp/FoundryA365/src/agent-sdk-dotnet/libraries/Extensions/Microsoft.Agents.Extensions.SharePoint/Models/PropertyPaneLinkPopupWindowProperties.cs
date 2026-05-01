// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.SharePoint.Models
{
    /// <summary>
    /// SharePoint property pane link popup window properties object.
    /// </summary>
    public class PropertyPaneLinkPopupWindowProperties
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.PropertyPaneLinkPopupWindowProperties"/> class.
        /// </summary>
        public PropertyPaneLinkPopupWindowProperties()
        {
            // Do nothing
        }

        /// <summary>
        /// This enum contains the different types of fields.
        /// </summary>
        public enum PopupWindowPosition
        {
            /// <summary>
            /// Center.
            /// </summary>
            Center = 0,

            /// <summary>
            /// Right Top.
            /// </summary>
            RightTop = 1,

            /// <summary>
            /// Left Top .
            /// </summary>
            LeftTop = 2,

            /// <summary>
            /// Right Bottom.
            /// </summary>
            RightBottom = 3,

            /// <summary>
            /// Left Bottom.
            /// </summary>
            LeftBottom = 4,
        }

        /// <summary>
        /// Gets or Sets the height of the pop up window of type <see cref="System.Int32"/>.
        /// </summary>
        /// <value>This value is the height of the property pane popup.</value>
        public int Height { get; set; }

        /// <summary>
        /// Gets or Sets the position of pop up window <see cref="Microsoft.Agents.Extensions.SharePoint.Models.PropertyPaneLinkPopupWindowProperties.PopupWindowPosition"/> enum.
        /// </summary>
        /// <value>This value is the window position of the property pane popup.</value>
        public PopupWindowPosition PositionWindowPosition { get; set; }

        /// <summary>
        /// Gets or Sets the title of pop up window of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the title of the property pane popup.</value>
        public string Title { get; set; }

        /// <summary>
        /// Gets or Sets the width of the pop up window of type <see cref="System.Int32"/>.
        /// </summary>
        /// <value>This value is the width of the property pane popup.</value>
        public int Width { get; set; }
    }
}
