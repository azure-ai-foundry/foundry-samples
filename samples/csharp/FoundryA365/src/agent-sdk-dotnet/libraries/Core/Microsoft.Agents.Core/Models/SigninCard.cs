// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> A card representing a request to sign in. </summary>
    public class SigninCard
    {
        public SigninCard() 
        {
            Buttons = [];
        }

        /// <summary> Initializes a new instance of SigninCard. </summary>
        /// <param name="text"> Text for signin request. </param>
        /// <param name="buttons"> Action to use to perform signin. </param>
        public SigninCard(string text = default, IList<CardAction> buttons = default)
        {
            Text = text;
            Buttons = buttons ?? [];
        }

        /// <summary>
        /// The content type value of a <see cref="Microsoft.Agents.Core.Models.SigninCard"/>.
        /// </summary>
        public const string ContentType = Models.ContentTypes.SignInCard;

        /// <summary>
        /// Creates a new attachment from <see cref="Microsoft.Agents.Core.Models.SigninCard"/>.
        /// </summary>
        /// <param name="card"> The instance of <see cref="Microsoft.Agents.Core.Models.SigninCard"/>.</param>
        /// <returns> The generated attachment.</returns>
        public Attachment ToAttachment()
        {
            return new Attachment
            {
                Content = this,
                ContentType = ContentType
            };
        }

        /// <summary> Text for signin request. </summary>
        public string Text { get; set; }
        /// <summary> Action to use to perform signin. </summary>
        public IList<CardAction> Buttons { get; set; }

        /// <summary>
        /// Creates a <see cref="Microsoft.Agents.Core.Models.SigninCard"/>.
        /// </summary>
        /// <param name="text"> The <see cref="Microsoft.Agents.Core.Models.SigninCard.Text"/>text.</param>
        /// <param name="buttonLabel"> The signin button label.</param>
        /// <param name="url"> The sigin url.</param>
        /// <returns> The created sigin card.</returns>
        public static SigninCard Create(string text, string buttonLabel, string url)
        {
            return new SigninCard
            {
                Text = text,
                Buttons =
                [
                    new CardAction
                    {
                       Title = buttonLabel,
                       Type = ActionTypes.Signin,
                       Value = url,
                    },
                ],
            };
        }

    }
}
