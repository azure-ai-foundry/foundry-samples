// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Represents the value of the invoke activity sent when the user acts on
    /// a file consent card.
    /// </summary>
    public class FileConsentCardResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.FileConsentCardResponse"/> class.
        /// </summary>
        public FileConsentCardResponse()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.FileConsentCardResponse"/> class.
        /// </summary>
        /// <param name="action">The action the user took. Possible values
        /// include: 'accept', 'decline'.</param>
        /// <param name="context">The context associated with the
        /// action.</param>
        /// <param name="uploadInfo">If the user accepted the file, contains
        /// information about the file to be uploaded.</param>
        public FileConsentCardResponse(string action = default, object context = default, FileUploadInfo uploadInfo = default)
        {
            Action = action;
            Context = context;
            UploadInfo = uploadInfo;
        }

        /// <summary>
        /// Gets or sets the action the user took. Possible values include:
        /// 'accept', 'decline'.
        /// </summary>
        /// <value>The action the user took.</value>
        public string Action { get; set; }

        /// <summary>
        /// Gets or sets the context associated with the action.
        /// </summary>
        /// <value>The context associated with the action.</value>
        public object Context { get; set; }

        /// <summary>
        /// Gets or sets if the user accepted the file, contains information
        /// about the file to be uploaded.
        /// </summary>
        /// <value>The information about the file to be uploaded.</value>
        public FileUploadInfo UploadInfo { get; set; }
    }
}
