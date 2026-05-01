// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.SharePoint.Models.Actions
{
    /// <summary>
    /// SharePoint Confirmation Dialog object.
    /// </summary>
    public class ConfirmationDialog
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.Actions.ConfirmationDialog"/> class.
        /// </summary>
        public ConfirmationDialog()
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or Sets the title of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the title to display.</value>
        public string Title { get; set; }

        /// <summary>
        /// Gets or Sets the message of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the message to display.</value>
        public string Message { get; set; }
    }
}
