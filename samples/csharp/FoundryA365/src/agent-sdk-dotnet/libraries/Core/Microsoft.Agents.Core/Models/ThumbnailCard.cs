// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> A thumbnail card (card with a single, small thumbnail image). </summary>
    public class ThumbnailCard
    {
        public ThumbnailCard() 
        {
            Images = [];
            Buttons = [];
        }

        /// <summary> Initializes a new instance of ThumbnailCard. </summary>
        /// <param name="title"> Title of the card. </param>
        /// <param name="subtitle"> Subtitle of the card. </param>
        /// <param name="text"> Text for the card. </param>
        /// <param name="images"> Array of images for the card. </param>
        /// <param name="buttons"> Set of actions applicable to the current card. </param>
        /// <param name="tap"> A clickable action. </param>
        public ThumbnailCard(string title = default, string subtitle = default, string text = default, IList<CardImage> images = default, IList<CardAction> buttons = default, CardAction tap = default)
        {
            Title = title;
            Subtitle = subtitle;
            Text = text;
            Images = images ?? [];
            Buttons = buttons ?? [];
            Tap = tap;
        }

        /// <summary>
        /// The content type value of a <see cref="Microsoft.Agents.Core.Models.ThumbnailCard"/>.
        /// </summary>
        public const string ContentType = Models.ContentTypes.ThumbnailCard;

        /// <summary>
        /// Creates a new attachment from <see cref="Microsoft.Agents.Core.Models.ThumbnailCard"/>.
        /// </summary>
        /// <param name="card"> The instance of <see cref="Microsoft.Agents.Core.Models.ThumbnailCard"/>.</param>
        /// <returns> The generated attachment.</returns>
        public Attachment ToAttachment()
        {
            return new Attachment
            {
                Content = this,
                ContentType = ContentType
            };
        }

        /// <summary> Title of the card. </summary>
        public string Title { get; set; }
        /// <summary> Subtitle of the card. </summary>
        public string Subtitle { get; set; }
        /// <summary> Text for the card. </summary>
        public string Text { get; set; }
        /// <summary> Array of images for the card. </summary>
        public IList<CardImage> Images { get; set; }
        /// <summary> Set of actions applicable to the current card. </summary>
        public IList<CardAction> Buttons { get; set; }
        /// <summary> A clickable action. </summary>
        public CardAction Tap { get; set; }
    }
}
