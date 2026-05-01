// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Core;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// RouteBuilder for routing activities of a specific type in an AgentApplication.
    /// </summary>
    /// <remarks>
    /// Use <see cref="Microsoft.Agents.Builder.App.TypeRouteBuilder"/> to create and configure routes that respond to event
    /// activities. This builder allows matching event activities by name or regular expression, and supports
    /// channelId and agentic routing scenarios. Instances are created via the <see cref="Microsoft.Agents.Builder.App.TypeRouteBuilder.Create"/> method
    /// and further configured using one of <see cref="Microsoft.Agents.Builder.App.TypeRouteBuilder.WithType(string)"/> or <see cref="Microsoft.Agents.Builder.App.TypeRouteBuilder.WithType(System.Text.RegularExpressions.Regex)"/>
    /// or <see cref="Microsoft.Agents.Builder.App.TypeRouteBuilder.WithSelector(Microsoft.Agents.Builder.App.RouteSelector)"/>.<br/><br/>
    /// Example usage:<br/><br/>
    /// <code>
    /// var route = TypeRouteBuilder.Create()
    ///    .WithName("myInvoke")
    ///    .WithHandler(async (context, state, ct) => Task.FromResult(context.SendActivityAsync("Invoke received!", cancellationToken: ct)))
    ///    .Build();
    ///
    /// app.AddRoute(route);
    /// </code>
    /// Since this builder can't determine if this is for an Invoke Activity, the method <see cref="Microsoft.Agents.Builder.App.TypeRouteBuilder.AsInvoke(bool)"/> should be called if appropriate.
    /// </remarks>
    public class TypeRouteBuilder : RouteBuilderBase<TypeRouteBuilder>
    {
        private string _type;
        private Regex _typePattern;

        /// <summary>
        /// Configures the route to match activities of the specified type.
        /// </summary>
        /// <remarks>This method updates the route selector to filter activities based on the provided
        /// type. If the route is marked as agentic, only agentic requests will be considered for matching.</remarks>
        /// <param name="type">The activity type to match. Cannot be null or empty.</param>
        /// <returns>A TypeRouteBuilder instance with the added selector for matching Activity.Type.</returns>
        public TypeRouteBuilder WithType(string type)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(type, nameof(type));

            if (_type != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"TypeRouteBuilder.WithType({type}) with Type already set");
            }

            if (_typePattern != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"TypeRouteBuilder.WithType({type}) with Type Regex already set");
            }

            _type = type;
            return this;
        }

        /// <summary>
        /// Configures the route to match activities whose type satisfies the specified regular expression pattern.
        /// </summary>
        /// <remarks>This method updates the route's selector to only match activities whose type matches
        /// the provided pattern. If the route is marked as agentic, it will also require the request to be agentic for
        /// the selector to return <see langword="true"/>.</remarks>
        /// <param name="typePattern">A regular expression used to determine whether the activity type should be matched by the route. Cannot be
        /// null.</param>
        /// <returns>A TypeRouteBuilder instance configured with the specified Activity.Type pattern selector.</returns>
        public TypeRouteBuilder WithType(Regex typePattern)
        {
            AssertionHelpers.ThrowIfNull(typePattern, nameof(typePattern));

            if (_typePattern != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"TypeRouteBuilder.WithType(Regex({typePattern})) with Type Regex already set");
            }

            if (_type != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"TypeRouteBuilder.WithType(Regex({typePattern})) with Type already set");
            }

            _typePattern = typePattern;
            return this;
        }

        /// <summary>
        /// Sets the route selector used to determine how incoming requests are matched to this route builder.
        /// </summary>
        /// <remarks>Use this method to customize the matching logic for routes. This allows for advanced
        /// routing scenarios where requests are selected based on custom rules or patterns. If WithType was
        /// also called, this selector is in addition to the Type selector.</remarks>
        /// <param name="selector">The route selector that defines the criteria for matching requests to the route. The supplied selector does
        /// not need to validate base route properties like ChannelId, Agentic, etc...</param>
        /// <returns>A TypeRouteBuilder instance configured with the specified custom selector.</returns>
        public override TypeRouteBuilder WithSelector(RouteSelector selector)
        {
            AssertionHelpers.ThrowIfNull(selector, nameof(selector));

            if (_route.Selector != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"TypeRouteBuilder.WithSelector()");
            }

            async Task<bool> ensureRouteMatch(ITurnContext context, CancellationToken cancellationToken)
            {
                return IsContextMatch(context, _route) && await selector(context, cancellationToken).ConfigureAwait(false);
            }

            _route.Selector = ensureRouteMatch;
            return this;
        }

        /// <summary>
        /// Assigns the specified route handler to the current route and returns the updated builder instance.
        /// </summary>
        /// <param name="handler">The route handler to associate with the route. Cannot be null.</param>
        /// <returns>The current RouteBuilder instance with the handler set, enabling method chaining.</returns>
        public TypeRouteBuilder WithHandler(RouteHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            _route.Handler = handler;
            return (TypeRouteBuilder)this;
        }

        protected override void PreBuild()
        {
            if (_route.Selector != null)
            {
                if (_type != null || _typePattern != null)
                {
                    // Match on both the existing selector and the Activity.Type
                    var existingSelector = _route.Selector;
                    _route.Selector = async (context, ct) =>
                        IsContextMatch(context, _route)
                        && (_type != null ? context.Activity.IsType(_type) : context.Activity.Type != null && _typePattern.IsMatch(context.Activity.Type))
                        && await existingSelector(context, ct);
                }
                return;
            }

            if (_type == null && _typePattern == null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteBuilderMissingProperty, null, nameof(TypeRouteBuilder), "Type or Selector");
            }

            // Just match on Activity.Type value
            _route.Selector = (context, ct) => Task.FromResult
                (
                    IsContextMatch(context, _route)
                    && (_type != null ? context.Activity.IsType(_type) : context.Activity.Type != null && _typePattern.IsMatch(context.Activity.Type))
                );
        }
    }
}
