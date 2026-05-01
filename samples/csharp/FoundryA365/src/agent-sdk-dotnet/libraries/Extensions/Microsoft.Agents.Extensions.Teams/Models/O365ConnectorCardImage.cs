// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// O365 connector card image.
    /// </summary>
    public class O365ConnectorCardImage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.O365ConnectorCardImage"/> class.
        /// </summary>
        public O365ConnectorCardImage()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.O365ConnectorCardImage"/> class.
        /// </summary>
        /// <param name="image">URL for the image.</param>
        /// <param name="title">Alternative text for the image.</param>
        public O365ConnectorCardImage(string image = default, string title = default)
        {
            Image = image;
            Title = title;
        }

        /// <summary>
        /// Gets or sets URL for the image.
        /// </summary>
        /// <value>The URL for the image.</value>
        public string Image { get; set; }

        /// <summary>
        /// Gets or sets alternative text for the image.
        /// </summary>
        /// <value>The alternative text for the image.</value>
        public string Title { get; set; }
    }
}
