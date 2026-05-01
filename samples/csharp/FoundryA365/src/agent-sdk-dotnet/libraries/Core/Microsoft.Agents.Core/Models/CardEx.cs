// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Extension methods for converting strongly typed Card objects to <see cref="Microsoft.Agents.Core.Models.Attachment"/>.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Creates a new attachment from <see cref="Microsoft.Agents.Core.Models.HeroCard"/>.
        /// </summary>
        /// <param name="card"> The instance of <see cref="Microsoft.Agents.Core.Models.HeroCard"/>.</param>
        /// <returns> The generated attachment.</returns>
        public static Attachment ToAttachment(this HeroCard card)
        {
            return CreateAttachment(card, HeroCard.ContentType);
        }

        /// <summary>
        /// Creates a new attachment from <see cref="Microsoft.Agents.Core.Models.ThumbnailCard"/>.
        /// </summary>
        /// <param name="card"> The instance of <see cref="Microsoft.Agents.Core.Models.ThumbnailCard"/>.</param>
        /// <returns> The generated attachment.</returns>
        public static Attachment ToAttachment(this ThumbnailCard card)
        {
            return CreateAttachment(card, ThumbnailCard.ContentType);
        }

        /// <summary>
        /// Creates a new attachment from <see cref="Microsoft.Agents.Core.Models.SigninCard"/>.
        /// </summary>
        /// <param name="card"> The instance of <see cref="Microsoft.Agents.Core.Models.SigninCard"/>.</param>
        /// <returns> The generated attachment.</returns>
        public static Attachment ToAttachment(this SigninCard card)
        {
            return CreateAttachment(card, SigninCard.ContentType);
        }

        /// <summary>
        /// Creates a new attachment from <see cref="Microsoft.Agents.Core.Models.ReceiptCard"/>.
        /// </summary>
        /// <param name="card"> The instance of <see cref="Microsoft.Agents.Core.Models.ReceiptCard"/>.</param>
        /// <returns> The generated attachment.</returns>
        public static Attachment ToAttachment(this ReceiptCard card)
        {
            return CreateAttachment(card, ReceiptCard.ContentType);
        }

        /// <summary>
        /// Creates a new attachment from <see cref="Microsoft.Agents.Core.Models.AudioCard"/>.
        /// </summary>
        /// <param name="card"> The instance of <see cref="Microsoft.Agents.Core.Models.AudioCard"/>.</param>
        /// <returns> The generated attachment.</returns>
        public static Attachment ToAttachment(this AudioCard card)
        {
            return CreateAttachment(card, AudioCard.ContentType);
        }

        /// <summary>
        /// Creates a new attachment from <see cref="Microsoft.Agents.Core.Models.VideoCard"/>.
        /// </summary>
        /// <param name="card"> The instance of <see cref="Microsoft.Agents.Core.Models.VideoCard"/>.</param>
        /// <returns> The generated attachment.</returns>
        public static Attachment ToAttachment(this VideoCard card)
        {
            return CreateAttachment(card, VideoCard.ContentType);
        }

        /// <summary>
        /// Creates a new attachment from <see cref="Microsoft.Agents.Core.Models.AnimationCard"/>.
        /// </summary>
        /// <param name="card"> The instance of <see cref="Microsoft.Agents.Core.Models.AnimationCard"/>.</param>
        /// <returns> The generated attachment.</returns>
        public static Attachment ToAttachment(this AnimationCard card)
        {
            return CreateAttachment(card, AnimationCard.ContentType);
        }

        /// <summary>
        /// Creates a new attachment from <see cref="Microsoft.Agents.Core.Models.OAuthCard"/>.
        /// </summary>
        /// <param name="card"> The instance of <see cref="Microsoft.Agents.Core.Models.OAuthCard"/>.</param>
        /// <returns> The generated attachment.</returns>
        public static Attachment ToAttachment(this OAuthCard card)
        {
            return CreateAttachment(card, OAuthCard.ContentType);
        }

        private static Attachment CreateAttachment<T>(T card, string contentType)
        {
            return new Attachment
            {
                Content = card,
                ContentType = contentType,
            };
        }
    }
}
