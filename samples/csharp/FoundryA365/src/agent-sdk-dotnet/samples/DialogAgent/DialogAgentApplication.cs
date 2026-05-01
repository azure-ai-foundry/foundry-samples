// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Agents.Builder.Dialogs;
using Microsoft.Agents.Core.Serialization;

namespace DialogAgent;

public class DialogAgentApplication : AgentApplication
{
    public DialogAgentApplication(AgentApplicationOptions options) : base(options)
    {
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
        OnMessage("-dialog", OnStartDialogAsync);
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Hello and Welcome!"), cancellationToken);
            }
        }
    }

    private async Task OnStartDialogAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var activeDialog = turnState.User.GetValue<string>("ActiveDialog");
        if (!string.IsNullOrEmpty(activeDialog))
        {
            await turnContext.SendActivityAsync($"Dialog '{activeDialog}' already active", cancellationToken: cancellationToken);
            return;
        }

        turnState.User.SetValue("ActiveDialog", nameof(UserProfileDialog));
        var dialog = new UserProfileDialog(turnState);
        var dialogResult = await dialog.RunAsync(turnContext, turnState.Conversation, cancellationToken);
    }

    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var activeDialog = turnState.User.GetValue<string>("ActiveDialog");
        if (string.IsNullOrEmpty(activeDialog))
        {
            await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
        }
        else
        {
            var dialog = new UserProfileDialog(turnState);
            var dialogResult = await dialog.RunAsync(turnContext, turnState.Conversation, cancellationToken);
            if (   dialogResult?.Status == DialogTurnStatus.Complete
                || dialogResult?.Status == DialogTurnStatus.CompleteAndWait
                || dialogResult?.Status == DialogTurnStatus.Cancelled)
            {
                turnState.User.DeleteValue("ActiveDialog");
                await turnContext.SendActivityAsync($"Dialog '{activeDialog}' complete.\nResult={ProtocolJsonSerializer.ToJson(dialogResult.Result)}", cancellationToken: cancellationToken);
            }
        }
    }
}
