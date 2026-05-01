// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

namespace Microsoft.Agents.Connector.Types
{
    /// <summary> Attachment data. </summary>
    public class AttachmentData
    {
        /// <summary> Initializes a new instance of AttachmentData. </summary>
        public AttachmentData()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="Microsoft.Agents.Connector.Types.AttachmentData"/> class.</summary>
        /// <param name="type">Content-Type of the attachment.</param>
        /// <param name="name">Name of the attachment.</param>
        /// <param name="originalBase64">Attachment content.</param>
        /// <param name="thumbnailBase64">Attachment thumbnail.</param>
        public AttachmentData(string type = default, string name = default, byte[] originalBase64 = default, byte[] thumbnailBase64 = default)
        {
            Type = type;
            Name = name;
            OriginalBase64 = originalBase64;
            ThumbnailBase64 = thumbnailBase64;
        }

        /// <summary> Content-Type of the attachment. </summary>
        public string Type { get; set; }
        /// <summary> Name of the attachment. </summary>
        public string Name { get; set; }
        /// <summary> Attachment content. </summary>
        public byte[] OriginalBase64 { get; set; }
        /// <summary> Attachment thumbnail. </summary>
        public byte[] ThumbnailBase64 { get; set; }
    }
}
