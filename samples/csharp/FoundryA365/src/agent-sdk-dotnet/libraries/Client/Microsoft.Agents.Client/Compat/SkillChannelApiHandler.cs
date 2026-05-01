// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Connector.Types;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core;

namespace Microsoft.Agents.Client.Compat
{
    /// <summary>
    /// This IChannelApiHandler is primarily used when calling another Agent using DeliveryModes.Normal, and forwarding most
    /// Agent replies to the originating channel.  This is the legacy behavior for the Root bot in a Bot Framework Skill scenario, including 
    /// for Dialogs SkillDialog.
    /// </summary>
    /// <remarks>
    /// This is provided for compatibility with Dialogs SkillDialog.  It is not intended for use with AgentApplication.
    /// </remarks>
    public class SkillChannelApiHandler : IChannelApiHandler
    {
        public static readonly string SkillConversationReferenceKey = $"{nameof(SkillChannelApiHandler)}.SkillConversationReference";

        private readonly IChannelAdapter _adapter;
        private readonly IAgent _agent;
        private readonly IAgentHost _agentHost;
        private readonly ILogger _logger;

        public SkillChannelApiHandler(
            IChannelAdapter adapter,
            IAgent agent,
            IAgentHost channelHost,
            ILogger logger = null)
        {
            AssertionHelpers.ThrowIfNull(adapter, nameof(adapter));
            AssertionHelpers.ThrowIfNull(agent, nameof(agent));
            AssertionHelpers.ThrowIfNull(channelHost, nameof(channelHost));

            _agent = agent;
            _adapter = adapter;
            _agentHost = channelHost;
            _logger = logger ?? NullLogger.Instance;
        }

        //
        // IChannelResponseHandler
        //

        public async Task<ResourceResponse> OnSendActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, IActivity activity, CancellationToken cancellationToken = default)
        {
            return await ProcessActivityAsync(claimsIdentity, conversationId, null, activity, cancellationToken).ConfigureAwait(false);
        }


        //
        // IChannelApiHandler
        //

        public async Task<ResourceResponse> OnSendToConversationAsync(ClaimsIdentity claimsIdentity, string conversationId, IActivity activity, CancellationToken cancellationToken = default)
        {
            return await ProcessActivityAsync(claimsIdentity, conversationId, null, activity, cancellationToken).ConfigureAwait(false);
        }

        public async Task<ResourceResponse> OnReplyToActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, IActivity activity, CancellationToken cancellationToken = default)
        {
            return await ProcessActivityAsync(claimsIdentity, conversationId, activityId, activity, cancellationToken).ConfigureAwait(false);
        }

        public async Task OnDeleteActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, CancellationToken cancellationToken = default)
        {
            var skillConversationReference = await GetSkillConversationReferenceAsync(conversationId, cancellationToken).ConfigureAwait(false);

            var callback = new AgentCallbackHandler(async (turnContext, ct) =>
            {
                turnContext.StackState.Set(SkillConversationReferenceKey, skillConversationReference);
                await turnContext.DeleteActivityAsync(activityId, cancellationToken).ConfigureAwait(false);
            });

            // We can't use the incoming ClaimsIdentity to send to the Adapter.
            var hostClaimsIdentity = AgentClaims.CreateIdentity(_agentHost.HostClientId, AgentClaims.AllowAnonymous(claimsIdentity));

            await _adapter.ContinueConversationAsync(hostClaimsIdentity, skillConversationReference.ConversationReference, skillConversationReference.OAuthScope, callback, cancellationToken).ConfigureAwait(false);
        }

        public async Task<ResourceResponse> OnUpdateActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, IActivity activity, CancellationToken cancellationToken = default)
        {
            var skillConversationReference = await GetSkillConversationReferenceAsync(conversationId, cancellationToken).ConfigureAwait(false);

            ResourceResponse resourceResponse = null;
            var callback = new AgentCallbackHandler(async (turnContext, ct) =>
            {
                turnContext.StackState.Set(SkillConversationReferenceKey, skillConversationReference);
                activity.ApplyConversationReference(skillConversationReference.ConversationReference);
                turnContext.Activity.Id = activityId;
                turnContext.Activity.CallerId = $"{CallerIdConstants.AgentPrefix}{AgentClaims.GetOutgoingAppIdClaim(claimsIdentity)}";
                resourceResponse = await turnContext.UpdateActivityAsync(activity, cancellationToken).ConfigureAwait(false);
            });

            // We can't use the incoming ClaimsIdentity to send to the Adapter.
            var hostClaimsIdentity = AgentClaims.CreateIdentity(_agentHost.HostClientId, AgentClaims.AllowAnonymous(claimsIdentity));

            await _adapter.ContinueConversationAsync(hostClaimsIdentity, skillConversationReference.ConversationReference, skillConversationReference.OAuthScope, callback, cancellationToken).ConfigureAwait(false);

            return resourceResponse ?? new ResourceResponse(Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        }

        public async Task<ChannelAccount> OnGetMemberAsync(ClaimsIdentity claimsIdentity, string userId, string conversationId, CancellationToken cancellationToken = default)
        {
            var skillConversationReference = await GetSkillConversationReferenceAsync(conversationId, cancellationToken).ConfigureAwait(false);
            ChannelAccount member = null;

            var callback = new AgentCallbackHandler(async (turnContext, ct) =>
            {
                var client = turnContext.Services.Get<IConnectorClient>();
                var conversationId = turnContext.Activity.Conversation.Id;
                member = await client.Conversations.GetConversationMemberAsync(userId, conversationId, cancellationToken).ConfigureAwait(false);
            });

            // We can't use the incoming ClaimsIdentity to send to the Adapter.
            var hostClaimsIdentity = AgentClaims.CreateIdentity(_agentHost.HostClientId, AgentClaims.AllowAnonymous(claimsIdentity));

            await _adapter.ContinueConversationAsync(hostClaimsIdentity, skillConversationReference.ConversationReference, skillConversationReference.OAuthScope, callback, cancellationToken).ConfigureAwait(false);

            return member;
        }

        public Task<IList<ChannelAccount>> OnGetActivityMembersAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ConversationResourceResponse> OnCreateConversationAsync(ClaimsIdentity claimsIdentity, ConversationParameters parameters, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ConversationsResult> OnGetConversationsAsync(ClaimsIdentity claimsIdentity, string conversationId, string continuationToken = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IList<ChannelAccount>> OnGetConversationMembersAsync(ClaimsIdentity claimsIdentity, string conversationId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task<ChannelAccount> OnGetConversationMemberAsync(ClaimsIdentity claimsIdentity, string userId, string conversationId, CancellationToken cancellationToken = default)
        {
            var skillConversationReference = await GetSkillConversationReferenceAsync(conversationId, cancellationToken).ConfigureAwait(false);
            ChannelAccount member = null;

            var callback = new AgentCallbackHandler(async (turnContext, ct) =>
            {
                var client = turnContext.Services.Get<IConnectorClient>();
                var conversationId = turnContext.Activity.Conversation.Id;
                member = await client.Conversations.GetConversationMemberAsync(userId, conversationId, cancellationToken).ConfigureAwait(false);
            });

            // We can't use the incoming ClaimsIdentity to send to the Adapter.
            var hostClaimsIdentity = AgentClaims.CreateIdentity(_agentHost.HostClientId, AgentClaims.AllowAnonymous(claimsIdentity));

            await _adapter.ContinueConversationAsync(hostClaimsIdentity, skillConversationReference.ConversationReference, skillConversationReference.OAuthScope, callback, cancellationToken).ConfigureAwait(false);

            return member;
        }

        public Task<PagedMembersResult> OnGetConversationPagedMembersAsync(ClaimsIdentity claimsIdentity, string conversationId, int? pageSize = null, string continuationToken = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task OnDeleteConversationMemberAsync(ClaimsIdentity claimsIdentity, string conversationId, string memberId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ResourceResponse> OnSendConversationHistoryAsync(ClaimsIdentity claimsIdentity, string conversationId, Transcript transcript, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ResourceResponse> OnUploadAttachmentAsync(ClaimsIdentity claimsIdentity, string conversationId, AttachmentData attachmentUpload, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        private static void ApplySkillActivityToTurnContext(ITurnContext turnContext, IActivity activity)
        {
            // adapter.ContinueConversation() sends an event activity with ContinueConversation in the name.
            // this warms up the incoming middlewares but once that's done and we hit the custom callback,
            // we need to swap the values back to the ones received from the skill so the bot gets the actual activity.
            turnContext.Activity.ChannelData = activity.ChannelData;
            turnContext.Activity.Code = activity.Code;
            turnContext.Activity.Entities = activity.Entities;
            turnContext.Activity.Locale = activity.Locale;
            turnContext.Activity.LocalTimestamp = activity.LocalTimestamp;
            turnContext.Activity.Name = activity.Name;
            turnContext.Activity.Properties = activity.Properties;
            turnContext.Activity.RelatesTo = activity.RelatesTo;
            turnContext.Activity.ReplyToId = activity.ReplyToId;
            turnContext.Activity.Timestamp = activity.Timestamp;
            turnContext.Activity.Text = activity.Text;
            turnContext.Activity.Type = activity.Type;
            turnContext.Activity.Value = activity.Value;
        }

        private async Task<ChannelConversationReference> GetSkillConversationReferenceAsync(string conversationId, CancellationToken cancellationToken)
        {
            var conversationReference = await _agentHost.GetConversationReferenceAsync(conversationId, cancellationToken).ConfigureAwait(false);
            if (conversationReference == null)
            {
                var sanitizedConversationId = conversationId.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "");
                _logger.LogError("Unable to get conversation reference for conversationId {SanitizedConversationId}.", sanitizedConversationId);
                throw new KeyNotFoundException();
            }

            return conversationReference;
        }

        private async Task<ResourceResponse> ProcessActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, string replyToActivityId, IActivity activity, CancellationToken cancellationToken)
        {
            var skillConversationReference = await GetSkillConversationReferenceAsync(conversationId, cancellationToken).ConfigureAwait(false);

            ResourceResponse resourceResponse = null;

            var callback = new AgentCallbackHandler(async (turnContext, ct) =>
            {
                turnContext.StackState.Set(SkillConversationReferenceKey, skillConversationReference);
                activity.ApplyConversationReference(skillConversationReference.ConversationReference);
                turnContext.Activity.Id = replyToActivityId;
                turnContext.Activity.CallerId = $"{CallerIdConstants.AgentPrefix}{AgentClaims.GetOutgoingAppIdClaim(claimsIdentity)}";
                switch (activity.Type)
                {
                    case ActivityTypes.EndOfConversation:
                        await _agentHost.DeleteConversationAsync(turnContext, conversationId, cancellationToken).ConfigureAwait(false);
                        await SendToAgentAsync(activity, turnContext, ct).ConfigureAwait(false);
                        break;
                    case ActivityTypes.Event:
                        await SendToAgentAsync(activity, turnContext, ct).ConfigureAwait(false);
                        break;
                    case ActivityTypes.Command:
                    case ActivityTypes.CommandResult:
                        if (activity.Name.StartsWith("application/", StringComparison.Ordinal))
                        {
                            // Send to channel and capture the resource response for the SendActivityCall so we can return it.
                            resourceResponse = await turnContext.SendActivityAsync(activity, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            await SendToAgentAsync(activity, turnContext, ct).ConfigureAwait(false);
                        }

                        break;

                    default:
                        // Capture the resource response for the SendActivityCall so we can return it.
                        resourceResponse = await turnContext.SendActivityAsync(activity, cancellationToken).ConfigureAwait(false);
                        break;
                }
            });

            // We can't use the incoming ClaimsIdentity to send to the Adapter.
            var hostClaimsIdentity = AgentClaims.CreateIdentity(_agentHost.HostClientId, AgentClaims.AllowAnonymous(claimsIdentity));

            await _adapter.ContinueConversationAsync(hostClaimsIdentity, skillConversationReference.ConversationReference, skillConversationReference.OAuthScope, callback, cancellationToken).ConfigureAwait(false);

            return resourceResponse ?? new ResourceResponse(Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        }

        private async Task SendToAgentAsync(IActivity activity, ITurnContext turnContext, CancellationToken ct)
        {
            ApplySkillActivityToTurnContext(turnContext, activity);
            await _agent.OnTurnAsync(turnContext, ct).ConfigureAwait(false);
        }
    }
}
