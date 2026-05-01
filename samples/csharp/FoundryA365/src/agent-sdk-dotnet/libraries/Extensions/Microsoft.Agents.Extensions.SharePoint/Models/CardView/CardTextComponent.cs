// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.SharePoint.Models.CardView
{
    /// <summary>
    /// Adaptive Card Extension card text component.
    /// </summary>
    public class CardTextComponent : BaseCardComponent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.CardView.CardTextComponent"/> class.
        /// </summary>
        public CardTextComponent()
            : base(CardComponentName.Text)
        {
        }

        /// <summary>
        /// Gets or sets the text to display.
        /// </summary>
        /// <value>Text to display.</value>
        public string Text { get; set; }
    }
}
