// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// Represents a host that contains a collection of Agents for Agent-to-Agent communication.
    /// </summary>
    public interface IAgentHost
    {
        /// <summary>
        /// The endpoint to use in Activity.ServiceUrl if unspecified in an Agents settings.
        /// </summary>
        Uri DefaultResponseEndpoint { get; set; }

        string HostClientId { get; set; }

        /// <summary>
        /// Gets a list of configured Agents.
        /// </summary>
        IList<IAgentInfo> GetAgents();

        /// <summary>
        /// Gets the Agent Client used to send Activities to another Agent.
        /// </summary>
        /// <param name="agentName">The name of the Agent</param>
        IAgentClient GetClient(string agentName);

        /// <summary>
        /// Sends an activity to an Agent.
        /// </summary>
        /// <remarks>
        /// This is used for Activity.DeliverMode == 'normal'.  In order to get the asynchronous replies from the Agent, the
        /// <see cref="Microsoft.Agents.Client.AgentResponses.OnAgentReply"/> handler must be set on the AgentApplication.
        /// </remarks>
        /// <remarks>
        /// This will not properly handle Invoke or ExpectReplies requests as it doesn't return a value.  Use <see cref="Microsoft.Agents.Client.IAgentHost.GetClient(string)"/> and
        /// use the returned <see cref="Microsoft.Agents.Client.IAgentClient"/> directly for those.
        /// </remarks>
        /// <param name="turnContext">Context for a turn of the Agent.</param>
        /// <param name="agentName">An Agent name from configuration.</param>
        /// <param name="agentConversationId">An Agent conversation identifier. More info: <see cref="Microsoft.Agents.Client.IAgentHost.GetOrCreateConversationAsync"/> or <see cref="Microsoft.Agents.Client.IAgentHost.GetConversation"/></param>
        /// <param name="activity">The activity to send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="System.ArgumentException">If the specified agentName is null or not found.</exception>
        Task SendToAgent(ITurnContext turnContext, string agentName, string agentConversationId, IActivity activity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends an activity to an Agent using an asynchronous stream.
        /// </summary>
        /// <param name="turnContext">Context for a turn of the Agent.</param>
        /// <param name="agentName">An Agent name from configuration.</param>
        /// <param name="agentConversationId">An Agent conversation identifier. More info: <see cref="Microsoft.Agents.Client.IAgentHost.GetOrCreateConversationAsync"/> or <see cref="Microsoft.Agents.Client.IAgentHost.GetConversation"/></param>
        /// <param name="activity">The activity to send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns></returns>
        IAsyncEnumerable<object> SendToAgentStreamedAsync(ITurnContext turnContext, string agentName, string agentConversationId, IActivity activity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the conversationId for an existing conversation for an Agent, relative to the current Turns Conversation.
        /// </summary>
        /// <remarks>
        /// IAgentHost currently only supports a single active conversation per Agent per Turn Conversation.
        /// </remarks>
        /// <param name="turnContext"></param>
        /// <param name="agentName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>conversationId for an existing conversation, or null.</returns>
        Task<string> GetConversation(ITurnContext turnContext, string agentName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns a list of all Agent conversations for the current Turns Conversation.
        /// </summary>
        /// <param name="turnContext"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>Non-null list of <see cref="Microsoft.Agents.Client.AgentConversation"/>.</returns>
        Task<IList<AgentConversation>> GetConversations(ITurnContext turnContext, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the existing conversation for an Agent, or creates a new one.
        /// </summary>
        /// <remarks>
        /// IAgentHost currently only supports a single active conversation per Agent per Turn Conversation.
        /// </remarks>
        /// <param name="turnContext"></param>
        /// <param name="agentName">An Agent name from configuration.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<string> GetOrCreateConversationAsync(ITurnContext turnContext, string agentName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes the indicated conversation.
        /// </summary>
        /// <remarks>
        /// Only the Agent knows when a conversation is done.  All effort should be made to remove conversations as otherwise the persisted conversations accumulate.
        /// A received Activity of type EndOfConversation is one instance where the conversation should be deleted.
        /// </remarks>
        /// <param name="turnContext"></param>
        /// <param name="agentConversationId">A conversationId return from <see cref="Microsoft.Agents.Client.IAgentHost.GetConversation"/> or <see cref="Microsoft.Agents.Client.IAgentHost.GetOrCreateConversationAsync"/>.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task DeleteConversationAsync(ITurnContext turnContext, string agentConversationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Ends the conversation for the specified Agent.
        /// </summary>
        /// <remarks>
        /// This deletes the conversation and sends the Agent an EndOfConversation. 
        /// </remarks>
        /// <param name="turnContext"></param>
        /// <param name="agentName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task EndAgentConversation(ITurnContext turnContext, string agentName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Ends all active conversations for the Turn conversation.
        /// </summary>
        /// <remarks>
        /// This deletes all conversations with other Agents for the current ITurnContext conversation.  This also sends an
        /// EndOfConversations to each Agent.
        /// </remarks>
        /// <param name="turnContext"></param>
        /// <param name="conversationState"></param>
        /// <param name="cancellationToken"></param>
        Task EndAllConversations(ITurnContext turnContext, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the conversation information for the specified Agent conversation.
        /// </summary>
        /// <param name="agentConversationId"></param>
        /// <param name="cancellationToken"></param>
        Task<ChannelConversationReference> GetConversationReferenceAsync(string agentConversationId, CancellationToken cancellationToken = default);
    }
}
