// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

namespace Microsoft.Agents.Core.Models
{
    /// <summary> Thumbnail URL. </summary>
    public class ThumbnailUrl
    {
        public ThumbnailUrl() { }

        /// <summary> Initializes a new instance of ThumbnailUrl. </summary>
        /// <param name="url"> URL pointing to the thumbnail to use for media content. </param>
        /// <param name="alt"> HTML alt text to include on this thumbnail image. </param>
        public ThumbnailUrl(string url = default, string alt = default)
        {
            Url = url;
            Alt = alt;
        }

        /// <summary> URL pointing to the thumbnail to use for media content. </summary>
        public string Url { get; set; }
        /// <summary> HTML alt text to include on this thumbnail image. </summary>
        public string Alt { get; set; }
    }
}
