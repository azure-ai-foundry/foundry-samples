// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Models;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Extensions.Teams.App.Meetings;
using Microsoft.Agents.Extensions.Teams.App.MessageExtensions;
using Microsoft.Agents.Extensions.Teams.App.TaskModules;
using Microsoft.Agents.Core;
using Microsoft.Agents.Extensions.Teams.App.Builders;

namespace Microsoft.Agents.Extensions.Teams.App
{
    /// <summary>
    /// AgentExtension for Microsoft Teams.
    /// </summary>
    public class TeamsAgentExtension : AgentExtension
    {
        private static readonly string CONFIG_FETCH_INVOKE_NAME = "config/fetch";
        private static readonly string CONFIG_SUBMIT_INVOKE_NAME = "config/submit";

        /// <summary>
        /// Creates a new TeamsAgentExtension instance.
        /// To leverage this extension, call <see cref="Microsoft.Agents.Builder.App.AgentApplication.RegisterExtension{TExtension}(TExtension, System.Action{TExtension})"/> with an instance of this class.
        /// Use the callback method to register routes for handling Teams-specific events.
        /// </summary>
        /// <param name="agentApplication">The agent application to leverage for route registration.</param>
        /// <param name="options">Options for configuring TaskModules.</param>
        public TeamsAgentExtension(AgentApplication agentApplication, TaskModulesOptions? options = null)
        {
            ChannelId = Channels.Msteams;

            AgentApplication = agentApplication;

            Meetings = new Meeting(agentApplication);
            MessageExtensions = new MessageExtension(agentApplication);
            TaskModules = new TaskModule(agentApplication, options);

            Options = options;
        }

        public TaskModulesOptions Options { get; }

        /// <summary>
        /// Fluent interface for accessing Meetings' specific features.
        /// </summary>
        public Meeting Meetings { get; }

        /// <summary>
        /// Fluent interface for accessing Message Extensions' specific features.
        /// </summary>
        public MessageExtension MessageExtensions { get; }

        /// <summary>
        /// Fluent interface for accessing Task Modules' specific features.
        /// </summary>
        public TaskModule TaskModules { get; }

#if !NETSTANDARD
        protected AgentApplication AgentApplication { get; init;}
#else
        protected AgentApplication AgentApplication { get; set;}
#endif

        /// <summary>
        /// Handles conversation update events.
        /// </summary>
        /// <param name="conversationUpdateEvent">Name of the conversation update event to handle, can use <see cref="Microsoft.Agents.Builder.App.ConversationUpdateEvents"/>.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The AgentExtension instance for chaining purposes.</returns>
        public TeamsAgentExtension OnConversationUpdate(string conversationUpdateEvent, RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AddRoute(AgentApplication, TeamsConversationUpdateRouteBuilder.Create()
                .AsAgentic(isAgenticOnly)
                .WithUpdateEvent(conversationUpdateEvent)
                .WithChannelId(ChannelId)
                .WithOrderRank(rank)
                .WithOAuthHandlers(autoSignInHandlers)
                .WithHandler(handler)
                .Build());

            return this;
        }

        /// <summary>
        /// Handles message edit events.
        /// </summary>
        /// <param name="handler">Function to call when the event is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The AgentExtension instance for chaining purposes.</returns>
        public TeamsAgentExtension OnMessageEdit(RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = (turnContext, cancellationToken) =>
            {
                TeamsChannelData teamsChannelData;
                return Task.FromResult(
                    string.Equals(turnContext.Activity.Type, ActivityTypes.MessageUpdate, StringComparison.OrdinalIgnoreCase)
                    && (teamsChannelData = turnContext.Activity.GetChannelData<TeamsChannelData>()) != null
                    && string.Equals(teamsChannelData.EventType, "editMessage"));
            };
            AddRoute(AgentApplication, routeSelector, handler, false, isAgenticOnly, rank, autoSignInHandlers);
            return this;
        }

        /// <summary>
        /// Handles message undo soft delete events.
        /// </summary>
        /// <param name="handler">Function to call when the event is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The AgentExtension instance for chaining purposes.</returns>
        public TeamsAgentExtension OnMessageUndelete(RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = (turnContext, cancellationToken) =>
            {
                TeamsChannelData teamsChannelData;
                return Task.FromResult(
                    string.Equals(turnContext.Activity.Type, ActivityTypes.MessageUpdate, StringComparison.OrdinalIgnoreCase)
                    && (teamsChannelData = turnContext.Activity.GetChannelData<TeamsChannelData>()) != null
                    && string.Equals(teamsChannelData.EventType, "undeleteMessage"));
            };
            AddRoute(AgentApplication, routeSelector, handler, false, isAgenticOnly, rank, autoSignInHandlers);
            return this;
        }

        /// <summary>
        /// Handles message soft delete events.
        /// </summary>
        /// <param name="handler">Function to call when the event is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The AgentExtension instance for chaining purposes.</returns>
        public TeamsAgentExtension OnMessageDelete(RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = (turnContext, cancellationToken) =>
            {
                TeamsChannelData teamsChannelData;
                return Task.FromResult(
                    string.Equals(turnContext.Activity.Type, ActivityTypes.MessageDelete, StringComparison.OrdinalIgnoreCase)
                    && (teamsChannelData = turnContext.Activity.GetChannelData<TeamsChannelData>()) != null
                    && string.Equals(teamsChannelData.EventType, "softDeleteMessage"));
            };
            AddRoute(AgentApplication, routeSelector, handler, false, isAgenticOnly, rank, autoSignInHandlers);
            return this;
        }

        /// <summary>
        /// Handles read receipt events for messages sent by the bot in personal scope.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The AgentExtension instance for chaining purposes.</returns>
        public TeamsAgentExtension OnTeamsReadReceipt(ReadReceiptHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = (context, _) => Task.FromResult
            (
                string.Equals(context.Activity?.Type, ActivityTypes.Event, StringComparison.OrdinalIgnoreCase)
                && string.Equals(context.Activity?.Name, "application/vnd.microsoft.readReceipt")
            );
            RouteHandler routeHandler = async (ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) =>
            {
                ReadReceiptInfo readReceiptInfo = ProtocolJsonSerializer.ToObject<ReadReceiptInfo>(turnContext.Activity.Value) ?? new();
                await handler(turnContext, turnState, readReceiptInfo, cancellationToken);
            };
            AddRoute(AgentApplication, routeSelector, routeHandler, false, isAgenticOnly, rank, autoSignInHandlers);
            return this;
        }

        /// <summary>
        /// Handles config fetch events for Microsoft Teams.
        /// </summary>
        /// <param name="handler">Function to call when the event is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The AgentExtension instance for chaining purposes.</returns>
        public TeamsAgentExtension OnConfigFetch(ConfigHandlerAsync handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = (turnContext, cancellationToken) => Task.FromResult(
                string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                && string.Equals(turnContext.Activity.Name, CONFIG_FETCH_INVOKE_NAME));
            RouteHandler routeHandler = async (ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) =>
            {
                ConfigResponseBase result = await handler(turnContext, turnState, turnContext.Activity.Value, cancellationToken);

                // Check to see if an invoke response has already been added
                if (!turnContext.StackState.Has(ChannelAdapter.InvokeResponseKey))
                {
                    var activity = Activity.CreateInvokeResponseActivity(result);
                    await turnContext.SendActivityAsync(activity, cancellationToken);
                }
            };
            AddRoute(AgentApplication, routeSelector, routeHandler, true, isAgenticOnly, rank, autoSignInHandlers);
            return this;
        }

        /// <summary>
        /// Handles config submit events for Microsoft Teams.
        /// </summary>
        /// <param name="handler">Function to call when the event is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The AgentExtension instance for chaining purposes.</returns>
        public TeamsAgentExtension OnConfigSubmit(ConfigHandlerAsync handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = (turnContext, cancellationToken) => Task.FromResult(
                string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                && string.Equals(turnContext.Activity.Name, CONFIG_SUBMIT_INVOKE_NAME));
            RouteHandler routeHandler = async (ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) =>
            {
                ConfigResponseBase result = await handler(turnContext, turnState, turnContext.Activity.Value, cancellationToken);

                // Check to see if an invoke response has already been added
                if (!turnContext.StackState.Has(ChannelAdapter.InvokeResponseKey))
                {
                    var activity = Activity.CreateInvokeResponseActivity(result);
                    await turnContext.SendActivityAsync(activity, cancellationToken);
                }
            };
            AddRoute(AgentApplication, routeSelector, routeHandler, true, isAgenticOnly, rank, autoSignInHandlers);
            return this;
        }

        /// <summary>
        /// Handles when a file consent card is accepted by the user.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The AgentExtension instance for chaining purposes.</returns>
        public TeamsAgentExtension OnFileConsentAccept(FileConsentHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
            => OnFileConsent(handler, "accept", rank, autoSignInHandlers, isAgenticOnly);

        /// <summary>
        /// Handles when a file consent card is declined by the user.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The AgentExtension instance for chaining purposes.</returns>
        public TeamsAgentExtension OnFileConsentDecline(FileConsentHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
            => OnFileConsent(handler, "decline", rank, autoSignInHandlers, isAgenticOnly);

        private TeamsAgentExtension OnFileConsent(FileConsentHandler handler, string fileConsentAction, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = (context, _) =>
            {
                FileConsentCardResponse? fileConsentCardResponse;
                return Task.FromResult
                (
                    string.Equals(context.Activity?.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(context.Activity?.Name, "fileConsent/invoke")
                    && (fileConsentCardResponse = ProtocolJsonSerializer.ToObject<FileConsentCardResponse>(context.Activity!.Value)) != null
                    && string.Equals(fileConsentCardResponse.Action, fileConsentAction)
                );
            };
            RouteHandler routeHandler = async (turnContext, turnState, cancellationToken) =>
            {
                FileConsentCardResponse fileConsentCardResponse = ProtocolJsonSerializer.ToObject<FileConsentCardResponse>(turnContext.Activity.Value) ?? new();
                await handler(turnContext, turnState, fileConsentCardResponse, cancellationToken);

                // Check to see if an invoke response has already been added
                if (!turnContext.StackState.Has(ChannelAdapter.InvokeResponseKey))
                {
                    var activity = Activity.CreateInvokeResponseActivity();
                    await turnContext.SendActivityAsync(activity, cancellationToken);
                }
            };
            AddRoute(AgentApplication, routeSelector, routeHandler, true, isAgenticOnly, rank, autoSignInHandlers);
            return this;
        }

        /// <summary>
        /// Handles O365 Connector Card Action activities.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The AgentExtension instance for chaining purposes.</returns>
        public AgentApplication OnO365ConnectorCardAction(O365ConnectorCardActionHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = (context, _) => Task.FromResult
            (
                string.Equals(context.Activity?.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                && string.Equals(context.Activity?.Name, "actionableMessage/executeAction")
            );
            RouteHandler routeHandler = async (turnContext, turnState, cancellationToken) =>
            {
                O365ConnectorCardActionQuery query = ProtocolJsonSerializer.ToObject<O365ConnectorCardActionQuery>(turnContext.Activity.Value) ?? new();
                await handler(turnContext, turnState, query, cancellationToken);

                // Check to see if an invoke response has already been added
                if (!turnContext.StackState.Has(ChannelAdapter.InvokeResponseKey))
                {
                    var activity = Activity.CreateInvokeResponseActivity();
                    await turnContext.SendActivityAsync(activity, cancellationToken);
                }
            };
            AddRoute(AgentApplication, routeSelector, routeHandler, true, isAgenticOnly, rank, autoSignInHandlers);
            return AgentApplication;
        }

        /// <summary>
        /// Registers a handler for feedback loop events when a user clicks the thumbsup or thumbsdown button on a response sent from the AI module.
        /// <see cref="Microsoft.Agents.Extensions.Teams.AI.AIOptions{TState}.EnableFeedbackLoop"/> must be set to true.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns></returns>
        [Obsolete("Use AgentApplication.OnFeedbackLoop instead")]
        public TeamsAgentExtension OnFeedbackLoop(FeedbackLoopHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            // This is for back-compat with existing usage.  We need to convert from Core FeedbackData to existing Teams Extension FeedbackLoopData.
            async Task coreHandler(ITurnContext turnContext, ITurnState turnState, Builder.App.FeedbackData coreFeedbackData, CancellationToken cancellationToken)
            {
                var teamsFeedbackLoopData = ProtocolJsonSerializer.ToObject<FeedbackLoopData>(coreFeedbackData);
                await handler(turnContext, turnState, teamsFeedbackLoopData, cancellationToken).ConfigureAwait(false);
            }

            // Use Core route handling for this.
            AgentApplication.OnFeedbackLoop(coreHandler, rank, autoSignInHandlers, isAgenticOnly);
            return this;
        }
    }
}
