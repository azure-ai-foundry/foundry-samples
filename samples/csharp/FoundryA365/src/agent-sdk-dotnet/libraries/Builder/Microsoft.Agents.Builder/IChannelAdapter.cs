// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Represents an Adapter that can connect an Agent to a service endpoint.
    /// </summary>
    /// <remarks>The Adapter encapsulates processing a received Activity, creates an
    /// <see cref="ITurnContext"/> and calls <see cref="IAgent.OnTurnAsync(ITurnContext, CancellationToken)"/>. 
    /// When your Agent receives an activity, response are sent to the caller via <see cref="ITurnContext.SendActivityAsync(IActivity, CancellationToken)"/>.
    /// </remarks>
    /// <seealso cref="Microsoft.Agents.Builder.ITurnContext"/>
    /// <seealso cref="Microsoft.Agents.Core.Models.IActivity"/>
    /// <seealso cref="Microsoft.Agents.Builder.IAgent"/>
    public interface IChannelAdapter
    {
        /// <summary>
        /// Gets or sets an error handler that can catch exceptions in the middleware or application.
        /// </summary>
        /// <value>An error handler that can catch exceptions in the middleware or application.</value>
        Func<ITurnContext, Exception, Task> OnTurnError { get; set; }

        /// <summary>
        /// Gets the collection of middleware in the Adapter's pipeline.
        /// </summary>
        /// <value>The middleware collection for the pipeline.</value>
        public IMiddlewareSet MiddlewareSet { get; }

        /// <summary>
        /// Adds middleware to the adapter's pipeline.
        /// </summary>
        /// <param name="middleware">The middleware to add.</param>
        /// <returns>The updated IChannelAdapter object.</returns>
        /// <remarks>Middleware is added to the adapter at initialization time.
        /// For each turn, the adapter calls middleware in the order in which you added it.
        /// </remarks>
        IChannelAdapter Use(IMiddleware middleware);

        /// <summary>
        /// Creates a conversation on the specified channel and executes a turn with the proper context for the new conversation.
        /// </summary>
        /// <param name="agentAppId">The application ID of the Agent. For example, <c>AgentClaims.GetAppId(ITurnContext.Identity)"</c></param>
        /// <param name="channelId">The ID for the channel. See <see cref="Channels"/></param>
        /// <param name="serviceUrl">The channel's service URL endpoint.</param>
        /// <param name="audience">The audience for the connector. For example, <c>AgentClaims.GetTokenAudience(ITurnContext.Identity)</c></param>
        /// <param name="conversationParameters">The conversation information used to create the conversation.</param>
        /// <param name="callback">The method to call for the resulting Agent turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <remarks>To start a conversation, your Agent must know its account information
        /// and the user's account information on that channel.
        /// Most channels only support initiating a direct message (non-group) conversation.
        /// <para>The adapter attempts to create a new conversation on the channel, and
        /// then sends a <c>ActivityEventNames.CreateConversation</c> Event Activity through its pipeline
        /// to the <paramref name="callback"/> method.</para>
        /// <para>If the conversation is established with the
        /// specified users, the ID of the activity's <see cref="Activity.Conversation"/>
        /// will contain the ID of the new conversation.</para>
        /// </remarks>
        Task CreateConversationAsync(string agentAppId, string channelId, string serviceUrl, string audience, ConversationParameters conversationParameters, AgentCallbackHandler callback, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a conversation on the specified channel and executes a turn with the proper context for the new conversation.
        /// </summary>
        /// <param name="identity">The identity of the user or bot initiating the conversation. Cannot be null and should at least contains the 'aud' claim if the desired app.</param>
        /// <param name="channelId">The ID for the channel. See <see cref="Channels"/></param>
        /// <param name="serviceUrl">The channel's service URL endpoint.</param>
        /// <param name="parameters">The parameters used to configure the conversation, such as members, topic name, and activity. Cannot be null.</param>
        /// <param name="audience">The audience for the connector. For example, <c>AgentClaims.GetTokenAudience(ITurnContext.Identity)</c></param>
        /// <param name="callback">A callback handler that is invoked to process activities within the conversation. If null a ContinueConversation is not performed.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation of creating the conversation with the new ConversationReference.</returns>
        Task<ConversationReference> CreateConversationAsync(ClaimsIdentity identity, string channelId, string serviceUrl, string audience, ConversationParameters parameters, AgentCallbackHandler callback, CancellationToken cancellationToken = default);

        /// <summary>
        /// Continues a conversation in a new Turn.  This is typically used for proactive interactions.
        /// </summary>
        /// <param name="agentId">The application ID of the Agent.</param>
        /// <param name="reference">A reference to the conversation to continue.</param>
        /// <param name="callback">The method to call for the resulting Agent turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <remarks>
        /// <para>This is a convenience wrapper for <see cref="ProcessProactiveAsync(ClaimsIdentity, IActivity, string, AgentCallbackHandler, CancellationToken)"/>.</para>
        /// <para>Using this overload will only work against Azure Bot Service and Agentic.  For proactive to other agents (including Copilot Studio), use 
        /// <see cref="ContinueConversationAsync(ClaimsIdentity, ConversationReference, AgentCallbackHandler, CancellationToken)"/>.  Use <see cref="Microsoft.Agents.Authentication.AgentClaims.CreateIdentity(string, bool, string)"/>
        /// to create a ClaimsIdentity with both the audience (your agents ClientId) and appId (the other agents ClientId).</para>
        /// </remarks>
        [Obsolete("Use ContinueConversationAsync(ClaimsIdentity, ConversationReference, AgentCallbackHandler, CancellationToken)")]
        Task ContinueConversationAsync(string agentId, ConversationReference reference, AgentCallbackHandler callback, CancellationToken cancellationToken = default);

        /// <summary>
        /// Continues a conversation in a new Turn.  This is typically used for proactive interactions.
        /// </summary>
        /// <param name="claimsIdentity">A <see cref="ClaimsIdentity"/> for the conversation.</param>
        /// <param name="reference">A reference to the conversation to continue.</param>
        /// <param name="callback">The method to call for the resulting Agent turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <remarks>
        /// <para>This is a convenience wrapper for <see cref="ProcessProactiveAsync(ClaimsIdentity, IActivity, string, AgentCallbackHandler, CancellationToken)"/>.</para>
        /// </remarks>
        Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, ConversationReference reference, AgentCallbackHandler callback, CancellationToken cancellationToken = default);

        /// <summary>
        /// Continues a conversation in a new Turn.  This is typically used for proactive interactions.
        /// </summary>
        /// <param name="agentId">The application ID of the Agent.</param>
        /// <param name="continuationActivity">An <see cref="Activity"/> with the appropriate <see cref="ConversationReference"/> with which to continue the conversation.</param>
        /// <param name="callback">The method to call for the resulting Agent turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <remarks>
        /// <para>This is a convenience wrapper for <see cref="ProcessProactiveAsync(ClaimsIdentity, IActivity, string, AgentCallbackHandler, CancellationToken)"/>.</para>
        /// <para>Using this overload will only work against Azure Bot Service and Agentic.  For proactive to other agents (including Copilot Studio), use 
        /// <see cref="ContinueConversationAsync(ClaimsIdentity, IActivity, AgentCallbackHandler, CancellationToken)"/>.  Use <see cref="Microsoft.Agents.Authentication.AgentClaims.CreateIdentity(string, bool, string)"/>
        /// to create a ClaimsIdentity with both the audience (your agents ClientId) and appId (the other agents ClientId).</para>
        /// </remarks>
        [Obsolete("Use ContinueConversationAsync(ClaimsIdentity, IActivity, AgentCallbackHandler, CancellationToken)")]
        Task ContinueConversationAsync(string agentId, IActivity continuationActivity, AgentCallbackHandler callback, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a proactive message to a conversation.  See <see cref="ProcessProactiveAsync(ClaimsIdentity, IActivity, string, AgentCallbackHandler, CancellationToken)"/>.
        /// </summary>
        /// <param name="claimsIdentity">A <see cref="ClaimsIdentity"/> for the conversation.</param>
        /// <param name="continuationActivity">An <see cref="Activity"/> with the appropriate <see cref="ConversationReference"/> with which to continue the conversation.</param>
        /// <param name="callback">The method to call for the resulting Agent turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <remarks>
        /// <para>This is a convenience wrapper for <see cref="ProcessProactiveAsync(ClaimsIdentity, IActivity, string, AgentCallbackHandler, CancellationToken)"/>.</para>
        /// </remarks>
        [Obsolete("This method will be removed in future versions of the SDK")]
        Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, AgentCallbackHandler callback, CancellationToken cancellationToken = default);

        /// <summary>
        /// Continues a conversation in a new Turn.  This is typically used for proactive interactions.
        /// </summary>
        /// <param name="claimsIdentity">A <see cref="ClaimsIdentity"/> for the conversation.</param>
        /// <param name="reference">A reference to the conversation to continue.</param>
        /// <param name="audience">A value signifying the recipient of the proactive message.  This overrides the default use of ClaimsIdentity for the audience.</param>
        /// <param name="callback">The method to call for the resulting Agent turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <remarks>
        /// <para>This is a convenience wrapper for <see cref="ProcessProactiveAsync(ClaimsIdentity, IActivity, string, AgentCallbackHandler, CancellationToken)"/>.</para>
        /// </remarks>
        Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, ConversationReference reference, string audience, AgentCallbackHandler callback, CancellationToken cancellationToken = default);

        /// <summary>
        /// Continues a conversation in a new Turn.  This is typically used for proactive interactions.
        /// </summary>
        /// <param name="claimsIdentity">A <see cref="ClaimsIdentity"/> for the conversation.</param>
        /// <param name="continuationActivity">An <see cref="Activity"/> with the appropriate <see cref="ConversationReference"/> with which to continue the conversation.</param>
        /// <param name="audience">A value signifying the recipient of the proactive message.</param>
        /// <param name="callback">The method to call for the resulting Agent turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <remarks>
        /// <para>This is a convenience wrapper for <see cref="ProcessProactiveAsync(ClaimsIdentity, IActivity, string, AgentCallbackHandler, CancellationToken)"/>.</para>
        /// </remarks>
        [Obsolete("This method will be removed in future versions of the SDK. Use ProcessProactiveAsync instead.")]
        Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, string audience, AgentCallbackHandler callback, CancellationToken cancellationToken = default);

        /// <summary>
        /// When overridden in a derived class, replaces an existing activity in the
        /// conversation.
        /// </summary>
        /// <param name="turnContext">The context object for the turn.</param>
        /// <param name="activity">New replacement activity.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>If the activity is successfully sent, the task result contains
        /// a <see cref="ResourceResponse"/> object containing the ID that the receiving
        /// channel assigned to the activity.
        /// <para>Before calling this, set the ID of the replacement activity to the ID
        /// of the activity to replace.</para></returns>
        /// <seealso cref="Microsoft.Agents.Builder.ITurnContext.OnUpdateActivity(Microsoft.Agents.Builder.UpdateActivityHandler)"/>
        Task<ResourceResponse> UpdateActivityAsync(ITurnContext turnContext, IActivity activity, CancellationToken cancellationToken);

        /// <summary>
        /// When overridden in a derived class, deletes an existing activity in the
        /// conversation.
        /// </summary>
        /// <param name="turnContext">The context object for the turn.</param>
        /// <param name="reference">Conversation reference for the activity to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <remarks>The <see cref="ConversationReference.ActivityId"/> of the conversation
        /// reference identifies the activity to delete.</remarks>
        /// <seealso cref="Microsoft.Agents.Builder.ITurnContext.OnDeleteActivity(Microsoft.Agents.Builder.DeleteActivityHandler)"/>
        Task DeleteActivityAsync(ITurnContext turnContext, ConversationReference reference, CancellationToken cancellationToken);

        /// <summary>
        /// Creates a turn context and runs the middleware pipeline for an incoming TRUSTED activity.
        /// </summary>
        /// <param name="claimsIdentity">A <see cref="ClaimsIdentity"/> for the request.</param>
        /// <param name="activity">The incoming activity.</param>
        /// <param name="callback">The code to run at the end of the adapter's middleware pipeline.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>If an Invoke Activity was received, an <see cref="InvokeResponse"/>, otherwise null.</returns>
        Task<InvokeResponse> ProcessActivityAsync(ClaimsIdentity claimsIdentity, IActivity activity, AgentCallbackHandler callback, CancellationToken cancellationToken);

        /// <summary>
        /// Executes a new turn pipeline in the context of the conversation of an Activity.
        /// </summary>
        /// <param name="claimsIdentity">A <see cref="ClaimsIdentity"/> for the conversation.  This should be the Identity needed for the Turn.</param>
        /// <param name="continuationActivity">The continuation <see cref="Activity"/> used to create the <see cref="ITurnContext"/>.  This is not the Activity sent to the conversation.</param>
        /// <param name="audience">The audience for the call.  If null, audience is used from <c>claimsIdentity</c>.</param>
        /// <param name="callback">The method to call for the resulting Agent turn.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// <para>A Turn, and the TurnContext, are in the context of an Activity.  Normally this is from Activities arriving via an endpoint (api/messages) which 
        /// are handled in <see cref="ProcessActivityAsync(ClaimsIdentity, IActivity, AgentCallbackHandler, CancellationToken)"/>.  Proactive is an Agent initiated 
        /// Turn, typically for a different conversation.</para>
        /// <para>The <c>continuationActivity</c> argument is used to "seed" the TurnContext created by the pipeline.  It is not the Activity sent to the conversation.  This
        /// is normally acquired by <see cref="ConversationReference.GetContinuationActivity()"/>.  This Activity becomes <see cref="ITurnContext.Activity"/> within 
        /// <c>AgentCallbackHandler</c>. The <c>GetContinuationActivity</c> methods creates a <c>ContinueConversation</c> Event Activity with <c>Activity.Conversation</c>, <c>Activity.From</c>, 
        /// and <c>Activity.Recipient</c> from the <c>ConversationReference</c>.
        /// </para>
        /// <para>
        /// As for ProcessActivity, the pipeline will call <see cref="AgentCallbackHandler"/> with the expected TurnContext.  Actions are performed in this callback as would
        /// for any other turn, relative to <c>continuationActivity.Conversation</c>.  For example, <see cref="ITurnContext.SendActivityAsync(IActivity, CancellationToken)"/>.  State is supported by loading the desired state
        /// manually.
        /// </para>
        /// <para>For example, from an AgentApplication <c>RouteHandler</c>, sending messages to another conversation would be the following. The variable <c>proactiveReference</c> (below) is a previously saved <c>ConversationReference</c> which is typically
        /// acquired by using <c>ITurnContext.Activity.GetConversationReference()</c> from a previous turn for a conversation.</para>
        /// <code>
        /// Task OnSendProactiveAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        /// {
        ///     await context.Adapter.ProcessProactiveAsync(
        ///         turnContext.Identity,
        ///         proactiveReference.GetContinuationActivity(),
        ///         null,
        ///         async(proactiveContext, proactiveCt) =>
        ///         {
        ///            // Middleware will have been executed
        ///            
        ///            // TurnState must be manually loaded and saved.
        ///            var turnState = Options.TurnStateFactory();
        ///            await turnState.LoadStateAsync(proactiveContext, cancellationToken: proactiveCt);
        ///
        ///            // State is relative to the proactive conversation and user.
        ///            turnState.Conversation.SetValue("lastConvoMessage", context.Activity.Text);
        ///            
        ///            // Perform actions as you would for any other turn.
        ///            await proactiveContext.SendActivityAsync($"Proactive Conversation: {context.Activity.Text}", cancellationToken: proactiveCt);
        ///
        ///            await turnState.SaveStateAsync(proactiveContext, cancellationToken: proactiveCt);
        ///         },
        ///         cancellationToken);
        /// }
        /// </code>
        /// </remarks>
        Task ProcessProactiveAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, string audience, AgentCallbackHandler callback, CancellationToken cancellationToken);

        Task ProcessProactiveAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, IAgent agent, CancellationToken cancellationToken, string audience = null);

        /// <summary>
        /// When overridden in a derived class, sends activities to the conversation.
        /// </summary>
        /// <param name="turnContext">The context object for the turn.</param>
        /// <param name="activities">The activities to send.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>If the activities are successfully sent, the task result contains
        /// an array of <see cref="ResourceResponse"/> objects containing the IDs that
        /// the receiving channel assigned to the activities.</returns>
        /// <seealso cref="Microsoft.Agents.Builder.ITurnContext.OnSendActivities(Microsoft.Agents.Builder.SendActivitiesHandler)"/>
        Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken);
        
        /// <summary>
        /// Channel Adapter Logger
        /// </summary>
        ILogger? Logger { get; set; }
    }
}