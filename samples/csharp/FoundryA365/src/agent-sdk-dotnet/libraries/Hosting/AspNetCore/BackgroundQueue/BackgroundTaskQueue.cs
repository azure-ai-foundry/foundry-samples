// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
using Microsoft.Agents.Core.Telemetry;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue
{
    /// <summary>
    /// Singleton queue, used to transfer a work item to the <see cref="Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue.HostedTaskService"/>.
    /// </summary>
    internal class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private readonly ConcurrentQueue<WorkItem> _workItems = new();
        private readonly SemaphoreSlim _signal = new(0);

        /// <summary>
        /// Enqueue a work item to be processed on a background thread.
        /// </summary>
        /// <param name="work">The work item to be enqueued for execution. Is defined as
        /// a function taking a cancellation token.</param>
        public void QueueBackgroundWorkItem(Func<CancellationToken, Task> work)
        {
            ArgumentNullException.ThrowIfNull(work);

            _workItems.Enqueue(new WorkItem() { Process = work, TelemetryActivity = System.Diagnostics.Activity.Current?.CloneActivity() });
            _signal.Release();
        }

        /// <summary>
        /// Wait for a signal of an enqueued work item to be processed.
        /// </summary>
        /// <param name="cancellationToken">CancellationToken used to cancel the wait.</param>
        /// <returns>A function taking a cancellation token that needs to be processed.
        /// </returns>
        public async Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken);

            if (!_workItems.TryDequeue(out WorkItem dequeued) || dequeued is null)
            {
                // In case of a race where the queue is empty, return a no-op work item.
                return _ => Task.CompletedTask;
            }
            dequeued.TelemetryActivity?.Start();
            return dequeued.Process;
        }
    }

    internal class WorkItem
    {
        public Func<CancellationToken, Task> Process { get; set; }

        /// <summary>
        /// Holds the <see cref="System.Diagnostics.Activity"/> used for distributed tracing and
        /// telemetry correlation, cloned from the original request context.
        /// </summary>
        public System.Diagnostics.Activity TelemetryActivity { get; set; }
    }
}
