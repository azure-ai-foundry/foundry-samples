// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// RouteBuilder for routing Event activities in an AgentApplication.
    /// </summary>
    /// <remarks>
    /// Use <see cref="Microsoft.Agents.Builder.App.EventRouteBuilder"/> to create and configure routes that respond to event
    /// activities. This builder allows matching event activities by name or regular expression, and supports
    /// channelId and agentic routing scenarios. Instances are created via the <see cref="Microsoft.Agents.Builder.App.EventRouteBuilder.Create"/> method
    /// and further configured using one of <see cref="Microsoft.Agents.Builder.App.EventRouteBuilder.WithName(string)"/> or <see cref="Microsoft.Agents.Builder.App.EventRouteBuilder.WithName(System.Text.RegularExpressions.Regex)"/>
    /// or <see cref="Microsoft.Agents.Builder.App.EventRouteBuilder.WithSelector(Microsoft.Agents.Builder.App.RouteSelector)"/>.<br/><br/>
    /// Example usage:<br/><br/>
    /// <code>
    /// var route = EventRouteBuilder.Create()
    ///    .WithName("myEvent")
    ///    .WithHandler(async (context, state, ct) => Task.FromResult(context.SendActivityAsync("Event received!", cancellationToken: ct)))
    ///    .Build();
    ///    
    /// app.AddRoute(route);
    /// </code>
    /// </remarks>
    public class EventRouteBuilder : RouteBuilderBase<EventRouteBuilder>
    {
        private string _eventName;
        private Regex _eventRegex;

        /// <summary>
        /// Configures the route to match event activities with the specified name, using a case-insensitive comparison.
        /// </summary>
        /// <remarks>This method restricts the route to only handle event activities whose name matches
        /// the specified value. If the route is marked as agentic, only agentic requests will be considered for
        /// matching.</remarks>
        /// <param name="name">The name of the event activity to match. Comparison is case-insensitive. Cannot be null.</param>
        /// <returns>A EventRouteBuilder instance with the added selector for matching Activity.Name.</returns>
        public EventRouteBuilder WithName(string name)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(name, nameof(name));

            if (_eventName != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"EventRouteBuilder.WithName({name}) with Name already set");
            }

            if (_eventRegex != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"EventRouteBuilder.WithName({name}) with Name Regex already set");
            }

            _eventName = name;
            return this;
        }

        /// <summary>
        /// Configures the event route to match event activities whose name satisfies the specified regular expression
        /// pattern.
        /// </summary>
        /// <remarks>This method restricts the route to event activities whose name matches the provided
        /// pattern. If the route is marked as agentic, only agentic requests will be considered for matching.</remarks>
        /// <param name="namePattern">The regular expression used to match the name of incoming event activities. Cannot be null.</param>
        /// <returns>A EventRouteBuilder instance configured with the specified Activity.Name pattern selector.</returns>
        public EventRouteBuilder WithName(Regex namePattern)
        {
            AssertionHelpers.ThrowIfNull(namePattern, nameof(namePattern));

            if (_eventRegex != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"EventRouteBuilder.WithName(Regex({namePattern})) with Name Regex already set");
            }

            if (_eventName != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"EventRouteBuilder.WithName(Regex({namePattern})) with Name already set");
            }

            _eventRegex = namePattern;
            return this;
        }

        /// <summary>
        /// Sets a custom route selector used to determine how incoming requests are matched to this route builder.
        /// </summary>
        /// <remarks>Use this method to customize the matching logic for routes. This allows for advanced
        /// routing scenarios where requests are selected based on custom rules or patterns. If WithName was
        /// also called, this selector is in addition to the Name selector.</remarks>
        /// <param name="selector">The route selector that defines the criteria for matching requests to the route. The supplied selector does
        /// not need to validate base route properties like ChannelId, Agentic, etc. An Activity type of "event" is enforced.</param>
        /// <returns>A EventRouteBuilder instance configured with the custom selector.</returns>
        public override EventRouteBuilder WithSelector(RouteSelector selector)
        {
            AssertionHelpers.ThrowIfNull(selector, nameof(selector));

            if (_route.Selector != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"EventRouteBuilder.WithSelector()");
            }

            async Task<bool> ensureEvent(ITurnContext context, CancellationToken cancellationToken)
            {
                return IsContextMatch(context, _route) && context.Activity.IsType(ActivityTypes.Event) && await selector(context, cancellationToken).ConfigureAwait(false);
            }

            _route.Selector = ensureEvent;
            return this;
        }

        /// <summary>
        /// Assigns the specified route handler to the current route and returns the updated builder instance.
        /// </summary>
        /// <param name="handler">The route handler to associate with the route. Cannot be null.</param>
        /// <returns>The current RouteBuilder instance with the handler set, enabling method chaining.</returns>
        public EventRouteBuilder WithHandler(RouteHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            _route.Handler = handler;
            return this;
        }

        /// <summary>
        /// For event routes, the invoke flag is ignored to prevent misconfiguration.
        /// </summary>
        /// <remarks>Events cannot be configured as invoke routes. This method always returns the
        /// current instance, regardless of the value of <paramref name="isInvoke"/>.</remarks>
        /// <param name="isInvoke">Ignored</param>
        /// <returns>The current instance of <see cref="Microsoft.Agents.Builder.App.EventRouteBuilder"/>.</returns>
        public override EventRouteBuilder AsInvoke(bool isInvoke = true)
        {
            return this;
        }

        protected override void PreBuild()
        {
            if (_route.Selector != null)
            {
                if (_eventName != null || _eventRegex != null)
                {
                    // Match on both the existing selector and the Activity.Name
                    var existingSelector = _route.Selector;
                    _route.Selector = async (context, ct) =>
                        IsContextMatch(context, _route)
                        && context.Activity.IsType(ActivityTypes.Event)
                        && (_eventName != null ? _eventName.Equals(context.Activity.Name, StringComparison.OrdinalIgnoreCase) : context.Activity.Name != null && _eventRegex.IsMatch(context.Activity.Name))
                        && await existingSelector(context, ct);
                }
                return;
            }

            if (_eventName == null && _eventRegex == null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteBuilderMissingProperty, null, nameof(EventRouteBuilder), "Name or Selector");
            }

            // Just match on Activity.Name value
            _route.Selector = (context, ct) => Task.FromResult
                (
                    IsContextMatch(context, _route)
                    && context.Activity.IsType(ActivityTypes.Event)
                    && context.Activity.Name != null
                    && (_eventName != null ? _eventName.Equals(context.Activity.Name, StringComparison.OrdinalIgnoreCase) : context.Activity.Name != null && _eventRegex.IsMatch(context.Activity.Name))
                );
        }
    }
}
