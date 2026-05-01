// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.Connector
{
    /// <summary>
    /// Retries asynchronous operations. In case of errors, it collects and returns exceptions in an AggregateException object.
    /// </summary>
    internal static class RetryAction
    {
        /// <summary>
        /// Starts the retry of the action requested.
        /// </summary>
        /// <typeparam name="TResult">The result expected from the action performed.</typeparam>
        /// <param name="task">A reference to the action to retry.</param>
        /// <param name="retryExceptionHandler">A reference to the method that handles exceptions.</param>
        /// <returns>A result object.</returns>
        public static async Task<TResult> RunAsync<TResult>(Func<Task<TResult>> task, Func<Exception, int, RetryParams> retryExceptionHandler)
        {
            AssertionHelpers.ThrowIfNull(task, nameof(task));
            AssertionHelpers.ThrowIfNull(retryExceptionHandler, nameof(retryExceptionHandler));

            RetryParams retry;
            var exceptions = new List<Exception>();
            var currentRetryCount = 0;

            do
            {
                try
                {
                    return await task().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    retry = retryExceptionHandler(ex, currentRetryCount);
                }

                if (retry.ShouldRetry)
                {
                    currentRetryCount++;
                    await Task.Delay(retry.RetryAfter.WithJitter()).ConfigureAwait(false);
                }
            }
            while (retry.ShouldRetry);

            throw new AggregateException("Failed to perform the required operation.", exceptions);
        }
    }
}
