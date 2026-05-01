// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

namespace Microsoft.Agents.Core.Models
{
    /// <summary> Media URL. </summary>
    public class MediaUrl
    {
        /// <summary> Initializes a new instance of MediaUrl. </summary>
        public MediaUrl()
        {
        }

        /// <summary> Initializes a new instance of MediaUrl. </summary>
        /// <param name="url"> Url for the media. </param>
        /// <param name="profile"> Optional profile hint to the client to differentiate multiple MediaUrl objects from each other. </param>
        public MediaUrl(string url = default, string profile = default)
        {
            Url = url;
            Profile = profile;
        }

        /// <summary> Url for the media. </summary>
        public string Url { get; set; }
        /// <summary> Optional profile hint to the client to differentiate multiple MediaUrl objects from each other. </summary>
        public string Profile { get; set; }
    }
}
