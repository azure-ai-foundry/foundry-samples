// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// RouteBuilder for routing Feedback Loop activities in an AgentApplication.
    /// </summary>
    /// <remarks>
    /// Use <see cref="Microsoft.Agents.Builder.App.FeedbackRouteBuilder"/> to create and configure routes that respond to activities of type 'invoke' with
    /// name "message/submitAction". This builder allows matching event activities by name or regular expression, and supports
    /// channelId and agentic routing scenarios. Instances are created via the <see cref="Microsoft.Agents.Builder.App.FeedbackRouteBuilder.Create"/> method
    /// and further configured using <see cref="Microsoft.Agents.Builder.App.FeedbackRouteBuilder.WithHandler(Microsoft.Agents.Builder.App.FeedbackLoopHandler)"/>.<br/><br/>
    /// Example usage:<br/><br/>
    /// <code>
    /// var route = FeedbackRouteBuilder.Create()
    ///    .WithHandler(async (context, state, feedbackData, ct) => Task.FromResult(context.SendActivityAsync("Feedback action received", cancellationToken: ct)))
    ///    .Build();
    ///    
    /// app.AddRoute(route);
    /// </code>
    /// </remarks>
    public class FeedbackRouteBuilder : RouteBuilderBase<FeedbackRouteBuilder>
    {
        public FeedbackRouteBuilder() : base()
        {
            _route.Flags |= RouteFlags.Invoke;
        }

        /// <summary>
        /// Configures the route to handle feedback actions using the specified feedback loop handler.
        /// </summary>
        /// <remarks>This method sets up the route to invoke the provided handler when an incoming
        /// activity represents a feedback action (i.e., an invoke activity with the name "message/submitAction" and an
        /// actionName of "feedback"). Use this method to define custom logic for handling feedback submissions in the
        /// feedback loop.</remarks>
        /// <param name="handler">A delegate that processes feedback data when a feedback action is received. Cannot be null.</param>
        /// <returns>The current <see cref="Microsoft.Agents.Builder.App.FeedbackRouteBuilder"/> instance for method chaining.</returns>
        public FeedbackRouteBuilder WithHandler(FeedbackLoopHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            Task<bool> routeSelector(ITurnContext context, CancellationToken _)
            {
                var jsonObject = ProtocolJsonSerializer.ToObject<JsonObject>(context.Activity.Value);
                string? actionName = jsonObject != null && jsonObject.ContainsKey("actionName") ? jsonObject["actionName"].ToString() : string.Empty;
                return Task.FromResult
                (
                    IsContextMatch(context, _route)
                    && context.Activity.Type == ActivityTypes.Invoke
                    && context.Activity.Name == "message/submitAction"
                    && actionName == "feedback"
                );
            }

            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                FeedbackData feedbackLoopData = ProtocolJsonSerializer.ToObject<FeedbackData>(turnContext.Activity.Value)!;
                feedbackLoopData.ReplyToId = turnContext.Activity.ReplyToId;

                await handler(turnContext, turnState, feedbackLoopData, cancellationToken);
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
        /// <param name="isInvoke">Ignored</param>
        /// <returns>The current instance of <see cref="Microsoft.Agents.Builder.App.FeedbackRouteBuilder"/> with Invoke routing enabled.</returns>
        public override FeedbackRouteBuilder AsInvoke(bool isInvoke = true)
        {
            return this;
        }
    }
}
