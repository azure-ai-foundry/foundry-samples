// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Client;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;

namespace Agent1;

/// <summary>
/// Sample Agent calling another Agent.
/// </summary>
public class HostAgent : AgentApplication
{
    // This provides access to other Agents.
    private readonly IAgentHost _agentHost;
    
    // The Agent this sample will communicate with.  This name matches AgentHost:Agents config.
    private const string Agent2Name = "Echo";

    public HostAgent(AgentApplicationOptions options, IAgentHost agentHost) : base(options)
    {
        _agentHost = agentHost ?? throw new ArgumentNullException(nameof(agentHost));

        // Register extension to handle communicating with other Agents.
        RegisterExtension(new AgentResponses(this, _agentHost), (extension) =>
        {
            // Handle DeliveryMode.Normal Agent asynchronous replies.
            extension.OnAgentReply(OnAgentResponseAsync);

            extension.AddDefaultEndOfConversationHandling();
        });
    }

    [Route(RouteType = RouteType.Conversation, EventName = ConversationUpdateEvents.MembersAdded)]
    protected async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync("Say \"agent\" and I'll patch you through", cancellationToken: cancellationToken);
            }
        }
    }

    // Handles messages sent by the user.
    [Route(RouteType = RouteType.Activity, Type = ActivityTypes.Message, Rank = RouteRank.Last)]
    protected async Task OnUserMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var echoConversationId = await _agentHost.GetConversation(turnContext, Agent2Name, cancellationToken);
        if (echoConversationId == null)
        {
            if (!turnContext.Activity.Text.Contains("agent"))
            {
                // Respond with instructions
                await turnContext.SendActivityAsync("Say \"agent\" and I'll patch you through", cancellationToken: cancellationToken);
                return;
            }

            // Create the Conversation to use with Agent2.  This same conversationId should be used for all
            // subsequent SendToAgent calls until the conversation is over.
            await turnContext.SendActivityAsync($"Got it, connecting you to the '{Agent2Name}' Agent...", cancellationToken: cancellationToken);
            echoConversationId = await _agentHost.GetOrCreateConversationAsync(turnContext, Agent2Name, cancellationToken);
        }

        // Send whatever the user said to Agent2.
        await _agentHost.SendToAgent(turnContext, Agent2Name, echoConversationId, turnContext.Activity, cancellationToken);
    }

    // Handles responses from Agent2.
    protected async Task OnAgentResponseAsync(ITurnContext turnContext, ITurnState turnState, ChannelConversationReference reference, IActivity agentActivity, CancellationToken cancellationToken)
    {
        // Agents can send an EndOfConversation Activity.  This Activity can optionally contain a result value.
        if (agentActivity.IsType(ActivityTypes.EndOfConversation))
        {
            // Agent2 signaled that the conversation is over.  Remove the conversation because we're done with it.
            await _agentHost.DeleteConversationAsync(turnContext, reference.AgentConversationId, cancellationToken);

            // In this sample, the Agent2 will send an EndOfConversation with a result when "end" is sent.
            if (agentActivity.Value != null)
            {
                var resultMessage = $"The '{reference.AgentName}' Agent returned:\n\n{ProtocolJsonSerializer.ToJson(agentActivity.Value)}";
                await turnContext.SendActivityAsync(resultMessage, cancellationToken: cancellationToken);
            }

            // Done with calling Agent2.
            await turnContext.SendActivityAsync($"Back in {nameof(HostAgent)}. Say \"agent\" and I'll patch you through", cancellationToken: cancellationToken);
        }
        else
        {
            // Forward whatever Agent2 sent to the User until EndOfConversation is received.
            // Note that the agentActivity is for a different conversation.  It cannot be sent directly to ABS
            // without modification.  Here we are just extracting values we need.
            await turnContext.SendActivityAsync($"({reference.AgentName}) {agentActivity.Text}", cancellationToken: cancellationToken);
        }
    }
}
