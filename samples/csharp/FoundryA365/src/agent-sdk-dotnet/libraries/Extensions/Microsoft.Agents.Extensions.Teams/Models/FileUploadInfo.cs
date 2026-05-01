// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Information about the file to be uploaded.
    /// </summary>
    public class FileUploadInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.FileUploadInfo"/> class.
        /// </summary>
        public FileUploadInfo()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.FileUploadInfo"/> class.
        /// </summary>
        /// <param name="name">Name of the file.</param>
        /// <param name="uploadUrl">URL to an upload session that the bot can
        /// use to set the file contents.</param>
        /// <param name="contentUrl">URL to file.</param>
        /// <param name="uniqueId">ID that uniquely identifies the
        /// file.</param>
        /// <param name="fileType">Type of the file.</param>
        public FileUploadInfo(string name = default, string uploadUrl = default, string contentUrl = default, string uniqueId = default, string fileType = default)
        {
            Name = name;
            UploadUrl = uploadUrl;
            ContentUrl = contentUrl;
            UniqueId = uniqueId;
            FileType = fileType;
        }

        /// <summary>
        /// Gets or sets name of the file.
        /// </summary>
        /// <value>The name of the file.</value>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets URL to an upload session that the bot can use to set
        /// the file contents.
        /// </summary>
        /// <value>The URL to an upload session that the bot can use to set the file contents.</value>
        public string UploadUrl { get; set; }

        /// <summary>
        /// Gets or sets URL to file.
        /// </summary>
        /// <value>The URL to the file content.</value>
        public string ContentUrl { get; set; }

        /// <summary>
        /// Gets or sets ID that uniquely identifies the file.
        /// </summary>
        /// <value>The unique file ID.</value>
        public string UniqueId { get; set; }

        /// <summary>
        /// Gets or sets type of the file.
        /// </summary>
        /// <value>The type of the file.</value>
        public string FileType { get; set; }
    }
}
