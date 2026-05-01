// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Connector.Errors;
using Microsoft.Agents.Connector.RestClients;
using Microsoft.Agents.Connector.Types;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Connector
{
    /// <summary>
    /// This is an IConnectorClient suited for communicating with Microsoft Copilot Studio via a Power Apps Connector request.
    /// </summary>
    /// <remarks>
    /// The only supported operations are <c>IConversations.SendToConversationAsync</c> and <c>IConversations.ReplyToActivityAsync</c>.
    /// </remarks>
    public class MCSConnectorClient : RestClientBase, IConnectorClient
    {
        public IAttachments Attachments { get; }

        public IConversations Conversations { get; }

        public Uri BaseUri => base.Endpoint;

        public MCSConnectorClient(Uri endpoint, IHttpClientFactory httpClientFactory, string namedClient = nameof(MCSConnectorClient))
            : base(endpoint, httpClientFactory, namedClient, null)
        {
            AssertionHelpers.ThrowIfNull(endpoint, nameof(endpoint));
            AssertionHelpers.ThrowIfNull(httpClientFactory, nameof(httpClientFactory));

            Conversations = new MCSConversations(this);
            Attachments = new AttachmentsRestClient(this);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }

    class MCSConversations(IRestTransport transport) : IConversations
    {
        public Task<ConversationResourceResponse> CreateConversationAsync(ConversationParameters parameters, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task DeleteActivityAsync(string conversationId, string activityId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task DeleteConversationMemberAsync(string conversationId, string memberId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<ChannelAccount>> GetActivityMembersAsync(string conversationId, string activityId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ChannelAccount> GetConversationMemberAsync(string userId, string conversationId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<ChannelAccount>> GetConversationMembersAsync(string conversationId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<PagedMembersResult> GetConversationPagedMembersAsync(string conversationId, int? pageSize = null, string continuationToken = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ConversationsResult> GetConversationsAsync(string continuationToken = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ResourceResponse> SendConversationHistoryAsync(string conversationId, Transcript transcript, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ResourceResponse> ReplyToActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            return SendToConversationAsync(activity, cancellationToken);
        }

        public async Task<ResourceResponse> SendToConversationAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));

            using var message = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = transport.Endpoint
            };
            message.Headers.Add("Accept", "application/json");
            message.Content = new StringContent(ProtocolJsonSerializer.ToJson(activity), System.Text.Encoding.UTF8, "application/json");

            using var httpClient = await transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 201:
                case 202:
                    {
#if !NETSTANDARD
                        if (httpResponse.Content.ReadAsStream(cancellationToken).Length == 0)
                        {
                            return new ResourceResponse() { Id = string.Empty };
                        }
                        return ProtocolJsonSerializer.ToObject<ResourceResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
#else
                        if (httpResponse.Content.ReadAsStringAsync().Result.Length == 0)
                        {
                            return new ResourceResponse() { Id = string.Empty };
                        }
                        return ProtocolJsonSerializer.ToObject<ResourceResponse>(httpResponse.Content.ReadAsStringAsync().Result);
#endif
                    }
                default:
                    {
                        throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.SendSendConversationError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
                    }
            }
        }

        public Task<ResourceResponse> UpdateActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ResourceResponse> UploadAttachmentAsync(string conversationId, AttachmentData attachmentUpload, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    class Attachments : IAttachments
    {
        public Task<Stream> GetAttachmentAsync(string attachmentId, string viewId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<AttachmentInfo> GetAttachmentInfoAsync(string attachmentId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public string GetAttachmentUri(string attachmentId, string viewId = "original")
        {
            throw new NotImplementedException();
        }
    }
}
