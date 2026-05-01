// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> A basic card. </summary>
    public class BasicCard
    {
        /// <summary> Initializes a new instance of BasicCard. </summary>
        public BasicCard()
        {
            Images = [];
            Buttons = [];
        }

        /// <summary> Initializes a new instance of BasicCard. </summary>
        /// <param name="title"> Title of the card. </param>
        /// <param name="subtitle"> Subtitle of the card. </param>
        /// <param name="text"> Text for the card. </param>
        /// <param name="images"> Array of images for the card. </param>
        /// <param name="buttons"> Set of actions applicable to the current card. </param>
        /// <param name="tap"> A clickable action. </param>
        public BasicCard(string title = default, string subtitle = default, string text = default, IList<CardImage> images = default, IList<CardAction> buttons = default, CardAction tap = default)
        {
            Title = title;
            Subtitle = subtitle;
            Text = text;
            Images = images ?? [];
            Buttons = buttons ?? [];
            Tap = tap;
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
