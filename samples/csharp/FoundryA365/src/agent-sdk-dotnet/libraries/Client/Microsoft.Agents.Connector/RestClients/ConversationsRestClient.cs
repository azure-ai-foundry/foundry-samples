// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using Microsoft.Agents.Connector.Errors;
using Microsoft.Agents.Connector.Telemetry.Scopes;
using Microsoft.Agents.Connector.Types;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Microsoft.Agents.Connector.RestClients
{
    internal class ConversationsRestClient(IRestTransport transport) : IConversations
    {
        /// <summary>
        /// Gets the maximum allowed length for an APX conversation ID.
        /// </summary>
        public int MaxApxConversationIdLength { get; set; } = 150;

        private readonly IRestTransport _transport = transport ?? throw new ArgumentNullException(nameof(_transport));

        /// <inheritdoc/>
        public async Task<ConversationsResult> GetConversationsAsync(string continuationToken = null, CancellationToken cancellationToken = default)
        {
            using var telemetryScope = new ScopeGetConversations();
            var request = RestRequest.Get(RestApiPaths.Conversations)
                .WithQuery("continuationToken", continuationToken);

            using var httpResponse = await RestPipeline.SendRawAsync(_transport, request, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    return await RestPipeline.ReadContentAsync<ConversationsResult>(httpResponse, cancellationToken).ConfigureAwait(false);
                default:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.SendGetConversationsError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
            }
        }

        /// <inheritdoc/>
        public async Task<ConversationResourceResponse> CreateConversationAsync(ConversationParameters body = null, CancellationToken cancellationToken = default)
        {
            using var telemetryScope = new ScopeCreateConversation();
            var request = RestRequest.Post(RestApiPaths.Conversations)
                .WithBody(body);

            using var httpResponse = await RestPipeline.SendRawAsync(_transport, request, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 201:
                case 202:
                    return await RestPipeline.ReadContentAsync<ConversationResourceResponse>(httpResponse, cancellationToken).ConfigureAwait(false);
                default:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.SendCreateConversationError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
            }
        }

        public async Task<ResourceResponse> SendToConversationAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            return await SendToConversationAsync(activity.Conversation.Id, activity, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<ResourceResponse> SendToConversationAsync(string conversationId, IActivity body = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(conversationId, nameof(conversationId));

            using var telemetryScope = new ScopeSendToConversation(conversationId, body?.Id);

            var convId = TruncateConversationId(conversationId, body);
            var path = string.Format(RestApiPaths.ConversationActivities, HttpUtility.UrlEncode(convId));
            var request = RestRequest.Post(path).WithBody(body);
            if (body?.ChannelId == Channels.Msteams && body.IsTargetedActivity())
            {
                if (body.Conversation?.IsGroup != true)
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.TeamsTargetedRequiresGroupChat, null);
                }
                request = request.WithQuery("isTargetedActivity", "true");
            }

            using var httpResponse = await RestPipeline.SendRawAsync(_transport, request, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 201:
                case 202:
                    // Teams is famous for not returning a response body for these.
                    var content = await RestPipeline.ReadAsStringAsync(httpResponse, cancellationToken).ConfigureAwait(false);
                    return string.IsNullOrEmpty(content)
                        ? new ResourceResponse() { Id = string.Empty }
                        : ProtocolJsonSerializer.ToObject<ResourceResponse>(content);
                default:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.SendSendConversationError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
            }
        }

        /// <inheritdoc/>
        public async Task<ResourceResponse> SendConversationHistoryAsync(string conversationId, Transcript body = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(conversationId, nameof(conversationId));

            var request = RestRequest.Post(string.Format(RestApiPaths.ConversationHistory, HttpUtility.UrlEncode(conversationId)))
                .WithBody(body);

            using var httpResponse = await RestPipeline.SendRawAsync(_transport, request, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 201:
                case 202:
                    return await RestPipeline.ReadContentAsync<ResourceResponse>(httpResponse, cancellationToken).ConfigureAwait(false);
                default:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.SendConversationHistoryError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
            }
        }

        public async Task<ResourceResponse> UpdateActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            return await UpdateActivityAsync(activity.Conversation.Id, activity.Id, activity, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<ResourceResponse> UpdateActivityAsync(string conversationId, string activityId, IActivity body = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(conversationId, nameof(conversationId));
            AssertionHelpers.ThrowIfNullOrEmpty(activityId, nameof(activityId));

            using var telemetryScope = new ScopeUpdateActivity(conversationId, activityId);

            var request = RestRequest.Put(string.Format(RestApiPaths.ConversationActivity, HttpUtility.UrlEncode(conversationId), HttpUtility.UrlEncode(activityId)))
                .WithBody(body);

            using var httpResponse = await RestPipeline.SendRawAsync(_transport, request, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 201:
                case 202:
                    return await RestPipeline.ReadContentAsync<ResourceResponse>(httpResponse, cancellationToken).ConfigureAwait(false);
                default:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.SendUpdateActivityError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
            }
        }

        public async Task<ResourceResponse> ReplyToActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            return activity == null
                ? throw new ArgumentNullException(nameof(activity))
                : await ReplyToActivityAsync(activity.Conversation.Id, activity.ReplyToId, activity, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<ResourceResponse> ReplyToActivityAsync(string conversationId, string activityId, IActivity body = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(activityId, nameof(activityId));
            AssertionHelpers.ThrowIfNullOrEmpty(conversationId, nameof(conversationId));

            using var telemetryScope = new ScopeReplyToActivity(conversationId, activityId);

            var convId = TruncateConversationId(conversationId, body);
            var path = string.Format(RestApiPaths.ConversationActivity, HttpUtility.UrlEncode(convId), HttpUtility.UrlEncode(activityId));
            var request = RestRequest.Post(path).WithBody(body);
            if (body?.ChannelId == Channels.Msteams && body.IsTargetedActivity())
            {
                if (body.Conversation?.IsGroup != true)
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.TeamsTargetedRequiresGroupChat, null);
                }
                request = request.WithQuery("isTargetedActivity", "true");
            }

            using var httpResponse = await RestPipeline.SendRawAsync(_transport, request, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 201:
                case 202:
                    // Teams is famous for not returning a response body for these.
                    var content = await RestPipeline.ReadAsStringAsync(httpResponse, cancellationToken).ConfigureAwait(false);
                    return string.IsNullOrEmpty(content)
                        ? new ResourceResponse() { Id = string.Empty }
                        : ProtocolJsonSerializer.ToObject<ResourceResponse>(content);
                default:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.SendReplyToActivityError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
            }
        }

        /// <inheritdoc/>
        public async Task DeleteActivityAsync(string conversationId, string activityId, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(conversationId, nameof(conversationId));
            AssertionHelpers.ThrowIfNullOrEmpty(activityId, nameof(activityId));

            using var telemetryScope = new ScopeDeleteActivity(conversationId, activityId);

            var request = RestRequest.Delete(string.Format(RestApiPaths.ConversationActivity, HttpUtility.UrlEncode(conversationId), HttpUtility.UrlEncode(activityId)));

            using var httpResponse = await RestPipeline.SendRawAsync(_transport, request, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 202:
                    return;
                default:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.SendDeleteActivityError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
            }
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ChannelAccount>> GetConversationMembersAsync(string conversationId, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(conversationId, nameof(conversationId));

            using var telemetryScope = new ScopeGetConversationMembers(conversationId);

            var request = RestRequest.Get(string.Format(RestApiPaths.ConversationMembers, HttpUtility.UrlEncode(conversationId)));

            using var httpResponse = await RestPipeline.SendRawAsync(_transport, request, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    return await RestPipeline.ReadContentAsync<IReadOnlyList<ChannelAccount>>(httpResponse, cancellationToken).ConfigureAwait(false);
                default:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.SendGetConversationMembersError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
            }
        }

        /// <inheritdoc/>
        public async Task<ChannelAccount> GetConversationMemberAsync(string userId, string conversationId, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(conversationId, nameof(conversationId));
            AssertionHelpers.ThrowIfNullOrEmpty(userId, nameof(userId));

            using var telemetryScope = new ScopeGetConversationMembers(conversationId);

            var request = RestRequest.Get(string.Format(RestApiPaths.ConversationMember, HttpUtility.UrlEncode(conversationId), HttpUtility.UrlEncode(userId)));

            using var httpResponse = await RestPipeline.SendRawAsync(_transport, request, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    return await RestPipeline.ReadContentAsync<ChannelAccount>(httpResponse, cancellationToken).ConfigureAwait(false);
                default:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.SendGetConversationMemberError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
            }
        }

        /// <inheritdoc/>
        public async Task DeleteConversationMemberAsync(string conversationId, string memberId, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(conversationId, nameof(conversationId));
            AssertionHelpers.ThrowIfNullOrEmpty(memberId, nameof(memberId));

            var request = RestRequest.Delete(string.Format(RestApiPaths.ConversationMember, HttpUtility.UrlEncode(conversationId), HttpUtility.UrlEncode(memberId)));

            using var httpResponse = await RestPipeline.SendRawAsync(_transport, request, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 204:
                    return;
                default:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.SendDeleteConversationMemberError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
            }
        }

        /// <inheritdoc/>
        public async Task<PagedMembersResult> GetConversationPagedMembersAsync(string conversationId, int? pageSize = default, string continuationToken = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(conversationId, nameof(conversationId));

            var request = RestRequest.Get(string.Format(RestApiPaths.ConversationPagedMembers, HttpUtility.UrlEncode(conversationId)))
                .WithQuery("pageSize", pageSize.HasValue ? pageSize.Value.ToString() : null)
                .WithQuery("continuationToken", continuationToken);

            using var httpResponse = await RestPipeline.SendRawAsync(_transport, request, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    return await RestPipeline.ReadContentAsync<PagedMembersResult>(httpResponse, cancellationToken).ConfigureAwait(false);
                default:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.SendGetConversationPagedMembersError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
            }
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ChannelAccount>> GetActivityMembersAsync(string conversationId, string activityId, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(conversationId, nameof(conversationId));
            AssertionHelpers.ThrowIfNullOrEmpty(activityId, nameof(activityId));

            var request = RestRequest.Get(string.Format(RestApiPaths.ActivityMembers, HttpUtility.UrlEncode(conversationId), HttpUtility.UrlEncode(activityId)));

            using var httpResponse = await RestPipeline.SendRawAsync(_transport, request, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    return await RestPipeline.ReadContentAsync<IReadOnlyList<ChannelAccount>>(httpResponse, cancellationToken).ConfigureAwait(false);
                default:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.SendGetActivityMembersError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
            }
        }

        /// <inheritdoc/>
        public async Task<ResourceResponse> UploadAttachmentAsync(string conversationId, AttachmentData body = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(conversationId, nameof(conversationId));

            using var telemetryScope = new ScopeUploadAttachment(conversationId);

            var request = RestRequest.Post(string.Format(RestApiPaths.ConversationAttachments, HttpUtility.UrlEncode(conversationId)))
                .WithBody(body);

            using var httpResponse = await RestPipeline.SendRawAsync(_transport, request, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                case 201:
                case 202:
                    return await RestPipeline.ReadContentAsync<ResourceResponse>(httpResponse, cancellationToken).ConfigureAwait(false);
                default:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.SendUploadAttachmentError, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
            }
        }

        private string TruncateConversationId(string conversationId, IActivity body)
        {
            string convId;

            // Truncate conversationId for Teams and Agentic roles to MaxApxConversationIdLength characters
            if ((body?.ChannelId?.Channel == Channels.Msteams ||
                body?.ChannelId?.Channel == Channels.Agents)
                && (body?.From?.Role == RoleTypes.AgenticIdentity
                || body?.From?.Role == RoleTypes.AgenticUser))
            {
                convId = conversationId.Length > MaxApxConversationIdLength ? conversationId[..MaxApxConversationIdLength] : conversationId;
            }
            else
                convId = conversationId;

            return convId;
        }
    }
}
