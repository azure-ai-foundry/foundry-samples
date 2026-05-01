// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using Microsoft.Agents.Connector.Types;
using System;
using Microsoft.Agents.Authentication;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// Routes Channel responses to the Adapter incoming pipeline.  This is the same route an Agent normally gets incoming Activities.
    /// </summary>
    /// <remarks>
    /// The default method an Agent responds with is Activity.DeliverMode == `normal`.  This is an asynchronous response via an
    /// HTTP POST to the endpoints defined by ChannelApiController.
    /// 
    /// <see cref="Microsoft.Agents.Builder.IChannelApiHandler"/> is all of the Connector API endpoints.  This implementation is just handling
    /// the Send/Reply from the other Agent.
    ///
    /// This implementation will send a custom Event to the Adapter, and the AgentApplication can add a route for
    /// <see cref="AdapterChannelResponseHandler.ChannelReplyEventName"/>.  The Event Activity.Value will be an instance of <see cref="Microsoft.Agents.Client.AdapterChannelResponseHandler.ChannelReply"/>.
    /// </remarks>
    /// <remarks>
    /// This implementation does not handle any of the other Connector API endpoints.
    /// </remarks>
    internal class AdapterChannelResponseHandler : IChannelApiHandler
    {
        public class ChannelReply
        {
            public ChannelConversationReference ChannelConversationReference { get; set; }
            public IActivity Activity { get; set; }
        }

        public const string ChannelReplyEventName = "application/vnd.microsoft.agents.ChannelReply";

        private readonly IChannelAdapter _adapter;
        private readonly IAgent _agent;
        private readonly IAgentHost _channelHost;
        private readonly ILogger<AdapterChannelResponseHandler> _logger;

        public AdapterChannelResponseHandler(IChannelAdapter adapter, IAgent agent, IAgentHost channelHost, ILogger<AdapterChannelResponseHandler> logger)
        {
            _adapter = adapter;
            _agent = agent;
            _channelHost = channelHost;
            _logger = logger;
        }

        public async Task<ResourceResponse> OnSendToConversationAsync(ClaimsIdentity claimsIdentity, string conversationId, IActivity activity, CancellationToken cancellationToken = default)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("ChannelApiResponse: ConversationId={ConversationId}, Activity='{Activity}'", activity.Conversation.Id, ProtocolJsonSerializer.ToJson(activity));
            }

            var conversationReference = await _channelHost.GetConversationReferenceAsync(conversationId, cancellationToken);
            if (conversationReference == null)
            {
                // Received a conversationId that isn't known.
                var sanitizedConversationId = conversationId.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "");
                _logger.LogWarning("Received unknown request for an unknown conversation '{AgentConversationId}' from '{AgentAppId}'", sanitizedConversationId, claimsIdentity.GetIncomingAudience());
                throw new KeyNotFoundException(sanitizedConversationId);
            }

            // Need to get this over to the calling Agents identity.  We will do it by packaging it in a custom event
            // and the AgentApplication will need to route to a handler.  See AgentResponsesExtension.OnAgentReply.
            var eventActivity = new Activity()
            {
                Type = ActivityTypes.Event,
                Name = ChannelReplyEventName,
                Value = new ChannelReply() { ChannelConversationReference = conversationReference, Activity = activity },
            };
            eventActivity.ApplyConversationReference(conversationReference.ConversationReference, isIncoming: true);

            // We can't use the incoming ClaimsIdentity to send to the Adapter.
            // Perhaps a better way to do this, but what ChannelServiceAdapterBase does in ContinueConversation.
            var hostClaimsIdentity = AgentClaims.AllowAnonymous(claimsIdentity) 
                ? new ClaimsIdentity()
                : new ClaimsIdentity(
                [
                    new(AuthenticationConstants.AudienceClaim, _channelHost.HostClientId),
                    new(AuthenticationConstants.AppIdClaim, _channelHost.HostClientId),
                ]);

            await _adapter.ProcessActivityAsync(hostClaimsIdentity, eventActivity, _agent.OnTurnAsync, cancellationToken);

            // This implementation isn't keeping track of Activities, so just make up an ActivityId.
            return new ResourceResponse() { Id = Guid.NewGuid().ToString() };
        }

        public Task<ResourceResponse> OnReplyToActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, IActivity activity, CancellationToken cancellationToken = default)
        {
            return OnSendToConversationAsync(claimsIdentity, conversationId, activity, cancellationToken);
        }

        [ExcludeFromCodeCoverage]
        public Task<ResourceResponse> OnUpdateActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, IActivity activity, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        [ExcludeFromCodeCoverage]
        public Task OnDeleteActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        [ExcludeFromCodeCoverage]
        public Task<IList<ChannelAccount>> OnGetActivityMembersAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        [ExcludeFromCodeCoverage]
        public Task<ConversationResourceResponse> OnCreateConversationAsync(ClaimsIdentity claimsIdentity, ConversationParameters parameters, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        [ExcludeFromCodeCoverage]
        public Task<ConversationsResult> OnGetConversationsAsync(ClaimsIdentity claimsIdentity, string conversationId, string continuationToken = null, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        [ExcludeFromCodeCoverage]
        public Task<IList<ChannelAccount>> OnGetConversationMembersAsync(ClaimsIdentity claimsIdentity, string conversationId, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        [ExcludeFromCodeCoverage]
        public Task<ChannelAccount> OnGetConversationMemberAsync(ClaimsIdentity claimsIdentity, string userId, string conversationId, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        [ExcludeFromCodeCoverage]
        public Task<PagedMembersResult> OnGetConversationPagedMembersAsync(ClaimsIdentity claimsIdentity, string conversationId, int? pageSize = null, string continuationToken = null, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        [ExcludeFromCodeCoverage]
        public Task OnDeleteConversationMemberAsync(ClaimsIdentity claimsIdentity, string conversationId, string memberId, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        [ExcludeFromCodeCoverage]
        public Task<ResourceResponse> OnSendConversationHistoryAsync(ClaimsIdentity claimsIdentity, string conversationId, Transcript transcript, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        [ExcludeFromCodeCoverage]
        public Task<ResourceResponse> OnUploadAttachmentAsync(ClaimsIdentity claimsIdentity, string conversationId, AttachmentData attachmentUpload, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }
    }
}
