// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using System.Threading;

namespace Microsoft.Agents.Builder
{
    public interface IMiddlewareSet
    {
        /// <summary>
        /// Adds a middleware object to the end of the set.
        /// </summary>
        /// <param name="middleware">The middleware to add.</param>
        /// <returns>The updated middleware set.</returns>
        /// <see cref="IChannelAdapter.Use(IMiddleware)"/>
        IMiddlewareSet Use(IMiddleware middleware);

        /// <summary>
        /// Processes an activity.
        /// </summary>
        /// <param name="turnContext">The context object for the turn.</param>
        /// <param name="callback">The delegate to call when the set finishes processing the activity.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        Task ReceiveActivityWithStatusAsync(ITurnContext turnContext, AgentCallbackHandler callback, CancellationToken cancellationToken);
    }
}
