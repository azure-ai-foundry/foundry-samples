// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Collections.Generic;

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Messaging extension result.
    /// </summary>
    public class MessagingExtensionResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.MessagingExtensionResult"/> class.
        /// </summary>
        public MessagingExtensionResult()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.MessagingExtensionResult"/> class.
        /// </summary>
        /// <param name="attachmentLayout">Hint for how to deal with multiple
        /// attachments. Possible values include: 'list', 'grid'.</param>
        /// <param name="type">The type of the result. Possible values include:
        /// 'result', 'auth', 'config', 'message', 'botMessagePreview'.</param>
        /// <param name="attachments">(Only when type is result)
        /// Attachments.</param>
        /// <param name="suggestedActions">The message extension suggested actions.</param>
        /// <param name="text">(Only when type is message) Text.</param>
        /// <param name="activityPreview">(Only when type is botMessagePreview) Message activity to preview.</param>
        public MessagingExtensionResult(string attachmentLayout = default, string type = default, IList<MessagingExtensionAttachment> attachments = default, MessagingExtensionSuggestedAction suggestedActions = default, string text = default, Activity activityPreview = default)
        {
            AttachmentLayout = attachmentLayout;
            Type = type;
            Attachments = attachments;
            SuggestedActions = suggestedActions;
            Text = text;
            ActivityPreview = activityPreview;
        }

        /// <summary>
        /// Gets or sets hint for how to deal with multiple attachments.
        /// Possible values include: 'list', 'grid'.
        /// </summary>
        /// <value>The hint for how to deal with multiple attachments.</value>
        public string AttachmentLayout { get; set; }

        /// <summary>
        /// Gets or sets the type of the result. Possible values include:
        /// 'result', 'auth', 'config', 'message', 'botMessagePreview'.
        /// </summary>
        /// <value>The type of the result.</value>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets (Only when type is result) Attachments.
        /// </summary>
        /// <value>The attachments.</value>
        public IList<MessagingExtensionAttachment> Attachments { get; set; }

        /// <summary>
        /// Gets or sets the suggested actions.
        /// </summary>
        /// <value>The suggested actions.</value>
        public MessagingExtensionSuggestedAction SuggestedActions { get; set; }

        /// <summary>
        /// Gets or sets (Only when type is message) Text.
        /// </summary>
        /// <value>The message text.</value>
        public string Text { get; set; }

        /// <summary>
        /// Gets or sets (Only when type is botMessagePreview) Message activity
        /// to preview.
        /// </summary>
        /// <value>The message activity to preview.</value>
        public Activity ActivityPreview { get; set; }
    }
}
