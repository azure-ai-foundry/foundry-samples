// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Connector;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests
{
    public class RetryActionTests
    {
        private int _runCount;

        [Fact]
        public async Task RunAsync_ShouldThrowOnNullTask()
        {
            Func<Task<int>> nullTask = null;

            await Assert.ThrowsAsync<ArgumentNullException>(() => RetryAction.RunAsync(
                task: nullTask,
                retryExceptionHandler: (ex, _retryCount) => HandleException(ex, _retryCount)));
        }

        [Fact]
        public async Task RunAsync_ShouldThrowOnNullRetryExceptionHandler()
        {
            Func<Exception, int, RetryParams> retryHandler = null;

            await Assert.ThrowsAsync<ArgumentNullException>(() => RetryAction.RunAsync(
                task: () => TestMethodSuccess(),
                retryExceptionHandler: retryHandler));
        }

        [Fact]
        public async Task RunAsync_ShouldExecuteMethodOnce()
        {
            _runCount = 0;

            await RetryAction.RunAsync(
                task: () => TestMethodSuccess(),
                retryExceptionHandler: (ex, _retryCount) => HandleException(ex, _retryCount));

            Assert.Equal(1, _runCount);
        }

        [Fact]
        public async Task RunAsync_ShouldRetryFailingMethod()
        {
            _runCount = 0;

            try
            {
                await RetryAction.RunAsync(
                    task: () => TestMethodFailure(),
                    retryExceptionHandler: (ex, _retryCount) => HandleException(ex, _retryCount));
            }
            catch (Exception ex)
            {
                Assert.Equal(3, _runCount);
                Assert.IsType<AggregateException>(ex);
                Assert.IsType<NotImplementedException>(((AggregateException)ex).InnerException);
            }
        }

        private async Task<bool> TestMethodSuccess()
        {
            _runCount++;
            return await Task.FromResult(true);
        }

        private Task<bool> TestMethodFailure()
        {
            _runCount++;
            throw new NotImplementedException("The operation has failed");
        }

        private static RetryParams HandleException(Exception ex, int currentRetryCount)
        {
            const int MAX_RETRIES = 2;

            if (currentRetryCount < MAX_RETRIES)
            {
                return RetryParams.DefaultBackOff(currentRetryCount);
            }
            else
            {
                return RetryParams.StopRetrying;
            }
        }
    }
}
