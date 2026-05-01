// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

namespace Microsoft.Agents.Core.Models
{
    /// <summary> An item on a receipt card. </summary>
    public class ReceiptItem
    {
        /// <summary> Initializes a new instance of ReceiptItem. </summary>
        public ReceiptItem()
        {
        }

        /// <summary> Initializes a new instance of ReceiptItem. </summary>
        /// <param name="title"> Title of the Card. </param>
        /// <param name="subtitle"> Subtitle appears just below Title field, differs from Title in font styling only. </param>
        /// <param name="text"> Text field appears just below subtitle, differs from Subtitle in font styling only. </param>
        /// <param name="image"> An image on a card. </param>
        /// <param name="price"> Amount with currency. </param>
        /// <param name="quantity"> Number of items of given kind. </param>
        /// <param name="tap"> A clickable action. </param>
        public ReceiptItem(string title = default, string subtitle = default, string text = default, CardImage image = default, string price = default, string quantity = default, CardAction tap = default)
        {
            Title = title;
            Subtitle = subtitle;
            Text = text;
            Image = image;
            Price = price;
            Quantity = quantity;
            Tap = tap;
        }

        /// <summary> Title of the Card. </summary>
        public string Title { get; set; }
        /// <summary> Subtitle appears just below Title field, differs from Title in font styling only. </summary>
        public string Subtitle { get; set; }
        /// <summary> Text field appears just below subtitle, differs from Subtitle in font styling only. </summary>
        public string Text { get; set; }
        /// <summary> An image on a card. </summary>
        public CardImage Image { get; set; }
        /// <summary> Amount with currency. </summary>
        public string Price { get; set; }
        /// <summary> Number of items of given kind. </summary>
        public string Quantity { get; set; }
        /// <summary> A clickable action. </summary>
        public CardAction Tap { get; set; }
    }
}
