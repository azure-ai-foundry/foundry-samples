// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using Microsoft.Agents.Connector.Errors;
using Microsoft.Agents.Connector.Telemetry.Scopes;
using Microsoft.Agents.Connector.Types;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Telemetry;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Microsoft.Agents.Connector.RestClients
{
    internal class AttachmentsRestClient(IRestTransport transport) : IAttachments
    {
        private readonly IRestTransport _transport = transport ?? throw new ArgumentNullException(nameof(_transport));

        /// <summary>
        /// Get the URI of an attachment view.
        /// </summary>
        /// <param name="attachmentId">id of the attachment.</param>
        /// <param name="viewId">default is "original".</param>
        /// <returns>uri.</returns>
        public string GetAttachmentUri(string attachmentId, string viewId = "original")
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(attachmentId, nameof(attachmentId));
            AssertionHelpers.ThrowIfNullOrWhiteSpace(viewId, nameof(viewId));

            return new Uri(_transport.Endpoint.EnsureTrailingSlash(),
                string.Format(RestApiPaths.AttachmentView,
                    Uri.EscapeDataString(attachmentId),
                    Uri.EscapeDataString(viewId))).ToString();
        }

        /// <summary> GetAttachmentInfo. </summary>
        /// <param name="attachmentId"> attachment id. </param>
        /// <param name="cancellationToken"> The cancellation token to use. </param>
        /// <exception cref="System.ArgumentNullException"> <paramref name="attachmentId"/> is null. </exception>
        /// <remarks> Get AttachmentInfo structure describing the attachment views. </remarks>
        public async Task<AttachmentInfo> GetAttachmentInfoAsync(string attachmentId, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(attachmentId, nameof(attachmentId));

            var request = RestRequest.Get(string.Format(RestApiPaths.AttachmentInfo, HttpUtility.UrlEncode(attachmentId)));

            using var telemetryScope = new ScopeGetAttachmentInfo(attachmentId);
            using var httpResponse = await RestPipeline.SendRawAsync(_transport, request, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    return await RestPipeline.ReadContentAsync<AttachmentInfo>(httpResponse, cancellationToken).ConfigureAwait(false);
                default:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.GetAttachmentInfoError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
            }
        }

        /// <summary> GetAttachment. </summary>
        /// <param name="attachmentId"> attachment id. </param>
        /// <param name="viewId"> View id from attachmentInfo. </param>
        /// <param name="cancellationToken"> The cancellation token to use. </param>
        /// <exception cref="System.ArgumentNullException"> <paramref name="attachmentId"/> or <paramref name="viewId"/> is null. </exception>
        /// <remarks> Get the named view as binary content. </remarks>
        public async Task<Stream> GetAttachmentAsync(string attachmentId, string viewId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(attachmentId))
            {
                throw new ArgumentNullException(nameof(attachmentId));
            }
            if (string.IsNullOrEmpty(viewId))
            {
                throw new ArgumentNullException(nameof(viewId));
            }

            using var telemetryScope = new ScopeGetAttachment(attachmentId, viewId);

            // Special case: requires Accept: application/octet-stream (not the default application/json)
            // and returns binary content, not a JSON-deserializable object.
            var requestUri = new Uri(_transport.Endpoint.EnsureTrailingSlash(), string.Format(RestApiPaths.AttachmentView, HttpUtility.UrlEncode(attachmentId), HttpUtility.UrlEncode(viewId)));
            using var message = new HttpRequestMessage(HttpMethod.Get, requestUri);
            message.Headers.Add("Accept", "application/octet-stream, application/json");

            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    {
                        var memoryStream = new MemoryStream();
#if !NETSTANDARD
                        httpResponse.Content.ReadAsStream(cancellationToken).CopyTo(memoryStream);
#else
                        (await httpResponse.Content.ReadAsStreamAsync()).CopyTo(memoryStream);
#endif
                        memoryStream.Seek(0, SeekOrigin.Begin);
                        return memoryStream;
                    }
                case 301:
                case 302:
                    return null;
                default:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.GetAttachmentError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
            }
        }
    }
}
