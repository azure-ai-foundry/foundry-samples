// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.SharePoint.Models.CardView
{
    /// <summary>
    /// Adaptive Card Extension card bar component.
    /// </summary>
    public class CardBarComponent : BaseCardComponent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.CardView.CardBarComponent"/> class.
        /// </summary>
        public CardBarComponent()
            : base(CardComponentName.CardBar)
        {
        }

        /// <summary>
        /// Gets or sets the title to display.
        /// </summary>
        /// <value>Title value to display in the card bar.</value>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the icon to display.
        /// </summary>
        /// <value>Icon to display in the card bar.</value>
        public CardImage Icon { get; set; }
    }
}
