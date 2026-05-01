// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using System;
using System.Net.Http;

namespace Microsoft.Agents.Extensions.Teams.App
{
    /// <summary>
    /// Downloads attachments from Teams using the configured Token Provider (from IConnections).
    /// </summary>
    [Obsolete("Use Microsoft.Agents.Builder.App.M365AttachmentDownloader instead.")]
    public class TeamsAttachmentDownloader(
        Builder.App.M365AttachmentDownloaderOptions options, 
        IConnections connections, 
        IHttpClientFactory httpClientFactory) : Microsoft.Agents.Builder.App.M365AttachmentDownloader(connections, httpClientFactory, options)
    {
    }

    /// <summary>
    /// The TeamsAttachmentDownloader options
    /// </summary>
    [Obsolete("Use Microsoft.Agents.Builder.App.M365AttachmentDownloaderOptions instead.")]
    public class TeamsAttachmentDownloaderOptions : Microsoft.Agents.Builder.App.M365AttachmentDownloaderOptions
    {
    }
}
