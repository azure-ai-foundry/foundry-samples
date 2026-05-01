// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.SharePoint.Models.Actions;
using System;

namespace Microsoft.Agents.Extensions.SharePoint.Models.CardView
{
    /// <summary>
    /// Adaptive Card Extension search footer component.
    /// </summary>
    public class CardSearchFooterComponent : BaseCardComponent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.CardView.CardSearchFooterComponent"/> class.
        /// </summary>
        public CardSearchFooterComponent()
            : base(CardComponentName.SearchFooter)
        {
            // this.ComponentName = CardComponentName.SearchFooter;
        }

        /// <summary>
        /// Gets or sets the title to display.
        /// </summary>
        /// <value>Title value to display in the search footer.</value>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets url to the image to use, should be a square aspect ratio and big enough to fit in the image area.
        /// </summary>
        /// <value>Image Url to display in the footer.</value>
        public Uri ImageUrl { get; set; }

        /// <summary>
        /// Gets or sets the initials to display in the image area when there is no image.
        /// </summary>
        /// <value>Initials to display in the image area when there is no image.</value>
        public string ImageInitials { get; set; }

        /// <summary>
        /// Gets or sets the primary text to display. For example, name of the person for people search.
        /// </summary>
        /// <value>Primary text to display.</value>
        public string Text { get; set; }

        /// <summary>
        /// Gets or sets action to invoke when the footer is selected.
        /// </summary>
        /// <value>Selection action.</value>
        public IAction OnSelection { get; set; }
    }
}
