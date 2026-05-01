// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// File info card.
    /// </summary>
    public class FileInfoCard
    {
        public const string ContentType = "application/vnd.microsoft.teams.card.file.info";

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.FileInfoCard"/> class.
        /// </summary>
        public FileInfoCard()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.FileInfoCard"/> class.
        /// </summary>
        /// <param name="uniqueId">Unique Id for the file.</param>
        /// <param name="fileType">Type of file.</param>
        /// <param name="etag">ETag for the file.</param>
        public FileInfoCard(string uniqueId = default, string fileType = default, object etag = default)
        {
            UniqueId = uniqueId;
            FileType = fileType;
            Etag = etag;
        }

        /// <summary>
        /// Gets or sets unique Id for the file.
        /// </summary>
        /// <value>The unique ID for the file.</value>
        public string UniqueId { get; set; }

        /// <summary>
        /// Gets or sets type of file.
        /// </summary>
        /// <value>The type of file.</value>
        public string FileType { get; set; }

        /// <summary>
        /// Gets or sets eTag for the file.
        /// </summary>
        /// <value>The eTag for the file.</value>
        public object Etag { get; set; }
    }
}
