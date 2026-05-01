// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Connector.Types;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Connector
{
    /// <summary>
    /// Attachments operations.
    /// </summary>
    public interface IAttachments
    {
        /// <summary>
        /// Get the URI of an attachment view.
        /// </summary>
        /// <param name="attachmentId">id of the attachment.</param>
        /// <param name="viewId">default is "original".</param>
        /// <returns>uri.</returns>
        string GetAttachmentUri(string attachmentId, string viewId = "original");

        /// <summary>
        /// Get AttachmentInfo structure describing the attachment views.
        /// </summary>
        /// <param name='attachmentId'>
        /// attachment id.
        /// </param>
        /// <param name='cancellationToken'>
        /// The cancellation token.
        /// </param>
        /// <returns>A task that represents the work queued to execute.</returns>
        Task<AttachmentInfo> GetAttachmentInfoAsync(string attachmentId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the named view as binary content.
        /// </summary>
        /// <param name='attachmentId'>
        /// attachment id.
        /// </param>
        /// <param name='viewId'>
        /// View id from attachmentInfo.
        /// </param>
        /// <param name='cancellationToken'>
        /// The cancellation token.
        /// </param>
        /// <returns>A task that represents the work queued to execute.</returns>
        Task<Stream> GetAttachmentAsync(string attachmentId, string viewId, CancellationToken cancellationToken = default);
    }
}
