// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

namespace Microsoft.Agents.Core.Models
{
    /// <summary> An image on a card. </summary>
    public class CardImage
    {
        /// <summary> Initializes a new instance of CardImage. </summary>
        public CardImage()
        {
        }

        /// <summary> Initializes a new instance of CardImage. </summary>
        /// <param name="url"> URL thumbnail image for major content property. </param>
        /// <param name="alt"> Image description intended for screen readers. </param>
        /// <param name="tap"> A clickable action. </param>
        public CardImage(string url = default, string alt = default, CardAction tap = default)
        {
            Url = url;
            Alt = alt;
            Tap = tap;
        }

        /// <summary> URL thumbnail image for major content property. </summary>
        public string Url { get; set; }
        /// <summary> Image description intended for screen readers. </summary>
        public string Alt { get; set; }
        /// <summary> A clickable action. </summary>
        public CardAction Tap { get; set; }
    }
}
