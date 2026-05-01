// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Connector;
using System;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests
{
    public class RetryParamTests
    {
        [Fact]
        public void Constructor_ShouldStopRetrying()
        {
            var retryParams = RetryParams.StopRetrying;
            Assert.False(retryParams.ShouldRetry);
        }

        [Fact]
        public void Constructor_ShouldRetryByDefault()
        {
            var retryParams = RetryParams.DefaultBackOff(0);
            Assert.True(retryParams.ShouldRetry);
            Assert.Equal(TimeSpan.FromMilliseconds(50), retryParams.RetryAfter);
        }

        [Fact]
        public void Constructor_ShouldEnforceOnMaxRetries()
        {
            var retryParams = RetryParams.DefaultBackOff(10);
            Assert.False(retryParams.ShouldRetry);
        }

        [Fact]
        public void Constructor_ShouldEnforceOnMaxDelay()
        {
            var retryParams = new RetryParams(TimeSpan.FromSeconds(11), true);
            Assert.Equal(TimeSpan.FromSeconds(10), retryParams.RetryAfter);
        }
    }
}
