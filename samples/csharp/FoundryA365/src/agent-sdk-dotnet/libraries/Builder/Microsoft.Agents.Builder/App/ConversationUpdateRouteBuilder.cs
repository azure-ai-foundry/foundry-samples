// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// RouteBuilder for routing ConversationUpdate activities in an AgentApplication.
    /// </summary>
    /// <remarks>
    /// Use <see cref="Microsoft.Agents.Builder.App.ConversationUpdateRouteBuilder"/> to create and configure routes that respond to event
    /// activities. This builder allows matching event activities by name or regular expression, and supports
    /// channelId and agentic routing scenarios. Instances are created via the <see cref="Microsoft.Agents.Builder.App.ConversationUpdateRouteBuilder.Create"/> method
    /// and further configured using one of <see cref="Microsoft.Agents.Builder.App.ConversationUpdateRouteBuilder.WithUpdateEvent(string)"/> or <see cref="Microsoft.Agents.Builder.App.ConversationUpdateRouteBuilder.WithSelector(Microsoft.Agents.Builder.App.RouteSelector)"/>.<br/><br/>
    /// Example usage:<br/><br/>
    /// <code>
    /// var route = ConversationUpdateRouteBuilder.Create()
    ///    .WithUpdateEvent(ConversationUpdateEvents.MembersAdded)
    ///    .WithHandler(async (context, state, ct) => Task.FromResult(context.SendActivityAsync("Welcome!", cancellationToken: ct)))
    ///    .Build();
    ///    
    /// app.AddRoute(route);
    /// </code>
    /// </remarks>
    public class ConversationUpdateRouteBuilder : RouteBuilderBase<ConversationUpdateRouteBuilder>
    {
        /// <summary>
        /// Configures the route to match a specific <see cref="Microsoft.Agents.Builder.App.ConversationUpdateEvents"/>, such as members being added or removed.
        /// </summary>
        /// <remarks>Use this method to restrict the route to trigger only for a particular conversation
        /// update event. If the specified event is not recognized, the route will match any conversation update
        /// activity.</remarks>
        /// <param name="eventName">The name of the conversation update event to match. Common values include events for members being 
        /// added or removed. Cannot be null.</param>
        /// <returns>The current <see cref="Microsoft.Agents.Builder.App.ConversationUpdateRouteBuilder"/> instance for method chaining.</returns>
        public ConversationUpdateRouteBuilder WithUpdateEvent(string eventName)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(eventName, nameof(eventName));

            if (_route.Selector != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"ConversationUpdateRouteBuilder.WithUpdateEvent({eventName})");
            }

            if (ConversationUpdateEvents.MembersAdded.Equals(eventName))
            {
                _route.Selector = (context, ct) => Task.FromResult
                    (
                        IsContextMatch(context, _route)
                        && context.Activity.IsType(ActivityTypes.ConversationUpdate)
                        && context.Activity.MembersAdded?.Count > 0
                    );
            }
            else if (ConversationUpdateEvents.MembersRemoved.Equals(eventName))
            {
                _route.Selector = (context, ct) => Task.FromResult
                    (
                        IsContextMatch(context, _route)
                        && context.Activity.IsType(ActivityTypes.ConversationUpdate)
                        && context.Activity.MembersRemoved?.Count > 0
                    );
            }
            else
            {
                _route.Selector = (context, ct) => Task.FromResult
                    (
                        IsContextMatch(context, _route)
                        && context.Activity.IsType(ActivityTypes.ConversationUpdate)
                    );
            }

            return this;
        }

        /// <summary>
        /// Sets a custom route selector used to determine how incoming requests are matched to this route builder.
        /// </summary>
        /// <remarks>Use this method to customize the matching logic for routes. This allows for advanced
        /// routing scenarios where requests are selected based on custom rules or patterns.</remarks>
        /// <param name="selector">The route selector that defines the criteria for matching requests to the route. The supplied selector does
        /// not need to validate base route properties like ChannelId, Agentic, etc. An Activity type of "conversationUpdate" is enforced.</param>
        /// <returns>The current instance of <see cref="Microsoft.Agents.Builder.App.ConversationUpdateRouteBuilder"/> with the specified selector applied.</returns>
        public override ConversationUpdateRouteBuilder WithSelector(RouteSelector selector)
        {
            if (_route.Selector != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"ConversationUpdateRouteBuilder.WithSelector()");
            }

            async Task<bool> ensureConversationUpdate(ITurnContext context, CancellationToken cancellationToken)
            {
                return IsContextMatch(context, _route) && context.Activity.IsType(ActivityTypes.ConversationUpdate) && await selector(context, cancellationToken).ConfigureAwait(false);
            }

            _route.Selector = ensureConversationUpdate;
            return this;
        }

        /// <summary>
        /// Assigns the specified route handler to the current route and returns the updated builder instance.
        /// </summary>
        /// <param name="handler">The route handler to associate with the route. Cannot be null.</param>
        /// <returns>The current RouteBuilder instance with the handler set, enabling method chaining.</returns>
        public ConversationUpdateRouteBuilder WithHandler(RouteHandler handler)
        {
            _route.Handler = handler;
            return this;
        }

        /// <summary>
        /// Returns the current conversation update route builder instance. For event routes, the invoke flag is ignored to
        /// prevent misconfiguration.
        /// </summary>
        /// <remarks>Conversation updates cannot be configured as invoke routes. This method always returns the
        /// current instance, regardless of the value of <paramref name="isInvoke"/>.</remarks>
        /// <param name="isInvoke">Ignored</param>
        /// <returns>The current instance of <see cref="Microsoft.Agents.Builder.App.ConversationUpdateRouteBuilder"/>.</returns>
        public override ConversationUpdateRouteBuilder AsInvoke(bool isInvoke = true)
        {
            return this;
        }
    }
}
