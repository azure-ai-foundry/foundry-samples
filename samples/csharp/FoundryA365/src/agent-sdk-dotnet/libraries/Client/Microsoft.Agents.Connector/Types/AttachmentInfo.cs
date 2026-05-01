// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;

namespace Microsoft.Agents.Connector.Types
{
    /// <summary> Metadata for an attachment. </summary>
    public class AttachmentInfo
    {
        /// <summary> Initializes a new instance of AttachmentInfo. </summary>
        public AttachmentInfo()
        {
            Views = [];
        }

        /// <summary> Initializes a new instance of AttachmentInfo. </summary>
        /// <param name="name"> Name of the attachment. </param>
        /// <param name="type"> ContentType of the attachment. </param>
        /// <param name="views"> attachment views. </param>
        public AttachmentInfo(string name = default, string type = default, IList<AttachmentView> views = default)
        {
            Name = name;
            Type = type;
            Views = views;
        }

        /// <summary> Name of the attachment. </summary>
        public string Name { get; set; }
        /// <summary> ContentType of the attachment. </summary>
        public string Type { get; set; }
        /// <summary> attachment views. </summary>
        public IList<AttachmentView> Views { get; set; }
    }
}
