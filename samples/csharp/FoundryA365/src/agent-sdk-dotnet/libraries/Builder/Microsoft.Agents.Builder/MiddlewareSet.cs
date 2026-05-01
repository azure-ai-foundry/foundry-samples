// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Contains an ordered set of <see cref="IMiddleware"/>.
    /// </summary>
    internal class MiddlewareSet : IMiddlewareSet, IMiddleware, IEnumerable<IMiddleware>
    {
        private readonly IList<IMiddleware> _middleware = [];

        /// <inheritdoc/>
        public IMiddlewareSet Use(IMiddleware middleware)
        {
            AssertionHelpers.ThrowIfNull(middleware, nameof(middleware));

            _middleware.Add(middleware);
            return this;
        }

        /// <inheritdoc/>
        public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken)
        {
            await ReceiveActivityInternalAsync(turnContext, null, 0, cancellationToken).ConfigureAwait(false);
            await next(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task ReceiveActivityWithStatusAsync(ITurnContext turnContext, AgentCallbackHandler callback, CancellationToken cancellationToken)
        {
            return ReceiveActivityInternalAsync(turnContext, callback, 0, cancellationToken);
        }

        /// <summary>
        /// Gets an enumerator that iterates over a collection of implementations of <see cref="IMiddleware"/> objects.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate over the collection.</returns>
        public IEnumerator<IMiddleware> GetEnumerator()
        {
            return _middleware.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _middleware.GetEnumerator();
        }

        private Task ReceiveActivityInternalAsync(ITurnContext turnContext, AgentCallbackHandler callback, int nextMiddlewareIndex, CancellationToken cancellationToken)
        {
            // Check if we're at the end of the middleware list yet
            if (nextMiddlewareIndex == _middleware.Count)
            {
                // If all the middleware ran, the "leading edge" of the tree is now complete.
                // This means it's time to run any developer specified callback.
                // Once this callback is done, the "trailing edge" calls are then completed. This
                // allows code that looks like:
                //      Trace.TraceInformation("before");
                //      await next();
                //      Trace.TraceInformation("after");
                // to run as expected.

                // If a callback was provided invoke it now and return its task, otherwise just return the completed task
                return callback?.Invoke(turnContext, cancellationToken) ?? Task.CompletedTask;
            }

            // Get the next piece of middleware
            var nextMiddleware = _middleware[nextMiddlewareIndex];

            // Execute the next middleware passing a closure that will recurse back into this method at the next piece of middleware as the NextDelegate
            return nextMiddleware.OnTurnAsync(
                turnContext,
                (ct) => ReceiveActivityInternalAsync(turnContext, callback, nextMiddlewareIndex + 1, ct),
                cancellationToken);
        }
    }
}
