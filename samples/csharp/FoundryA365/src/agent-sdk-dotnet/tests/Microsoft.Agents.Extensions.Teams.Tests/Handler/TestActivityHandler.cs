// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.Compat;
using Microsoft.Agents.Extensions.Teams.Models;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.Tests
{
    internal class TestActivityHandler : TeamsActivityHandler
    {

        public List<string> Record { get; } = [];

        // ConversationUpdate
        protected override Task OnConversationUpdateActivityAsync(ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnConversationUpdateActivityAsync(turnContext, cancellationToken);
        }

        protected override Task OnTeamsChannelCreatedAsync(ChannelInfo channelInfo, TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsChannelCreatedAsync(channelInfo, teamInfo, turnContext, cancellationToken);
        }

        protected override Task OnTeamsChannelDeletedAsync(ChannelInfo channelInfo, TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsChannelDeletedAsync(channelInfo, teamInfo, turnContext, cancellationToken);
        }

        protected override Task OnTeamsChannelRenamedAsync(ChannelInfo channelInfo, TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsChannelRenamedAsync(channelInfo, teamInfo, turnContext, cancellationToken);
        }

        protected override Task OnTeamsChannelRestoredAsync(ChannelInfo channelInfo, TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsChannelRestoredAsync(channelInfo, teamInfo, turnContext, cancellationToken);
        }

        protected override Task OnTeamsTeamArchivedAsync(TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsTeamArchivedAsync(teamInfo, turnContext, cancellationToken);
        }

        protected override Task OnTeamsTeamDeletedAsync(TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsTeamDeletedAsync(teamInfo, turnContext, cancellationToken);
        }

        protected override Task OnTeamsTeamHardDeletedAsync(TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsTeamHardDeletedAsync(teamInfo, turnContext, cancellationToken);
        }

        protected override Task OnTeamsTeamRenamedAsync(TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsTeamRenamedAsync(teamInfo, turnContext, cancellationToken);
        }

        protected override Task OnTeamsTeamRestoredAsync(TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsTeamRestoredAsync(teamInfo, turnContext, cancellationToken);
        }

        protected override Task OnTeamsTeamUnarchivedAsync(TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsTeamUnarchivedAsync(teamInfo, turnContext, cancellationToken);
        }

        protected override Task OnTeamsMembersAddedAsync(IList<TeamsChannelAccount> membersAdded, TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.CompletedTask;
        }

        protected override Task OnTeamsMembersRemovedAsync(IList<TeamsChannelAccount> membersRemoved, TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.CompletedTask;
        }

        protected override Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.CompletedTask;
        }

        protected override Task OnMembersRemovedAsync(IList<ChannelAccount> membersRemoved, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.CompletedTask;
        }

        protected override Task OnTeamsReadReceiptAsync(ReadReceiptInfo readReceiptInfo, ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            turnContext.SendActivityAsync(readReceiptInfo.LastReadMessageId);
            return Task.CompletedTask;
        }

        protected override Task OnTeamsMeetingParticipantsJoinAsync(MeetingParticipantsEventDetails meeting, ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            turnContext.SendActivityAsync(meeting.Members[0].User.Id);
            return base.OnTeamsMeetingParticipantsJoinAsync(meeting, turnContext, cancellationToken);
        }

        protected override Task OnTeamsMeetingParticipantsLeaveAsync(MeetingParticipantsEventDetails meeting, ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            turnContext.SendActivityAsync(meeting.Members[0].User.Id);
            return base.OnTeamsMeetingParticipantsLeaveAsync(meeting, turnContext, cancellationToken);
        }

        // Invoke
        protected override Task<InvokeResponse> OnInvokeActivityAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnInvokeActivityAsync(turnContext, cancellationToken);
        }

        protected override Task<InvokeResponse> OnTeamsFileConsentAsync(ITurnContext<IInvokeActivity> turnContext, FileConsentCardResponse fileConsentCardResponse, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsFileConsentAsync(turnContext, fileConsentCardResponse, cancellationToken);
        }

        protected override Task OnTeamsFileConsentAcceptAsync(ITurnContext<IInvokeActivity> turnContext, FileConsentCardResponse fileConsentCardResponse, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.CompletedTask;
        }

        protected override Task OnTeamsFileConsentDeclineAsync(ITurnContext<IInvokeActivity> turnContext, FileConsentCardResponse fileConsentCardResponse, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.CompletedTask;
        }

        protected override Task OnTeamsO365ConnectorCardActionAsync(ITurnContext<IInvokeActivity> turnContext, O365ConnectorCardActionQuery query, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.CompletedTask;
        }

        protected override Task<MessagingExtensionActionResponse> OnTeamsMessagingExtensionAgentMessagePreviewEditAsync(ITurnContext<IInvokeActivity> turnContext, MessagingExtensionAction action, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(new MessagingExtensionActionResponse());
        }

        protected override Task<MessagingExtensionActionResponse> OnTeamsMessagingExtensionAgentMessagePreviewSendAsync(ITurnContext<IInvokeActivity> turnContext, MessagingExtensionAction action, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(new MessagingExtensionActionResponse());
        }

        protected override Task OnTeamsMessagingExtensionCardButtonClickedAsync(ITurnContext<IInvokeActivity> turnContext, JsonElement cardData, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsMessagingExtensionCardButtonClickedAsync(turnContext, cardData, cancellationToken);
        }

        protected override Task<MessagingExtensionActionResponse> OnTeamsMessagingExtensionFetchTaskAsync(ITurnContext<IInvokeActivity> turnContext, MessagingExtensionAction action, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(new MessagingExtensionActionResponse());
        }

        protected override Task<MessagingExtensionResponse> OnTeamsMessagingExtensionConfigurationQuerySettingUrlAsync(ITurnContext<IInvokeActivity> turnContext, MessagingExtensionQuery query, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(new MessagingExtensionResponse());
        }

        protected override Task OnTeamsMessagingExtensionConfigurationSettingAsync(ITurnContext<IInvokeActivity> turnContext, JsonElement settings, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.CompletedTask;
        }

        protected override Task<MessagingExtensionResponse> OnTeamsMessagingExtensionQueryAsync(ITurnContext<IInvokeActivity> turnContext, MessagingExtensionQuery query, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(new MessagingExtensionResponse());
        }

        protected override Task<MessagingExtensionResponse> OnTeamsMessagingExtensionSelectItemAsync(ITurnContext<IInvokeActivity> turnContext, JsonElement query, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(new MessagingExtensionResponse());
        }

        protected override Task<MessagingExtensionActionResponse> OnTeamsMessagingExtensionSubmitActionAsync(ITurnContext<IInvokeActivity> turnContext, MessagingExtensionAction action, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(new MessagingExtensionActionResponse());
        }

        protected override Task<MessagingExtensionActionResponse> OnTeamsMessagingExtensionSubmitActionDispatchAsync(ITurnContext<IInvokeActivity> turnContext, MessagingExtensionAction action, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsMessagingExtensionSubmitActionDispatchAsync(turnContext, action, cancellationToken);
        }

        protected override Task<MessagingExtensionResponse> OnTeamsAppBasedLinkQueryAsync(ITurnContext<IInvokeActivity> turnContext, AppBasedLinkQuery query, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(new MessagingExtensionResponse());
        }

        protected override Task<MessagingExtensionResponse> OnTeamsAnonymousAppBasedLinkQueryAsync(ITurnContext<IInvokeActivity> turnContext, AppBasedLinkQuery query, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(new MessagingExtensionResponse());
        }

        protected override Task<InvokeResponse> OnTeamsCardActionInvokeAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsCardActionInvokeAsync(turnContext, cancellationToken);
        }

        protected override Task<TaskModuleResponse> OnTeamsTaskModuleFetchAsync(ITurnContext<IInvokeActivity> turnContext, TaskModuleRequest taskModuleRequest, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(new TaskModuleResponse());
        }

        protected override Task<TaskModuleResponse> OnTeamsTaskModuleSubmitAsync(ITurnContext<IInvokeActivity> turnContext, TaskModuleRequest taskModuleRequest, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(new TaskModuleResponse());
        }

        protected override Task OnTeamsSigninVerifyStateAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.CompletedTask;
        }

        protected override Task<TabResponse> OnTeamsTabFetchAsync(ITurnContext<IInvokeActivity> turnContext, TabRequest taskModuleRequest, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(new TabResponse());
        }

        protected override Task<TabResponse> OnTeamsTabSubmitAsync(ITurnContext<IInvokeActivity> turnContext, TabSubmit taskModuleRequest, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(new TabResponse());
        }

        protected override Task<ConfigResponseBase> OnTeamsConfigFetchAsync(ITurnContext<IInvokeActivity> turnContext, JsonElement configData, CancellationToken cancellationToken)
        {
            ConfigResponseBase configResponse = new ConfigTaskResponse();
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(configResponse);
        }

        protected override Task<ConfigResponseBase> OnTeamsConfigSubmitAsync(ITurnContext<IInvokeActivity> turnContext, JsonElement configData, CancellationToken cancellationToken)
        {
            ConfigResponseBase configResponse = new ConfigTaskResponse();
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return Task.FromResult(configResponse);
        }

        protected override Task OnEventActivityAsync(ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnEventActivityAsync(turnContext, cancellationToken);
        }

        protected override Task OnTeamsMeetingStartAsync(MeetingStartEventDetails meeting, ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            turnContext.SendActivityAsync(meeting.StartTime.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return Task.CompletedTask;
        }

        protected override Task OnTeamsMeetingEndAsync(MeetingEndEventDetails meeting, ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            turnContext.SendActivityAsync(meeting.EndTime.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return Task.CompletedTask;
        }

        protected override Task OnMessageUpdateActivityAsync(ITurnContext<IMessageUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnMessageUpdateActivityAsync(turnContext, cancellationToken);
        }

        protected override Task OnTeamsMessageEditAsync(ITurnContext<IMessageUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsMessageEditAsync(turnContext, cancellationToken);
        }

        protected override Task OnTeamsMessageUndeleteAsync(ITurnContext<IMessageUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsMessageUndeleteAsync(turnContext, cancellationToken);
        }

        protected override Task OnMessageDeleteActivityAsync(ITurnContext<IMessageDeleteActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnMessageDeleteActivityAsync(turnContext, cancellationToken);
        }

        protected override Task OnTeamsMessageSoftDeleteAsync(ITurnContext<IMessageDeleteActivity> turnContext, CancellationToken cancellationToken)
        {
            Record.Add(MethodBase.GetCurrentMethod().Name);
            return base.OnTeamsMessageSoftDeleteAsync(turnContext, cancellationToken);
        }
    }
}
