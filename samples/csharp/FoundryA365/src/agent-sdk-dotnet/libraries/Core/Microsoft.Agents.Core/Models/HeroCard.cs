// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> A Hero card (card with a single, large image). </summary>
    public class HeroCard
    {
        /// <summary> Initializes a new instance of HeroCard. </summary>
        public HeroCard()
        {
            Images = [];
            Buttons = [];
        }

        /// <summary> Initializes a new instance of HeroCard. </summary>
        /// <param name="title"> Title of the card. </param>
        /// <param name="subtitle"> Subtitle of the card. </param>
        /// <param name="text"> Text for the card. </param>
        /// <param name="images"> Array of images for the card. </param>
        /// <param name="buttons"> Set of actions applicable to the current card. </param>
        /// <param name="tap"> A clickable action. </param>
        public HeroCard(string title = default, string subtitle = default, string text = default, IList<CardImage> images = default, IList<CardAction> buttons = default, CardAction tap = default)
        {
            Title = title;
            Subtitle = subtitle;
            Text = text;
            Images = images ?? [];
            Buttons = buttons ?? [];
            Tap = tap;
        }

        /// <summary>
        /// The content type value of a <see cref="Microsoft.Agents.Core.Models.HeroCard"/>.
        /// </summary>
        public const string ContentType = Models.ContentTypes.HeroCard;

        /// <summary>
        /// Creates a new attachment from <see cref="Microsoft.Agents.Core.Models.HeroCard"/>.
        /// </summary>
        /// <param name="card"> The instance of <see cref="Microsoft.Agents.Core.Models.HeroCard"/>.</param>
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
