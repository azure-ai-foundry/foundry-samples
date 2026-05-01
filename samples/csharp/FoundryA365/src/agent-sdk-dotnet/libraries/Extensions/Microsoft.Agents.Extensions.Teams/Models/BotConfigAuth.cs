// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Specifies Agent config auth, including type and suggestedActions.
    /// </summary>
    public class BotConfigAuth
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.BotConfigAuth"/> class.
        /// </summary>
        public BotConfigAuth()
        {
        }

        /// <summary>
        /// Gets or sets type of bot config auth.
        /// </summary>
        /// <value>
        /// The type of bot config auth.
        /// </value>
        public string Type { get; set; } = "auth";

        /// <summary>
        /// Gets or sets suggested actions. 
        /// </summary>
        /// <value>
        /// The suggested actions of bot config auth.
        /// </value>
        public SuggestedActions SuggestedActions { get; set; }
    }
}
