// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.SharePoint.Models.CardView
{
    /// <summary>
    /// Properties for the image rendered in a card view.
    /// </summary>
    public class CardImage
    {
        /// <summary>
        /// Gets or sets the URL to display as image or icon name.
        /// </summary>
        /// <value>image URL or icon name.</value>
        public string Image { get; set; }

        /// <summary>
        /// Gets or sets the alt text for the image.
        /// </summary>
        /// <value>Alt text for the image.</value>
        public string AltText { get; set; }
    }
}
