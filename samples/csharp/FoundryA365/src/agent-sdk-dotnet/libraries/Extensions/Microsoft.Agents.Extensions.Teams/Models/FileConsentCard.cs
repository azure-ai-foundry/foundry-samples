// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// File consent card attachment.
    /// </summary>
    public class FileConsentCard
    {
        /// <summary>
        /// Content type to be used in the type property.
        /// </summary>
        public const string ContentType = "application/vnd.microsoft.teams.card.file.consent";

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.FileConsentCard"/> class.
        /// </summary>
        public FileConsentCard()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.FileConsentCard"/> class.
        /// </summary>
        /// <param name="description">File description.</param>
        /// <param name="sizeInBytes">Size of the file to be uploaded in
        /// Bytes.</param>
        /// <param name="acceptContext">Context sent back to the Bot if user
        /// consented to upload. This is free flow schema and is sent back in
        /// Value field of Activity.</param>
        /// <param name="declineContext">Context sent back to the Bot if user
        /// declined. This is free flow schema and is sent back in Value field
        /// of Activity.</param>
        public FileConsentCard(string description = default, long? sizeInBytes = default, object acceptContext = default, object declineContext = default)
        {
            Description = description;
            SizeInBytes = sizeInBytes;
            AcceptContext = acceptContext;
            DeclineContext = declineContext;
        }

        /// <summary>
        /// Gets or sets file description.
        /// </summary>
        /// <value>The file description.</value>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets size of the file to be uploaded in Bytes.
        /// </summary>
        /// <value>The size of the file to be uploaded in bytes.</value>
        public long? SizeInBytes { get; set; }

        /// <summary>
        /// Gets or sets context sent back to the Bot if user consented to
        /// upload. This is free flow schema and is sent back in Value field of
        /// Activity.
        /// </summary>
        /// <value>The context to send back if user consented to upload.</value>
        public object AcceptContext { get; set; }

        /// <summary>
        /// Gets or sets context sent back to the Bot if user declined. This is
        /// free flow schema and is sent back in Value field of Activity.
        /// </summary>
        /// <value>The context to send back to the Bot if user declined.</value>
        public object DeclineContext { get; set; }
    }
}
