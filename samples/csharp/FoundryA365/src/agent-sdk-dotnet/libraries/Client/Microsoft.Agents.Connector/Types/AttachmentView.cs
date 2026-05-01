// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

namespace Microsoft.Agents.Connector.Types
{
    /// <summary> Attachment View name and size. </summary>
    public class AttachmentView
    {
        /// <summary> Initializes a new instance of AttachmentView. </summary>
        public AttachmentView()
        {
        }

        /// <summary> Initializes a new instance of AttachmentView. </summary>
        /// <param name="viewId"> Id of the attachment. </param>
        /// <param name="size"> Size of the attachment. </param>
        public AttachmentView(string viewId = default, int? size = default)
        {
            ViewId = viewId;
            Size = size;
        }

        /// <summary> Id of the attachment. </summary>
        public string ViewId { get; set; }
        /// <summary> Size of the attachment. </summary>
        public int? Size { get; set; }
    }
}
