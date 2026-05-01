// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Envelope for cards for a Tab request.
    /// </summary>
    public class TabResponseCard
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TabResponseCard"/> class.
        /// </summary>
        public TabResponseCard()
        {
        }

        /// <summary>
        /// Gets or sets adaptive card for this card tab response.
        /// </summary>
        /// <value>
        /// Cards for this <see cref="Microsoft.Agents.Extensions.Teams.Models.TabResponse"/>.
        /// </value>
        public object Card { get; set; }
    }
}
