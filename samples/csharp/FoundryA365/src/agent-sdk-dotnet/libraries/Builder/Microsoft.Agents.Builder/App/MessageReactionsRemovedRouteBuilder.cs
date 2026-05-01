// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// RouteBuilder for routing message reactions removed activities in an AgentApplication.
    /// </summary>
    /// <remarks>
    /// Use <see cref="Microsoft.Agents.Builder.App.MessageReactionsRemovedRouteBuilder"/> to create and configure routes that respond to activities of type 'messageReaction'
    /// where reactions have been removed. Removed reactions are in <see cref="Microsoft.Agents.Core.Models.Activity.ReactionsRemoved"/> property of the activity."/>
    /// This builder allows matching event activities by name or regular expression, and supports channelId and agentic routing scenarios.
    /// Instances are created via the <see cref="Microsoft.Agents.Builder.App.MessageReactionsRemovedRouteBuilder.Create"/> method and further configured using <see cref="Microsoft.Agents.Builder.App.MessageReactionsRemovedRouteBuilder.WithHandler(Microsoft.Agents.Builder.App.RouteHandler)"/>.<br/><br/>
    /// Example usage:<br/><br/>
    /// <code>
    /// var route = MessageReactionsRemovedRouteBuilder.Create()
    ///    .WithHandler(async (context, state, continuation, ct) => Task.FromResult(context.SendActivityAsync("Reactions removed", cancellationToken: ct)))
    ///    .Build();
    ///    
    /// app.AddRoute(route);
    /// </code>
    /// </remarks>
    public class MessageReactionsRemovedRouteBuilder : RouteBuilderBase<MessageReactionsRemovedRouteBuilder>
    {
        public MessageReactionsRemovedRouteBuilder WithHandler(RouteHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            _route.Selector = (ITurnContext context, CancellationToken _) => Task.FromResult
            (
                (!_route.Flags.HasFlag(RouteFlags.Agentic) || AgenticAuthorization.IsAgenticRequest(context))
                && context.Activity.IsType(ActivityTypes.MessageReaction)
                && context.Activity?.ReactionsRemoved != null
                && context.Activity.ReactionsRemoved.Count > 0
            );

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
        /// <returns>The current instance of <see cref="Microsoft.Agents.Builder.App.MessageReactionsRemovedRouteBuilder"/> with Invoke routing enabled.</returns>
        public override MessageReactionsRemovedRouteBuilder AsInvoke(bool isInvoke = true)
        {
            return this;
        }
    }
}
