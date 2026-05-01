// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.SharePoint.Models.CardView
{
    /// <summary>
    /// Adaptive Card Extension search box component.
    /// </summary>
    public class CardSearchBoxComponent : BaseCardComponent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.CardView.CardSearchBoxComponent"/> class.
        /// </summary>
        public CardSearchBoxComponent()
            : base(CardComponentName.SearchBox)
        {
        }

        /// <summary>
        /// Gets or sets the placeholder text to display in the sarch box.
        /// </summary>
        /// <value>Placeholder text to display.</value>
        public string Placeholder { get; set; }

        /// <summary>
        /// Gets or sets the default text value of the search box.
        /// </summary>
        /// <value>Default value to display in the search box.</value>
        public string DefaultValue { get; set; }

        /// <summary>
        /// Gets or sets the search box's button configuration.
        /// </summary>
        /// <value>Searh box's button configuration.</value>
        public CardSearchBoxButton Button { get; set; }
    }
}
