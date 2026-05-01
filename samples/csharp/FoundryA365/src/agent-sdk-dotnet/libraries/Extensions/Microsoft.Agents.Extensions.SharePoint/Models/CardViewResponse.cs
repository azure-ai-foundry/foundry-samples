// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.SharePoint.Models.Actions;
using Microsoft.Agents.Extensions.SharePoint.Models.CardView;

namespace Microsoft.Agents.Extensions.SharePoint.Models
{
    /// <summary>
    /// SharePoint Card View Data object.
    /// </summary>
    public class CardViewResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.CardViewResponse"/> class.
        /// </summary>
        public CardViewResponse()
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or Sets AceData for the card view of type <see cref="Microsoft.Agents.Extensions.SharePoint.Models.AceData"/>.
        /// </summary>
        /// <value>This value is the ace data of the card view response.</value>
        public AceData AceData { get; set; }

        /// <summary>
        /// Gets or sets the card view configuration.
        /// </summary>
        /// <value>Card view configuration.</value>
        public CardViewParameters CardViewParameters { get; set; }

        /// <summary>
        /// Gets or sets action to invoke when the card is selected.
        /// </summary>
        /// <value>Action to invoke.</value>
        public IOnCardSelectionAction OnCardSelection { get; set; }

        /// <summary>
        /// Gets or Sets the view Id of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the view id of the card view.</value>
        public string ViewId { get; set; }
    }
}
