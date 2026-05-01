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
    /// RouteBuilder for routing Invoke activities in an AgentApplication.
    /// </summary>
    /// <remarks>Use this builder to define routing logic for activities of type 'invoke', such as those
    /// triggered by adaptive cards or other client-initiated operations. The builder allows specifying matching
    /// criteria based on the activity's name, enabling precise control over which invoke activities are handled by the
    /// route.</remarks>
    public class InvokeRouteBuilder : RouteBuilderBase<InvokeRouteBuilder>
    {
        private string _invokeName;
        private Regex _invokeRegex;

        public InvokeRouteBuilder() : base()
        {
            _route.Flags |= RouteFlags.Invoke;
        }

        /// <summary>
        /// Configures the route to match only invoke activities with the specified name.
        /// </summary>
        /// <remarks>This method restricts the route to handle only invoke activities whose <c>Name</c>
        /// property matches the specified value. If the route is marked as agentic, the request must also be authorized
        /// as agentic to match.</remarks>
        /// <param name="name">The name of the invoke activity to match. Comparison is case-insensitive.</param>
        /// <returns>A InvokeRouteBuilder instance with the added selector for matching Activity.Name.</returns>
        public InvokeRouteBuilder WithName(string name)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(name, nameof(name));

            if (_invokeName != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"InvokeRouteBuilder.WithName({name}) with Name already set");
            }

            if (_invokeRegex != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"InvokeRouteBuilder.WithName({name}) with Name Regex already set");
            }

            _invokeName = name;
            return this;
        }

        /// <summary>
        /// Configures the route to match invoke activities whose name matches the specified regular expression pattern.
        /// </summary>
        /// <remarks>This method restricts the route to handle only invoke activities with names matching
        /// the provided pattern. If the route is marked as agentic, it will only match agentic requests. Use this
        /// method to filter invoke activities based on their name when defining route handlers.</remarks>
        /// <param name="namePattern">A regular expression used to match the name of incoming invoke activities. Only activities with a name that
        /// matches this pattern will be handled by the route.</param>
        /// <returns>A InvokeRouteBuilder instance configured with the specified Activity.Name pattern selector.</returns>
        public InvokeRouteBuilder WithName(Regex namePattern)
        {
            AssertionHelpers.ThrowIfNull(namePattern, nameof(namePattern));

            if (_invokeRegex != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"InvokeRouteBuilder.WithName(Regex({namePattern})) with Name Regex already set");
            }

            if (_invokeName != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"InvokeRouteBuilder.WithName(Regex({namePattern})) with Name already set");
            }

            _invokeRegex = namePattern;
            return this;
        }

        /// <summary>
        /// Sets a custom route selector used to determine how incoming requests are matched to this route builder.
        /// </summary>
        /// <remarks>Use this method to customize the matching logic for routes. This allows for advanced
        /// routing scenarios where requests are selected based on custom rules or patterns. If WithName was
        /// also called, this selector is in addition to the Name selector.</remarks>
        /// <param name="selector">The route selector that defines the criteria for matching requests to the route. The supplied selector does
        /// not need to validate base route properties like ChannelId, Agentic, etc. An Activity type of "invoke" is enforced.</param>
        /// <returns>The current instance of <see cref="Microsoft.Agents.Builder.App.InvokeRouteBuilder"/> with the specified selector applied.</returns>
        public override InvokeRouteBuilder WithSelector(RouteSelector selector)
        {
            if (_route.Selector != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"InvokeRouteBuilder.WithSelector()");
            }

            async Task<bool> ensureInvoke(ITurnContext context, CancellationToken cancellationToken)
            {
                return IsContextMatch(context, _route) && context.Activity.IsType(ActivityTypes.Invoke) && await selector(context, cancellationToken).ConfigureAwait(false);
            }

            _route.Selector = ensureInvoke;
            return this;
        }

        /// <summary>
        /// Assigns the specified route handler to the current route and returns the updated builder instance.
        /// </summary>
        /// <param name="handler">The route handler to associate with the route. Cannot be null.</param>
        /// <returns>The current RouteBuilder instance with the handler set, enabling method chaining.</returns>
        public InvokeRouteBuilder WithHandler(RouteHandler handler)
        {
            _route.Handler = handler;
            return this;
        }

        /// <summary>
        /// Returns the current route builder instance configured for Invoke routing. This method ensures that the route
        /// remains set as an Invoke route.
        /// </summary>
        /// <remarks>This override prevents changing the route configuration from Invoke routing,
        /// maintaining consistency with the route's initial setup.</remarks>
        /// <param name="isInvoke">A value indicating whether the route should be treated as an Invoke route. The parameter is ignored, as the
        /// route is always configured for Invoke routing.</param>
        /// <returns>The current instance of <see cref="Microsoft.Agents.Builder.App.InvokeRouteBuilder"/> with Invoke routing enabled.</returns>
        public override InvokeRouteBuilder AsInvoke(bool isInvoke = true)
        {
            return this;
        }

        protected override void PreBuild()
        {
            if (_route.Selector != null)
            {
                if (_invokeName != null || _invokeRegex != null)
                {
                    // Match on both the existing selector and the Activity.Name
                    var existingSelector = _route.Selector;
                    _route.Selector = async (context, ct) =>
                        IsContextMatch(context, _route)
                        && context.Activity.IsType(ActivityTypes.Invoke)
                        && (_invokeName != null ? _invokeName.Equals(context.Activity.Name, StringComparison.OrdinalIgnoreCase) : context.Activity.Name != null && _invokeRegex.IsMatch(context.Activity.Name))
                        && await existingSelector(context, ct);
                }
                return;
            }

            if (_invokeName == null && _invokeRegex == null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteBuilderMissingProperty, null, nameof(InvokeRouteBuilder), "Name or Selector");
            }

            // Just match on Activity.Name value
            _route.Selector = (context, ct) => Task.FromResult
                (
                    IsContextMatch(context, _route)
                    && context.Activity.IsType(ActivityTypes.Invoke)
                    && (_invokeName != null ? _invokeName.Equals(context.Activity.Name, StringComparison.OrdinalIgnoreCase) : context.Activity.Name != null && _invokeRegex.IsMatch(context.Activity.Name))
                );
        }
    }
}
