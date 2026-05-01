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

namespace StreamingAgent1;

/// <summary>
/// Sample Agent calling another Agent.
/// </summary>
public class StreamingHostAgent : AgentApplication
{
    // This provides access to other Agents.
    private readonly IAgentHost _agentHost;

    // The Agent this sample will communicate with.  This name matches AgentHost:Agents config.
    private const string Agent2Name = "Echo";

    public StreamingHostAgent(AgentApplicationOptions options, IAgentHost agentHost) : base(options)
    {
        _agentHost = agentHost ?? throw new ArgumentNullException(nameof(agentHost));

        RegisterExtension(new AgentResponses(this, _agentHost), (extension) =>
        {
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

    [Route(RouteType = RouteType.Activity, Type = ActivityTypes.Message, Rank = RouteRank.Last)]
    protected async Task OnUserMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var echoConversationId = await _agentHost.GetConversation(turnContext, Agent2Name, cancellationToken);
        if (echoConversationId == null)
        {
            if (!turnContext.Activity.Text.Contains("agent"))
            {
                await turnContext.SendActivityAsync("Say \"agent\" and I'll patch you through", cancellationToken: cancellationToken);
                return;
            }

            // Create the Conversation to use with Agent2.  This same conversationId should be used for all
            // subsequent SendToBot calls.
            await turnContext.SendActivityAsync($"Got it, connecting you to the '{Agent2Name}' Agent...", cancellationToken: cancellationToken);
            echoConversationId = await _agentHost.GetOrCreateConversationAsync(turnContext, Agent2Name, cancellationToken);
        }

        // Send the message to the other Agent, and handle Agent2 replies.
        await foreach (IActivity agentActivity in _agentHost.SendToAgentStreamedAsync(turnContext, Agent2Name, echoConversationId, turnContext.Activity, cancellationToken))
        {
            // Agent2 sends EndOfConversation when "end" was received.
            if (agentActivity.IsType(ActivityTypes.EndOfConversation))
            {
                // Remove the Agent conversation reference since the conversation is over.
                await _agentHost.DeleteConversationAsync(turnContext, echoConversationId, cancellationToken);

                if (agentActivity.Value != null)
                {
                    var resultMessage = $"The '{Agent2Name}' Agent returned:\n\n{ProtocolJsonSerializer.ToJson(agentActivity.Value)}";
                    await turnContext.SendActivityAsync(resultMessage, cancellationToken: cancellationToken);
                }

                // Done with calling the remote Agent.
                await turnContext.SendActivityAsync($"Back in {nameof(StreamingHostAgent)}. Say \"agent\" and I'll patch you through", cancellationToken: cancellationToken);
            }
            else
            {
                // Just repeat message to C2
                await turnContext.SendActivityAsync($"({Agent2Name}) {agentActivity.Text}", cancellationToken: cancellationToken);
            }
        }
    }
}
