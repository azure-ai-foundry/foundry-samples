// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Payload for Tab Response.
    /// </summary>
    public class TabResponsePayload
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TabResponsePayload"/> class.
        /// </summary>
        public TabResponsePayload()
        {
        }

        /// <summary>
        /// Gets or sets choice of action options when responding to the
        /// tab/fetch message. Possible values include: 'continue', 'auth' or 'silentAuth'.
        /// </summary>
        /// <value>
        /// One of either: 'continue', 'auth' or 'silentAuth'.
        /// </value>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Microsoft.Agents.Extensions.Teams.Models.TabResponseCards"/> when responding to 
        /// tab/fetch activity with type of 'continue'.
        /// </summary>
        /// <value>
        /// Cards in response to a <see cref="Microsoft.Agents.Extensions.Teams.Models.TabResponseCards"/>.
        /// </value>
        public TabResponseCards Value { get; set; }

        /// <summary>
        /// Gets or sets the Suggested Actions for this card tab.
        /// </summary>
        /// <value>
        /// The Suggested Actions for this card tab.
        /// </value>
        public TabSuggestedActions SuggestedActions { get; set; }
    }
}
