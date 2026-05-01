// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.SharePoint.Models.Actions;

namespace Microsoft.Agents.Extensions.SharePoint.Models.CardView
{
    /// <summary>
    /// Names of the supported Adaptive Card Extension Card View button styles.
    /// </summary>
    public enum CardButtonStyle
    {
        /// <summary>
        /// Default style.
        /// </summary>
        Default,

        /// <summary>
        /// Positive (primary) style.
        /// </summary>
        Positive
    }

    /// <summary>
    /// Adaptive Card Extension card button component.
    /// </summary>
    public class CardButtonComponent : BaseCardComponent, ICardButtonBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.CardView.CardButtonComponent"/> class.
        /// </summary>
        public CardButtonComponent()
            : base(CardComponentName.CardButton)
        {
        }

        /// <summary>
        /// Gets or sets the button's action.
        /// </summary>
        /// <value>Button's action.</value>
        public IAction Action { get; set; }

        /// <summary>
        /// Gets or sets the text to display.
        /// </summary>
        /// <value>Text value to display in the card button.</value>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the style of the button.
        /// </summary>
        /// <value>Style of the button.</value>
        public CardButtonStyle Style { get; set; }
    }
}
