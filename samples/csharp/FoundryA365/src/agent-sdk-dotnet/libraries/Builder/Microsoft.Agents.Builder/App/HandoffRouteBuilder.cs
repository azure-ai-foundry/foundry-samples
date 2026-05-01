// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// RouteBuilder for routing Handoff activities in an AgentApplication.
    /// </summary>
    /// <remarks>
    /// Use <see cref="Microsoft.Agents.Builder.App.HandoffRouteBuilder"/> to create and configure routes that respond to activities of type 'invoke' with
    /// name "handoff/action". This builder allows matching event activities by name or regular expression, and supports
    /// channelId and agentic routing scenarios. Instances are created via the <see cref="Microsoft.Agents.Builder.App.HandoffRouteBuilder.Create"/> method
    /// and further configured using <see cref="Microsoft.Agents.Builder.App.HandoffRouteBuilder.WithHandler(Microsoft.Agents.Builder.App.HandoffHandler)"/>.<br/><br/>
    /// Example usage:<br/><br/>
    /// <code>
    /// var route = HandoffRouteBuilder.Create()
    ///    .WithHandler(async (context, state, continuation, ct) => Task.FromResult(context.SendActivityAsync("Handoff action received", cancellationToken: ct)))
    ///    .Build();
    ///    
    /// app.AddRoute(route);
    /// </code>
    /// </remarks>
    public class HandoffRouteBuilder : RouteBuilderBase<HandoffRouteBuilder>
    {
        public HandoffRouteBuilder() : base()
        {
            _route.Flags |= RouteFlags.Invoke;
        }

        public HandoffRouteBuilder WithHandler(HandoffHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            Task<bool> routeSelector(ITurnContext context, CancellationToken _) => Task.FromResult
                (
                    IsContextMatch(context, _route)
                    && context.Activity.IsType(ActivityTypes.Invoke)
                    && string.Equals(context.Activity?.Name, "handoff/action", System.StringComparison.OrdinalIgnoreCase)
                );

            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                string token = turnContext.Activity.Value?.GetType()?.GetProperty("Continuation")?.GetValue(turnContext.Activity.Value) as string ?? "";
                await handler(turnContext, turnState, token, cancellationToken);
                await turnContext.SendActivityAsync(Activity.CreateInvokeResponseActivity(), cancellationToken);
            }

            _route.Selector = routeSelector;
            _route.Handler = routeHandler;

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
        /// <returns>The current instance of <see cref="Microsoft.Agents.Builder.App.HandoffRouteBuilder"/> with Invoke routing enabled.</returns>
        public override HandoffRouteBuilder AsInvoke(bool isInvoke = true)
        {
            return this;
        }
    }
}
