// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Proactive;

public class ProactiveAgent : AgentApplication
{
    public ProactiveAgent(AgentApplicationOptions options) : base(options)
    {
        // Manual way to store a conversation for use in Proactive.  This is for sample purposes only.
        OnMessage("-s", async (turnContext, turnState, cancellationToken) =>
        {
            var id = await Proactive.StoreConversationAsync(turnContext, cancellationToken);
            await turnContext.SendActivityAsync($"Conversation '{id}' stored", cancellationToken: cancellationToken);
        });

        OnMessage("-signin", async (turnContext, turnState, cancellationToken) =>
        {
            await turnContext.SendActivityAsync("Signed in", cancellationToken: cancellationToken);
        }, autoSignInHandlers: ["me"]);

        OnMessage("-signout", async (turnContext, turnState, cancellationToken) =>
        {
            await UserAuthorization.SignOutUserAsync(turnContext, turnState, "me", cancellationToken);
            await turnContext.SendActivityAsync("Signed out", cancellationToken: cancellationToken);
        });

        // In-code ContinueConversation.  Send "-s" first to store the conversation.
        OnMessage(new Regex("-c.*"), async (turnContext, turnState, cancellationToken) =>
        {
            var split = turnContext.Activity.Text.Split(' ');
            var conversationId = split.Length == 1 ? turnContext.Activity.Conversation.Id : split[1];

            try
            {
                // Since OnContinueConversationAsync has the [ContinueConversation] attribute this
                // will automatically pick up the specified token handlers. 
                await Proactive.ContinueConversationAsync(turnContext.Adapter, conversationId, OnContinueConversationAsync, cancellationToken: cancellationToken);
            }
            catch (UserNotSignedIn)
            {
                await turnContext.SendActivityAsync($"Send '-signin' first", cancellationToken: cancellationToken);
            }
        });
    }

    [Route(RouteType = RouteType.Conversation, EventName = ConversationUpdateEvents.MembersAdded)]
    public async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Hello and Welcome!"), cancellationToken);
            }
        }
    }

    [Route(Type = ActivityTypes.Message, Rank = RouteRank.Last)]
    public async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // This demonstrates using a Conversation instance to perform ContinueConversation with a custom 
        // continuation activity.
        // This does the same as:  await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}"),
        // except using ContinueConversation.
        // ConversationBuilder can also be used to manually create a Conversation instance manually.
        var conversation = new Conversation(turnContext);

        var customContinuation = conversation.Reference.GetContinuationActivity();
        customContinuation.Value = turnContext.Activity;

        await Proactive.ContinueConversationAsync(
            turnContext.Adapter, 
            conversation, 
            async (context, state, ct) =>
            {
                var originalActivity = (IActivity)context.Activity.Value;
                await context.SendActivityAsync($"You said: {originalActivity.Text}", cancellationToken: ct);
            },
            continuationActivity: customContinuation,
            cancellationToken: cancellationToken);
    }

    // This attribute indicates this is a ContinueConversation handler.
    // It can be used in a code-first approach using Proactive.ContinueConversationAsync, or if MapAgentProactiveEndpoints was called in
    // startup it can be mapped to an Http request to /proactive/continue that triggers this logic.
    // Either way the tokens will be provided from the indicated token handlers. This will fail if the user is not signed into "me".
    [ContinueConversation(autoSignInHandlers: "me")]
    public async Task OnContinueConversationAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var token = await turnContext.GetTurnTokenAsync(cancellationToken: cancellationToken);
        await turnContext.SendActivityAsync($"This is OnContinueConversation. Token={(token == null ? "not signed in" : token.Length)}", cancellationToken: cancellationToken);
    }

    // There can be ContinueConversation handlers for differet scenarios.  In this case, if enabled, the Http request /proactive/continue/ext is mapped to this method.
    [ContinueConversation("ext")]
    public Task OnContinueConversationExtendedAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        return turnContext.SendActivityAsync($"This is ContinueConversationExtended", cancellationToken: cancellationToken);
    }
}
