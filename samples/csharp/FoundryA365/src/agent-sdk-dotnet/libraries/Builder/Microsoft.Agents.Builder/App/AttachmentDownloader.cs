// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    public class AttachmentDownloader : IInputFileDownloader
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public AttachmentDownloader(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public async Task<IList<InputFile>> DownloadFilesAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken = default)
        {
            if (turnContext.Activity.ChannelId.IsParentChannel(Channels.Msteams))
            {
                return [];
            }

            if (turnContext.Activity.Attachments == null || turnContext.Activity.Attachments.Count == 0)
            {
                return [];
            }

            List<InputFile> files = [];

            foreach (Attachment attachment in turnContext.Activity.Attachments)
            {
                InputFile? file = await DownloadFileAsync(attachment);
                if (file != null)
                {
                    files.Add(file);
                }
            }

            return files;
        }

        private async Task<InputFile?> DownloadFileAsync(Attachment attachment)
        {
            string? name = attachment.Name;

            using var httpClient = _httpClientFactory.CreateClient(nameof(AttachmentDownloader));

            if (attachment.ContentUrl != null && (attachment.ContentUrl.StartsWith("https://") || attachment.ContentUrl.StartsWith("http://localhost")))
            {
                // Determine where the file is hosted.
                var remoteFileUrl = attachment.ContentUrl;

                using HttpRequestMessage request = new(HttpMethod.Get, remoteFileUrl);
                HttpResponseMessage response = await httpClient.SendAsync(request).ConfigureAwait(false);

                // Failed to download file
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                // Convert to a buffer
                byte[] content = await response.Content.ReadAsByteArrayAsync();

                // Fixup content type
                string contentType = response.Content.Headers.ContentType.MediaType;
                if (contentType.StartsWith("image/"))
                {
                    contentType = "image/png";
                }

                return new InputFile(new BinaryData(content), contentType)
                {
                    ContentUrl = attachment.ContentUrl,
                    Filename = name
                };
            }
            else
            {
                return new InputFile(new BinaryData(attachment.Content), attachment.ContentType)
                {
                    ContentUrl = attachment.ContentUrl,
                    Filename = name
                };
            }
        }
    }
}
