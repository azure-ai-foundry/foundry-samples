// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.HeaderPropagation;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue
{
    /// <summary>
    /// <see cref="Microsoft.Extensions.Hosting.BackgroundService"/> implementation used to process activities with claims.
    ///  <see href="https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.hosting.backgroundservice">More information.</see>
    /// </summary>
    internal class HostedActivityService : BackgroundService
    {
        private readonly ILogger<HostedActivityService> _logger;
        private readonly ReaderWriterLockSlim _lock = new();
        private readonly ConcurrentDictionary<ActivityWithClaims, Task> _activitiesProcessing = new();
        private readonly IActivityTaskQueue _activityQueue;
        private readonly int _shutdownTimeoutSeconds;
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Create a <see cref="Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue.HostedActivityService"/> instance for processing Activities
        /// on background threads.
        /// </summary>
        /// <remarks>
        /// It is important to note that exceptions on the background thread are only logged in the <see cref="Microsoft.Extensions.Logging.ILogger"/>.
        /// </remarks>
        /// <param name="provider"></param>
        /// <param name="config"><see cref="Microsoft.Extensions.Configuration.IConfiguration"/> used to retrieve ShutdownTimeoutSeconds from appsettings.</param>
        /// <param name="activityTaskQueue"><see cref="Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue.ActivityTaskQueue"/>Queue of activities to be processed.  This class
        /// contains a semaphore which the BackgroundService waits on to be notified of activities to be processed.</param>
        /// <param name="logger">Logger to use for logging BackgroundService processing and exception information.</param>
        /// <param name="options"></param>
        public HostedActivityService(IServiceProvider provider, IConfiguration config, IActivityTaskQueue activityTaskQueue, ILogger<HostedActivityService> logger, AdapterOptions options = null)
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(activityTaskQueue);
            ArgumentNullException.ThrowIfNull(provider);

            _shutdownTimeoutSeconds = options != null ? options.ShutdownTimeoutSeconds : 60;
            _activityQueue = activityTaskQueue;
            _logger = logger ?? NullLogger<HostedActivityService>.Instance;
            _serviceProvider = provider;
        }

        /// <summary>
        /// Called by BackgroundService when the hosting service is shutting down.
        /// </summary>
        /// <param name="stoppingToken"><see cref="System.Threading.CancellationToken"/> sent from BackgroundService for shutdown.</param>
        /// <returns>The Task to be executed asynchronously.</returns>
        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Queued Hosted Service is stopping.");

            _activityQueue.Stop();

            // Obtain a write lock and do not release it, preventing new tasks from starting
            if (_lock.TryEnterWriteLock(TimeSpan.FromSeconds(_shutdownTimeoutSeconds)))
            {
                // Wait for currently running tasks, but only n seconds.
                await Task.WhenAny(Task.WhenAll(_activitiesProcessing.Values), Task.Delay(TimeSpan.FromSeconds(_shutdownTimeoutSeconds), stoppingToken)).ConfigureAwait(false);
            }

            await base.StopAsync(stoppingToken).ConfigureAwait(false);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Queued Hosted Service is running.");

            await BackgroundProcessing(stoppingToken).ConfigureAwait(false);
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var activityWithClaims = await _activityQueue.WaitForActivityAsync(stoppingToken).ConfigureAwait(false);
                if (activityWithClaims != null)
                {
                    // The read lock will not be acquirable if the app is shutting down.
                    // New tasks should not be starting during shutdown.
                    if (_lock.TryEnterReadLock(500))
                    {
                        try
                        {
                            // Create the task which will execute the work item.
                            // CancellationToken.None: cleanup must always run regardless of shutdown state.
                            var task = GetTaskFromWorkItem(activityWithClaims, stoppingToken)
                                .ContinueWith(t =>
                                {
                                    _activitiesProcessing.TryRemove(activityWithClaims, out _);
                                }, CancellationToken.None);

                            _activitiesProcessing.TryAdd(activityWithClaims, task);
                        }
                        finally
                        {
                            _lock.ExitReadLock();
                        }
                    }
                    else
                    {
                        _logger.LogError("Work item for '{ConversationId}' not processed.  Server is shutting down?", activityWithClaims.Activity.Conversation.Id);
                    }
                }
            }
        }

        private async Task GetTaskFromWorkItem(ActivityWithClaims activityWithClaims, CancellationToken stoppingToken)
        {
            try
            {
                // We must go back through DI to get the IAgent. This is because the IAgent is typically transient, and anything
                // else that is transient as part of the Agent, that uses IServiceProvider will encounter error since that is scoped
                // and disposed before this gets called.
                var agent = _serviceProvider.GetService(activityWithClaims.AgentType ?? typeof(IAgent));
                agent ??= _serviceProvider.GetService(typeof(IAgent));

                HeaderPropagationContext.HeadersFromRequest = activityWithClaims.Headers;
                activityWithClaims.TelemetryActivity?.Start();
                try
                {
                    if (activityWithClaims.IsProactive)
                    {
                        await activityWithClaims.ChannelAdapter.ProcessProactiveAsync(
                            activityWithClaims.ClaimsIdentity,
                            activityWithClaims.Activity,
                            activityWithClaims.ProactiveAudience ?? activityWithClaims.ClaimsIdentity.GetOutgoingAudience(),
                            ((IAgent)agent).OnTurnAsync,
                            stoppingToken).ConfigureAwait(false);
                    }
                    else
                    {
                        var response = await activityWithClaims.ChannelAdapter.ProcessActivityAsync(
                            activityWithClaims.ClaimsIdentity,
                            activityWithClaims.Activity,
                            ((IAgent)agent).OnTurnAsync,
                            stoppingToken).ConfigureAwait(false);

                        if (activityWithClaims.OnComplete != null)
                        {
                            await activityWithClaims.OnComplete.Invoke(response).ConfigureAwait(false);
                        }
                    }
                }
                finally
                {
                    // make sure to close down any current activity once the turn is complete. 
                    activityWithClaims.TelemetryActivity?.Stop();
                }
            }
            catch (Exception ex)
            {
                // Agent Errors should be processed in the Adapter.OnTurnError.  Unlikely this will be hit.
                _logger.LogError(ex, "Error occurred executing WorkItem.");

                InvokeResponse invokeResponse = null;
                if (activityWithClaims.Activity.IsType(ActivityTypes.Invoke))
                {
                    invokeResponse = new InvokeResponse() { Status = (int)HttpStatusCode.InternalServerError };
                }

                if (activityWithClaims.OnComplete != null)
                {
                    await activityWithClaims.OnComplete(invokeResponse).ConfigureAwait(false);
                }
            }
        }
    }
}
