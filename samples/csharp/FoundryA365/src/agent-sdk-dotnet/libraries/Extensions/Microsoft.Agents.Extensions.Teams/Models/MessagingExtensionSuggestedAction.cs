// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Collections.Generic;

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Messaging extension Actions (Only when type is auth or config).
    /// </summary>
    public class MessagingExtensionSuggestedAction
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.MessagingExtensionSuggestedAction"/> class.
        /// </summary>
        public MessagingExtensionSuggestedAction()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.MessagingExtensionSuggestedAction"/> class.
        /// </summary>
        /// <param name="actions">Actions.</param>
        public MessagingExtensionSuggestedAction(IList<CardAction> actions = default(IList<CardAction>))
        {
            Actions = actions;
        }

        /// <summary>
        /// Gets or sets actions.
        /// </summary>
        /// <value>The actions.</value>
        public IList<CardAction> Actions { get; set; }
    }
}
