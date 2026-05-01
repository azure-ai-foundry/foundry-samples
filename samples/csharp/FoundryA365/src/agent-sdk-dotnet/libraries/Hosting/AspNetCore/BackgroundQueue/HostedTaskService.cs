// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue
{
    /// <summary>
    /// <see cref="Microsoft.Extensions.Hosting.BackgroundService"/> implementation used to process work items on background threads.
    /// See <see href="https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.hosting.backgroundservice">BackgroundService</see> for more information.
    /// </summary>
    internal class HostedTaskService : BackgroundService
    {
        private readonly ILogger<HostedTaskService> _logger;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private readonly ConcurrentDictionary<Func<CancellationToken,Task>, Task> _tasks = new();
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly int _shutdownTimeoutSeconds;

        /// <summary>
        /// Create a <see cref="Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue.HostedTaskService"/> instance for processing work on a background thread.
        /// </summary>
        /// <remarks>
        /// It is important to note that exceptions on the background thread are only logged in the <see cref="Microsoft.Extensions.Logging.ILogger"/>.
        /// </remarks>
        /// <param name="taskQueue"><see cref="Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue.ActivityTaskQueue"/> implementation where tasks are queued to be processed.</param>
        /// <param name="logger"><see cref="Microsoft.Extensions.Logging.ILogger"/> implementation, for logging including background thread exception information.</param>
        /// <param name="options"></param>
        public HostedTaskService(IBackgroundTaskQueue taskQueue, ILogger<HostedTaskService> logger, AdapterOptions options = null)
        {
            ArgumentNullException.ThrowIfNull(taskQueue);

            _shutdownTimeoutSeconds = options != null ? options.ShutdownTimeoutSeconds : 60;
            _taskQueue = taskQueue;
            _logger = logger ?? NullLogger<HostedTaskService>.Instance;;
        }

        /// <summary>
        /// Called by BackgroundService when the hosting service is shutting down.
        /// </summary>
        /// <param name="stoppingToken"><see cref="System.Threading.CancellationToken"/> sent from BackgroundService for shutdown.</param>
        /// <returns>The Task to be executed asynchronously.</returns>
        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Queued Hosted Service is stopping.");

            // Obtain a write lock and do not release it, preventing new tasks from starting
            if (_lock.TryEnterWriteLock(TimeSpan.FromSeconds(_shutdownTimeoutSeconds)))
            {
                // Wait for currently running tasks, but only n seconds.
                await Task.WhenAny(Task.WhenAll(_tasks.Values), Task.Delay(TimeSpan.FromSeconds(_shutdownTimeoutSeconds), stoppingToken)).ConfigureAwait(false);
            }

            await base.StopAsync(stoppingToken).ConfigureAwait(false);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Queued Hosted Service is running.{Environment.NewLine}", Environment.NewLine);
            
            await BackgroundProcessing(stoppingToken).ConfigureAwait(false);
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var workItem = await _taskQueue.DequeueAsync(stoppingToken).ConfigureAwait(false);
                if (workItem != null)
                {
                    try
                    {
                        // The read lock will not be acquirable if the app is shutting down.
                        // New tasks should not be starting during shutdown.
                        if (_lock.TryEnterReadLock(500))
                        {
                            var task = workItem(stoppingToken) //GetTaskFromWorkItem(workItem, stoppingToken)
                                .ContinueWith(t =>
                                {
                                    // After the work item completes, clear the running tasks of all completed tasks.
                                    foreach (var kv in _tasks.Where(tsk => tsk.Value.IsCompleted))
                                    {
                                        _tasks.TryRemove(kv.Key, out Task removed);
                                    }
                                }, stoppingToken);

                            _tasks.TryAdd(workItem, task);
                        }
                        else
                        {
                            _logger.LogError("Work item not processed.  Server is shutting down.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error occurred executing WorkItem.");
                    }
                    finally
                    {
                        _lock.ExitReadLock();
                    }
                }
            }
        }
    }
}
