// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.Tests.BackgroundTaskService
{
    public class BackgroundTaskQueueTests
    {
        [Fact]
        public async Task DequeueAsync_ShouldResolveQueuedTask()
        {
            var queue = new BackgroundTaskQueue();
            var resolved = false;

            queue.QueueBackgroundWorkItem((cancellationToken) =>
            {
                resolved = true;
                return Task.FromResult(0);
            });

            var dequeued = await queue.DequeueAsync(CancellationToken.None);
            await dequeued(CancellationToken.None);

            Assert.True(resolved);
        }
    }
}