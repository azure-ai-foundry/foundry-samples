// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Envelope for Card Tab Response Payload.
    /// </summary>
    public class TabResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TabResponse"/> class.
        /// </summary>
        public TabResponse()
        {
        }

        /// <summary>
        /// Gets or sets the response to the tab/fetch message.
        /// Possible values for the tab type include: 'continue', 'auth' or 'silentAuth'.
        /// </summary>
        /// <value>
        /// Cards in response to a <see cref="Microsoft.Agents.Extensions.Teams.Models.TabRequest"/>.
        /// </value>
        public TabResponsePayload Tab { get; set; }
    }
}
