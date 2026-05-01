// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.SharePoint.Models.CardView
{
    /// <summary>
    /// Adaptive Card Extension text input component.
    /// </summary>
    public class CardTextInputComponent : BaseCardComponent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.CardView.CardTextInputComponent"/> class.
        /// </summary>
        public CardTextInputComponent()
            : base(CardComponentName.TextInput)
        {
        }

        /// <summary>
        /// Gets or sets the placeholder text to display in the text input.
        /// </summary>
        /// <value>Placeholder text to display.</value>
        public string Placeholder { get; set; }

        /// <summary>
        /// Gets or sets the default text value of the text input.
        /// </summary>
        /// <value>Default value to display in the text input.</value>
        public string DefaultValue { get; set; }

        /// <summary>
        /// Gets or sets the text input's button configuration.
        /// </summary>
        /// <value>Text input's button configuration.</value>
        public CardTextInputBaseButton Button { get; set; }

        /// <summary>
        /// Gets or sets properties for an optional icon, displayed in the left end of the text input.
        /// </summary>
        /// <value>Properties for an optional icon.</value>
        public CardImage IconBefore { get; set; }

        /// <summary>
        /// Gets or sets properties for an optional icon, displayed in the right end of the text input.
        /// </summary>
        /// <value>Properties for an optional icon.</value>
        public CardImage IconAfter { get; set; }
    }
}
