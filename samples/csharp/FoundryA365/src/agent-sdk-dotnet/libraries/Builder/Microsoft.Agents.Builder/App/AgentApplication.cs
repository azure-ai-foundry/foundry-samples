// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App.AdaptiveCards;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Telemetry.App.Scopes;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    public delegate Task AgentApplicationTurnError(ITurnContext turnContext, ITurnState turnState, Exception exception, CancellationToken cancellationToken);

    /// <summary>
    /// Application class for routing and processing incoming requests.
    /// </summary>
    public partial class AgentApplication : IAgent
    {
        private readonly UserAuthorization _userAuth;

        private readonly RouteList _routes;
        private readonly ConcurrentQueue<TurnEventHandler> _beforeTurn;
        private readonly ConcurrentQueue<TurnEventHandler> _afterTurn;
        private readonly ConcurrentQueue<AgentApplicationTurnError> _turnErrorHandlers;
        
        public List<IAgentExtension> RegisteredExtensions { get; private set; } = new List<IAgentExtension>();

        /// <summary>
        /// Creates a new AgentApplication instance.
        /// </summary>
        /// <param name="options">Optional. Options used to configure the application.</param>
        public AgentApplication(AgentApplicationOptions options)
        {
            AssertionHelpers.ThrowIfNull(options, nameof(options));

            Options = options;

            Logger = options.LoggerFactory?.CreateLogger<AgentApplication>() ?? AgentApplicationOptions.DefaultLoggerFactory.CreateLogger<AgentApplication>();

            if (Options.TurnStateFactory == null)
            {
                // This defaults to a TurnState with TempState only
                Options.TurnStateFactory = () => new TurnState();
            }

            _routes = new RouteList();
            _beforeTurn = new ConcurrentQueue<TurnEventHandler>();
            _afterTurn = new ConcurrentQueue<TurnEventHandler>();
            _turnErrorHandlers = new ConcurrentQueue<AgentApplicationTurnError>();

            // Application Features

            AdaptiveCards = new AdaptiveCard(this);
            Proactive = new Proactive.Proactive(this);

            if (options.UserAuthorization != null)
            {
                _userAuth = new UserAuthorization(this, options.UserAuthorization);
            }

            ApplyRouteAttributes();
        }

        #region Application Features

        /// <summary>
        /// Fluent interface for accessing Adaptive Card specific features.
        /// </summary>
        public AdaptiveCard AdaptiveCards { get; }

        public Proactive.Proactive Proactive { get; }

        /// <summary>
        /// The application's configured options.
        /// </summary>
        public AgentApplicationOptions Options { get; }

        public ILogger Logger { get; private set; }

        /// <summary>
        /// Accessing user authorization features.
        /// </summary>
        public UserAuthorization UserAuthorization
        {
            get
            {
                if (_userAuth == null)
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.UserAuthorizationNotConfigured, null);
                }

                return _userAuth;
            }
        }

        #endregion

        #region Route Handling

        /// <summary>
        /// Adds a new route to the application.
        /// 
        /// Developers won't typically need to call this method directly as it's used internally by all
        /// of the fluent interfaces to register routes for their specific activity types.
        /// 
        /// Routes will be matched in the order they're added to the application. The first selector to
        /// return `true` when an activity is received will have its handler called.
        ///
        /// Invoke-based activities receive special treatment and are matched separately as they typically
        /// have shorter execution timeouts.
        /// </summary>
        /// <param name="selector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="isInvokeRoute">Boolean indicating if the RouteSelector is for an activity that uses "invoke" which require special handling. Defaults to `false`.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication AddRoute(RouteSelector selector, RouteHandler handler, bool isInvokeRoute = false, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(selector, nameof(selector));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            var route = RouteBuilder.Create()
                .AsAgentic(isAgenticOnly)
                .AsInvoke(isInvokeRoute)
                .WithSelector(selector)
                .WithHandler(handler)
                .WithOrderRank(rank)
                .WithOAuthHandlers(autoSignInHandlers)
                .Build();

            _routes.AddRoute(route);

            return this;
        }

        public AgentApplication AddRoute(Route route)
        {
            AssertionHelpers.ThrowIfNull(route, nameof(route));

            _routes.AddRoute(route);

            return this;
        }

        /// <summary>
        /// Handles incoming activities of a given type.
        /// </summary>
        /// <param name="type">Name of the activity type to match.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnActivity(string type, RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            return AddRoute(TypeRouteBuilder.Create()
                .WithType(type)
                .WithHandler(handler)
                .WithOrderRank(rank)
                .WithOAuthHandlers(autoSignInHandlers)
                .AsAgentic(isAgenticOnly)
                .Build()
            );
        }

        /// <summary>
        /// Handles incoming activities of a given type.
        /// </summary>
        /// <param name="typePattern">Regular expression to match against the incoming activity type.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnActivity(Regex typePattern, RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            return AddRoute(
                TypeRouteBuilder.Create()
                    .WithType(typePattern)
                    .WithHandler(handler)
                    .WithOrderRank(rank)
                    .WithOAuthHandlers(autoSignInHandlers)
                    .AsAgentic(isAgenticOnly)
                    .Build()
            );
        }

        /// <summary>
        /// Handles incoming activities of a given type.
        /// </summary>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        [Obsolete("This method is deprecated use AddRoute(Route) or AddRoute(RouteSelector, RouteHandler, bool, ushort, string[], bool) instead.")]
        public AgentApplication OnActivity(RouteSelector routeSelector, RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            return AddRoute(
                RouteBuilder.Create()
                    .WithSelector(routeSelector)
                    .WithHandler(handler)
                    .WithOrderRank(rank)
                    .WithOAuthHandlers(autoSignInHandlers)
                    .AsAgentic(isAgenticOnly)
                    .Build()
            );
        }

        /// <summary>
        /// Handles incoming activities of a given type.
        /// </summary>
        /// <param name="routeSelectors">Combination of String, Regex, and RouteSelector selectors.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        [Obsolete("This method is deprecated use AddRoute(Route) instead.")]
        public AgentApplication OnActivity(MultipleRouteSelector routeSelectors, RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(routeSelectors, nameof(routeSelectors));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            if (routeSelectors.Strings != null)
            {
                foreach (string type in routeSelectors.Strings)
                {
                    OnActivity(type, handler, rank, autoSignInHandlers, isAgenticOnly);
                }
            }
            if (routeSelectors.Regexes != null)
            {
                foreach (Regex typePattern in routeSelectors.Regexes)
                {
                    OnActivity(typePattern, handler, rank, autoSignInHandlers, isAgenticOnly);
                }
            }
            if (routeSelectors.RouteSelectors != null)
            {
                foreach (RouteSelector routeSelector in routeSelectors.RouteSelectors)
                {
                    AddRoute(routeSelector, handler, false, rank, autoSignInHandlers, isAgenticOnly);
                }
            }
            return this;
        }

        /// <summary>
        /// Handles conversation update events.
        /// </summary>
        /// <param name="conversationUpdateEvent">Name of the conversation update event to handle, can use <see cref="Microsoft.Agents.Builder.App.ConversationUpdateEvents"/>.  If </param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public virtual AgentApplication OnConversationUpdate(string conversationUpdateEvent, RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            return AddRoute(
                ConversationUpdateRouteBuilder.Create()
                    .WithUpdateEvent(conversationUpdateEvent)
                    .WithHandler(handler)
                    .WithOrderRank(rank)
                    .WithOAuthHandlers(autoSignInHandlers)
                    .AsAgentic(isAgenticOnly)
                    .Build()
            );
        }

        /// <summary>
        /// Handles conversation update events using a custom selector.
        /// </summary>
        /// <param name="conversationUpdateSelector">This will be used in addition the checking for Activity.Type == ActivityTypes.ConversationUpdate.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        [Obsolete("This method is deprecated use AddRoute(Route) instead.")]
        public virtual AgentApplication OnConversationUpdate(RouteSelector conversationUpdateSelector, RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(conversationUpdateSelector, nameof(conversationUpdateSelector));

            async Task<bool> ensureConversationUpdate(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                return (!isAgenticOnly || AgenticAuthorization.IsAgenticRequest(turnContext)) && turnContext.Activity.IsType(ActivityTypes.ConversationUpdate) && await conversationUpdateSelector(turnContext, cancellationToken);
            }

            return AddRoute(
                RouteBuilder.Create()
                    .WithSelector(ensureConversationUpdate)
                    .WithHandler(handler)
                    .WithOrderRank(rank)
                    .WithOAuthHandlers(autoSignInHandlers)
                    .AsAgentic(isAgenticOnly)
                    .Build()

            );
        }

        /// <summary>
        /// Handles conversation update events.
        /// </summary>
        /// <param name="conversationUpdateEvents">Name of the conversation update events to handle, can use <see cref="Microsoft.Agents.Builder.App.ConversationUpdateEvents"/> as array item.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        [Obsolete("This method is deprecated use OnConversationUpdate(string) instead.")]
        public AgentApplication OnConversationUpdate(string[] conversationUpdateEvents, RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(conversationUpdateEvents, nameof(conversationUpdateEvents));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            foreach (string conversationUpdateEvent in conversationUpdateEvents)
            {
                OnConversationUpdate(conversationUpdateEvent, handler, rank, autoSignInHandlers, isAgenticOnly);
            }
            return this;
        }

        /// <summary>
        /// Handles incoming messages with a given keyword.
        /// <br/>
        /// This method provides a simple way to have an Agent respond anytime a user sends a
        /// message with a specific word or phrase.
        /// <br/>
        /// For example, you can easily clear the current conversation anytime a user sends "/reset":
        /// <br/>
        /// <code>application.OnMessage("/reset", (context, turnState, _) => ...);</code>
        /// </summary>
        /// <param name="text">Substring of the incoming message text.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnMessage(string text, RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            return AddRoute(
                MessageRouteBuilder.Create()
                    .WithText(text)
                    .WithHandler(handler)
                    .WithOrderRank(rank)
                    .WithOAuthHandlers(autoSignInHandlers)
                    .AsAgentic(isAgenticOnly)
                    .Build()
            );
        }

        /// <summary>
        /// Handles incoming messages with a given keyword.
        /// <br/>
        /// This method provides a simple way to have a Agent respond anytime a user sends a
        /// message with a specific word or phrase.
        /// <br/>
        /// For example, you can easily clear the current conversation anytime a user sends "/reset":
        /// <br/>
        /// <code>application.OnMessage(new Regex("reset"), (context, turnState, _) => ...);</code>
        /// </summary>
        /// <param name="textPattern">Regular expression to match against the text of an incoming message.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnMessage(Regex textPattern, RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            return AddRoute(
                MessageRouteBuilder.Create()
                    .WithText(textPattern)
                    .WithHandler(handler)
                    .WithOrderRank(rank)
                    .WithOAuthHandlers(autoSignInHandlers)
                    .AsAgentic(isAgenticOnly)
                    .Build()
            );
        }

        /// <summary>
        /// Handles incoming messages with a given keyword.
        /// <br/>
        /// This method provides a simple way to have a Agent respond anytime a user sends a
        /// message with a specific word or phrase.
        /// </summary>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        [Obsolete("This method is deprecated use AddRoute(Route) instead.")]
        public AgentApplication OnMessage(RouteSelector routeSelector, RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(routeSelector, nameof(routeSelector));

            async Task<bool> ensureMessage(ITurnContext context, CancellationToken cancellationToken)
            {
                return (!isAgenticOnly || AgenticAuthorization.IsAgenticRequest(context)) && context.Activity.IsType(ActivityTypes.Message) && await routeSelector(context, cancellationToken).ConfigureAwait(false);
            }

            return AddRoute(
                RouteBuilder.Create()
                    .WithSelector(ensureMessage)
                    .WithHandler(handler)
                    .WithOrderRank(rank)
                    .WithOAuthHandlers(autoSignInHandlers)
                    .Build()
            );
        }

        /// <summary>
        /// Handles incoming messages with a given keyword.
        /// <br/>
        /// This method provides a simple way to have a Agent respond anytime a user sends a
        /// message with a specific word or phrase.
        /// </summary>
        /// <param name="routeSelectors">Combination of String, Regex, and RouteSelector selectors.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        [Obsolete("This method is deprecated use OnMessage instead.")]
        public AgentApplication OnMessage(MultipleRouteSelector routeSelectors, RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(routeSelectors, nameof(routeSelectors));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            if (routeSelectors.Strings != null)
            {
                foreach (string text in routeSelectors.Strings)
                {
                    OnMessage(text, handler, rank, autoSignInHandlers, isAgenticOnly);
                }
            }
            if (routeSelectors.Regexes != null)
            {
                foreach (Regex textPattern in routeSelectors.Regexes)
                {
                    OnMessage(textPattern, handler, rank, autoSignInHandlers, isAgenticOnly);
                }
            }
            if (routeSelectors.RouteSelectors != null)
            {
                foreach (RouteSelector routeSelector in routeSelectors.RouteSelectors)
                {
                    AddRoute(routeSelector, handler, false, rank: rank, autoSignInHandlers, isAgenticOnly);
                }
            }
            return this;
        }

        /// <summary>
        /// Handles incoming Event with a specific Name.
        /// </summary>
        /// <param name="eventName">Substring of the incoming message text.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnEvent(string eventName, RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            return AddRoute(
                EventRouteBuilder.Create()
                    .WithName(eventName)
                    .WithHandler(handler)
                    .WithOrderRank(rank)
                    .WithOAuthHandlers(autoSignInHandlers)
                    .AsAgentic(isAgenticOnly)
                    .Build()
            );
        }

        /// <summary>
        /// Handles incoming Events matching a Name pattern.
        /// </summary>
        /// <param name="namePattern">Regular expression to match against the text of an incoming message.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnEvent(Regex namePattern, RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            return AddRoute(
                EventRouteBuilder.Create()
                    .WithName(namePattern)
                    .WithHandler(handler)
                    .WithOrderRank(rank)
                    .WithOAuthHandlers(autoSignInHandlers)
                    .AsAgentic(isAgenticOnly)
                    .Build()
            );
        }

        /// <summary>
        /// Handles incoming Events.
        /// </summary>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnEvent(RouteSelector routeSelector, RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            return AddRoute(
                EventRouteBuilder.Create()
                    .WithSelector(routeSelector)
                    .WithHandler(handler)
                    .WithOrderRank(rank)
                    .WithOAuthHandlers(autoSignInHandlers)
                    .Build()
            );
        }

        /// <summary>
        /// Handles message reactions added events.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnMessageReactionsAdded(RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            return AddRoute(MessageReactionsAddedRouteBuilder.Create()
                .WithHandler(handler)
                .WithOrderRank(rank)
                .WithOAuthHandlers(autoSignInHandlers)
                .AsAgentic(isAgenticOnly)
                .Build()
            );
        }

        /// <summary>
        /// Handles message reactions removed events.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnMessageReactionsRemoved(RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            return AddRoute(MessageReactionsRemovedRouteBuilder.Create()
                .WithHandler(handler)
                .WithOrderRank(rank)
                .WithOAuthHandlers(autoSignInHandlers)
                .AsAgentic(isAgenticOnly)
                .Build()
            );
        }

        /// <summary>
        /// Handles handoff activities.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnHandoff(HandoffHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            return AddRoute(
                HandoffRouteBuilder.Create()
                    .WithHandler(handler)
                    .WithOrderRank(rank)
                    .WithOAuthHandlers(autoSignInHandlers)
                    .AsAgentic(isAgenticOnly)
                    .Build()
            );
        }

        /// <summary>
        /// Registers a handler for feedback loop events when a user clicks the thumbsup or thumbsdown button on a response.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnFeedbackLoop(FeedbackLoopHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            return AddRoute(
                FeedbackRouteBuilder.Create()
                    .WithHandler(handler)
                    .WithOrderRank(rank)
                    .WithOAuthHandlers(autoSignInHandlers)
                    .AsAgentic(isAgenticOnly)
                    .Build()
            );
        }


        /// <summary>
        /// Add a handler that will execute before the turn's activity handler logic is processed.
        /// <br/>
        /// Handler returns true to continue execution of the current turn. Handler returning false
        /// prevents the turn from running, but the Agents state is still saved, which lets you
        /// track the reason why the turn was not processed. It also means you can use this as
        /// a way to call into the dialog system. For example, you could use the OAuthPrompt to sign the
        /// user in before allowing the AI system to run.
        /// </summary>
        /// <param name="handler">Function to call before turn execution.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnBeforeTurn(TurnEventHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            _beforeTurn.Enqueue(handler);
            return this;
        }

        /// <summary>
        /// Add a handler that will execute after the turn's activity handler logic is processed.
        /// <br/>
        /// Handler returns true to finish execution of the current turn. Handler returning false
        /// prevents the Agents state from being saved.
        /// </summary>
        /// <param name="handler">Function to call after turn execution.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnAfterTurn(TurnEventHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            _afterTurn.Enqueue(handler);
            return this;
        }

        /// <summary>
        /// Allows the AgentApplication to provide error handling without having to change the Adapter.OnTurnError.  This
        /// is beneficial since the application has more context.
        /// </summary>
        /// <remarks>
        /// Exceptions here will bubble-up to Adapter.OnTurnError.  Since it isn't know where in the turn the exception
        /// was thrown, it is possible that OnAfterTurn handlers, and ITurnState saving has NOT happened.
        /// </remarks>
        public AgentApplication OnTurnError(AgentApplicationTurnError handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            _turnErrorHandlers.Enqueue(handler);
            return this;
        }

#endregion

        #region ShowTyping
        /// <summary>
        /// Manually start a timer to periodically send "typing" activities.
        /// </summary>
        /// <remarks>
        /// The timer waits 1000ms to send its initial "typing" activity and then send an additional
        /// "typing" activity every 1000ms.The timer will automatically end once an outgoing activity
        /// has been sent. If the timer is already running or the current activity is not a "message"
        /// the call is ignored.
        /// </remarks>
        /// <param name="turnContext">The turn context.</param>
        public void StartTypingTimer(ITurnContext turnContext)
        {
            // Idempotent — if already started for this turn, do nothing.
            if (turnContext.Services.Get<TypingWorker>() != null)
            {
                return;
            }

            var worker = TypingWorker.Create(turnContext, Options.TypingOptions);
            if (worker == null)
            {
                return;
            }

            turnContext.Services.Set<TypingWorker>(worker);
            worker.Start();
        }

        /// <summary>
        /// Manually stop the typing timer for the current turn.
        /// </summary>
        /// <remarks>
        /// Stops the typing worker immediately and waits for it to finish. Subsequent calls for
        /// the same turn are no-ops. The worker is also stopped automatically at end of turn.
        /// </remarks>
        /// <param name="turnContext">The turn context.</param>
#pragma warning disable CA1822 // Method is intentionally an instance member for API symmetry with StartTypingTimer.
        public async Task StopTypingTimer(ITurnContext turnContext)
        {
            var worker = turnContext.Services.Get<TypingWorker>();
            if (worker != null)
            {
                await worker.DisposeAsync().ConfigureAwait(false);
                // Remove the entry so StartTypingTimer can create a new worker if called again.
                turnContext.Services.TryRemove(typeof(TypingWorker).FullName, out _);
            }
        }
#pragma warning restore CA1822

        /// <summary>
        /// Manually stop the typing timer.
        /// </summary>
        /// <remarks>
        /// If the timer isn't running nothing happens.
        /// Per-turn workers are definitively stopped in the turn's finally block via DisposeAsync.
        /// </remarks>
        [Obsolete("Use StopTypingTimer(ITurnContext) instead.")]
        public void StopTypingTimer()
        {
            // No-op: use StopTypingTimer(ITurnContext) to stop the per-turn worker explicitly.
        }

        #endregion

        #region Turn Handling

        /// <summary>
        /// Called by the adapter (for example, a <see cref="Microsoft.Agents.Hosting.AspNetCore.CloudAdapter"/>)
        /// at runtime in order to process an inbound <see cref="Microsoft.Agents.Core.Models.Activity"/>.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            AssertionHelpers.ThrowIfNull(turnContext.Activity, nameof(turnContext.Activity));

            using var onTurnTelemetryScope = new ScopeOnTurn(turnContext);

            if (_userAuth != null)
            {
                turnContext.Services.Set<UserAuthorization>(_userAuth);
            }

            bool routeMatched = false;
            bool routeAuthorized = false;

            try
            {
                // Start typing timer if configured
                if (Options.StartTypingTimer)
                {
                    StartTypingTimer(turnContext);
                };

                // Handle @mentions
                if (ActivityTypes.Message.Equals(turnContext.Activity.Type, StringComparison.OrdinalIgnoreCase))
                {
                    if (Options.NormalizeMentions)
                    {
                        turnContext.Activity.NormalizeMentions(Options.RemoveRecipientMention);
                    }
                    else if (Options.RemoveRecipientMention)
                    {
                        turnContext.Activity.Text = turnContext.Activity.RemoveRecipientMention();
                    }
                }

                // Load turn state
                ITurnState turnState = Options.TurnStateFactory!();
                await turnState!.LoadStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);

                try
                {
                    // Handle user auth
                    if (_userAuth != null)
                    {
                        // For AutoSignIn, this will initiate the OAuth flow.  Otherwise, this will continue OAuth flows
                        // start by a Route (when `autoSignInHandlers` are specified on the Route).
                        var signInComplete = await _userAuth.StartOrContinueSignInUserAsync(turnContext, turnState, cancellationToken: cancellationToken).ConfigureAwait(false);
                        if (!signInComplete)
                        {
                            return;
                        }
                    }

                    // Download any input files
                    IList<IInputFileDownloader>? fileDownloaders = Options.FileDownloaders;
                    if (fileDownloaders != null && fileDownloaders.Count > 0)
                    {
                        using var telemetryScope = new ScopeDownloadFiles(turnContext);
                        foreach (IInputFileDownloader downloader in fileDownloaders)
                        {
                            var files = await downloader.DownloadFilesAsync(turnContext, turnState, cancellationToken).ConfigureAwait(false);
                            turnState.Temp.InputFiles = [.. turnState.Temp.InputFiles, .. files];
                        }
                    }

                    // Call before turn handler
                    using (var telemetryScope = new ScopeBeforeTurn())
                    {
                        foreach (TurnEventHandler beforeTurnHandler in _beforeTurn)
                        {
                            if (!await beforeTurnHandler(turnContext, turnState, cancellationToken))
                            {
                                // Save turn state
                                // - This lets the Agent keep track of why it ended the previous turn. It also
                                //   allows the dialog system to be used before the AI system is called.
                                await turnState!.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);

                                return;
                            }
                        }
                    }

                    if (Logger.IsEnabled(LogLevel.Debug))
                    {
                        var (routeCount, routeListFormatted) = _routes.FormatRouteList();
                        LogRouteList(Logger, routeCount, routeListFormatted);
                    }

                    // Execute first matching handler.  The RouteList enumerator is ordered by Invoke & Rank, then by Rank & add order.
                    foreach (Route route in _routes.Enumerate())
                    {
                        if (await route.Selector(turnContext, cancellationToken))
                        {
                            routeMatched = true;
                            var handlers = route.OAuthHandlers(turnContext);
                            if (_userAuth == null || handlers?.Length == 0)
                            {
                                routeAuthorized = true;
                                using var routeTelemetryScope = new ScopeRouteHandler(
                                        isAgentic: route.Flags.HasFlag(RouteFlags.Agentic),
                                        isInvoke: route.Flags.HasFlag(RouteFlags.Invoke)
                                    );
                                await route.Handler(turnContext, turnState, cancellationToken);
                            }
                            else
                            {
                                bool signInComplete = false;

                                foreach (var handler in handlers)
                                {
                                    signInComplete = await _userAuth.StartOrContinueSignInUserAsync(turnContext, turnState, handler, forceAuto: true, cancellationToken: cancellationToken).ConfigureAwait(false);
                                    if (!signInComplete)
                                    {
                                        break;
                                    }
                                }

                                if (signInComplete)
                                {
                                    routeAuthorized = true;
                                    using var routeTelemetryScope = new ScopeRouteHandler(
                                        isAgentic: route.Flags.HasFlag(RouteFlags.Agentic),
                                        isInvoke: route.Flags.HasFlag(RouteFlags.Invoke)
                                    );
                                    await route.Handler(turnContext, turnState, cancellationToken);
                                }
                            }

                            if (!route.Flags.HasFlag(RouteFlags.NonTerminal))
                            {
                                break;
                            }
                        }
                    }
                    onTurnTelemetryScope.Share(routeAuthorized, routeMatched);

                    // Call after turn handler
                    using (var telemetryScope = new ScopeAfterTurn())
                    {
                        foreach (TurnEventHandler afterTurnHandler in _afterTurn)
                        {
                            if (!await afterTurnHandler(turnContext, turnState, cancellationToken))
                            {
                                return;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    onTurnTelemetryScope.Share(routeAuthorized, routeMatched);
                    foreach (AgentApplicationTurnError errorHandler in _turnErrorHandlers)
                    {
                        await errorHandler(turnContext, turnState, ex, cancellationToken).ConfigureAwait(false);
                    }

                    throw;
                }

                await turnState!.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // Stop the typing worker for this turn if one was started.
                var typingWorker = turnContext.Services?.Get<TypingWorker>();
                if (typingWorker != null)
                {
                    await typingWorker.DisposeAsync().ConfigureAwait(false);
                }

                if (turnContext.StreamingResponse != null && turnContext.StreamingResponse.IsStreamStarted())
                {
                    await turnContext.StreamingResponse.EndStreamAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private void ApplyRouteAttributes()
        {
            // This will evaluate all methods that have an attribute, in declaration order (grouped by inheritance chain)
            foreach (var method in GetType().GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var activityRoutes = method.GetCustomAttributes<Attribute>(true);
                foreach (var attribute in activityRoutes)
                {
                    // Add route for all IRouteAttribute instances
                    if (attribute is IRouteAttribute routeAttribute)
                    {
                        routeAttribute.AddRoute(this, method);
                    }
                }
            }
        }

        #endregion

        #region Extension

        /// <summary>
        /// Registers extension with application, providing callback to specify extension features.
        /// </summary>
        /// <typeparam name="TExtension"></typeparam>
        /// <param name="extension"></param>
        /// <param name="extensionRegistration"></param>
        public void RegisterExtension<TExtension>(TExtension extension, Action<TExtension> extensionRegistration)
            where TExtension : IAgentExtension
        {
            AssertionHelpers.ThrowIfNull(extensionRegistration, nameof(extensionRegistration));
            if (RegisteredExtensions.Contains(extension))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.ExtensionAlreadyRegistered, null, nameof(TExtension));
            }
            // TODO: add Logging event for extension registration
            RegisteredExtensions.Add(extension);
            extensionRegistration(extension);
        }
        #endregion
    }
}
