// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Messaging extension response.
    /// </summary>
    public class MessagingExtensionResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.MessagingExtensionResponse"/> class.
        /// </summary>
        public MessagingExtensionResponse()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.MessagingExtensionResponse"/> class.
        /// </summary>
        /// <param name="composeExtension">A <see cref="Microsoft.Agents.Extensions.Teams.Models.MessagingExtensionResult"/> that initializes the current object's ComposeExension property.</param>
        public MessagingExtensionResponse(MessagingExtensionResult composeExtension = default)
        {
            ComposeExtension = composeExtension;
        }

        /// <summary>
        /// Gets or sets the compose extension.
        /// </summary>
        /// <value>The compose extension.</value>
        public MessagingExtensionResult ComposeExtension { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Microsoft.Agents.Extensions.Teams.Models.CacheInfo"/> for this <see cref="Microsoft.Agents.Extensions.Teams.Models.MessagingExtensionResponse"/>.
        /// module.
        /// </summary>
        /// <value>The <see cref="Microsoft.Agents.Extensions.Teams.Models.CacheInfo"/> for this <see cref="Microsoft.Agents.Extensions.Teams.Models.MessagingExtensionResponse"/>.</value>
        public CacheInfo CacheInfo { get; set; }
    }
}
