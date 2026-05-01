// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Response of messaging extension action.
    /// </summary>
    public class MessagingExtensionActionResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.MessagingExtensionActionResponse"/> class.
        /// </summary>
        public MessagingExtensionActionResponse()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.MessagingExtensionActionResponse"/> class.
        /// </summary>
        /// <param name="task">The JSON for the Adaptive card to appear in the
        /// task module.</param>
        /// <param name="composeExtension">A <see cref="Microsoft.Agents.Extensions.Teams.Models.MessagingExtensionResult"/> that initializes the current object's ComposeExension property.</param>
        public MessagingExtensionActionResponse(TaskModuleResponseBase task = default, MessagingExtensionResult composeExtension = default)
        {
            Task = task;
            ComposeExtension = composeExtension;
        }

        /// <summary>
        /// Gets or sets the JSON for the Adaptive card to appear in the task
        /// module.
        /// </summary>
        /// <value>The JSON for the Adaptive card to appear in the task module.</value>
        public TaskModuleResponseBase Task { get; set; }

        /// <summary>
        /// Gets or sets the compose extension result.
        /// </summary>
        /// <value>The compose extension result.</value>
        public MessagingExtensionResult ComposeExtension { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Microsoft.Agents.Extensions.Teams.Models.CacheInfo "/> for this <see cref="Microsoft.Agents.Extensions.Teams.Models.MessagingExtensionActionResponse"/>.
        /// </summary>
        /// <value>The <see cref="Microsoft.Agents.Extensions.Teams.Models.CacheInfo "/> for this <see cref="Microsoft.Agents.Extensions.Teams.Models.MessagingExtensionActionResponse"/>.</value>
        public CacheInfo CacheInfo { get; set; }
    }
}
