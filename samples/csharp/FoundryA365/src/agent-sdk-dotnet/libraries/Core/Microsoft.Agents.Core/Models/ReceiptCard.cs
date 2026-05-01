// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> A receipt card. </summary>
    public class ReceiptCard
    {
        /// <summary> Initializes a new instance of ReceiptCard. </summary>
        public ReceiptCard()
        {
            Facts = [];
            Items = [];
            Buttons = [];
        }

        /// <summary> Initializes a new instance of ReceiptCard. </summary>
        /// <param name="title"> Title of the card. </param>
        /// <param name="facts"> Array of Fact objects. </param>
        /// <param name="items"> Array of Receipt Items. </param>
        /// <param name="tap"> A clickable action. </param>
        /// <param name="total"> Total amount of money paid (or to be paid). </param>
        /// <param name="tax"> Total amount of tax paid (or to be paid). </param>
        /// <param name="vat"> Total amount of VAT paid (or to be paid). </param>
        /// <param name="buttons"> Set of actions applicable to the current card. </param>
        public ReceiptCard(string title = default, IList<Fact> facts = default, IList<ReceiptItem> items = default, CardAction tap = default, string total = default, string tax = default, string vat = default, IList<CardAction> buttons = default)
        {
            Title = title;
            Facts = facts ?? [];
            Items = items ?? [];
            Tap = tap;
            Total = total;
            Tax = tax;
            Vat = vat;
            Buttons = buttons ?? [];
        }

        /// <summary>
        /// The content type value of a <see cref="Microsoft.Agents.Core.Models.ReceiptCard"/>.
        /// </summary>
        public const string ContentType = "application/vnd.microsoft.card.receipt";

        /// <summary>
        /// Creates a new attachment from <see cref="Microsoft.Agents.Core.Models.ReceiptCard"/>.
        /// </summary>
        /// <param name="card"> The instance of <see cref="Microsoft.Agents.Core.Models.ReceiptCard"/>.</param>
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
        /// <summary> Array of Fact objects. </summary>
        public IList<Fact> Facts { get; set; }
        /// <summary> Array of Receipt Items. </summary>
        public IList<ReceiptItem> Items { get; set; }
        /// <summary> A clickable action. </summary>
        public CardAction Tap { get; set; }
        /// <summary> Total amount of money paid (or to be paid). </summary>
        public string Total { get; set; }
        /// <summary> Total amount of tax paid (or to be paid). </summary>
        public string Tax { get; set; }
        /// <summary> Total amount of VAT paid (or to be paid). </summary>
        public string Vat { get; set; }
        /// <summary> Set of actions applicable to the current card. </summary>
        public IList<CardAction> Buttons { get; set; }
    }
}
