// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> A card representing a request to perform a sign in via OAuth. </summary>
    public class OAuthCard
    {
        /// <summary> Initializes a new instance of OAuthCard. </summary>
        public OAuthCard()
        {
            Buttons = [];
        }

        /// <summary> Initializes a new instance of OAuthCard. </summary>
        /// <param name="text"> Text for signin request. </param>
        /// <param name="connectionName"> The name of the registered connection. </param>
        /// <param name="buttons"> Action to use to perform signin. </param>
        public OAuthCard(string text = default, string connectionName = default, IList<CardAction> buttons = default)
        {
            Text = text;
            ConnectionName = connectionName;
            Buttons = buttons ?? [];
        }

        /// <summary>
        /// The content type value of a <see cref="Microsoft.Agents.Core.Models.OAuthCard"/>.
        /// </summary>
        public const string ContentType = Models.ContentTypes.OAuthCard;

        /// <summary>
        /// Creates a new attachment from <see cref="Microsoft.Agents.Core.Models.OAuthCard"/>.
        /// </summary>
        /// <param name="card"> The instance of <see cref="Microsoft.Agents.Core.Models.OAuthCard"/>.</param>
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
        /// <summary> The name of the registered connection. </summary>
        public string ConnectionName { get; set; }
        /// <summary> Record for a token exchange request that is sent as part of Authentication. </summary>
        public TokenExchangeResource TokenExchangeResource { get; set; }
        /// <summary>
        /// Gets or sets the resource to directly post a token to token service.
        /// </summary>
        /// <value>The resource to directly post a token to token service.</value>
        public TokenPostResource TokenPostResource { get; set; }
        /// <summary> Action to use to perform signin. </summary>
        public IList<CardAction> Buttons { get; set; }
    }
}
