// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Telemetry;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue
{
    /// <summary>
    /// Singleton queue, used to transfer an ActivityWithClaims to the <see cref="Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue.HostedActivityService"/>.
    /// </summary>
    internal class ActivityTaskQueue : IActivityTaskQueue
    {
        private readonly SemaphoreSlim _signal = new(0);
        private readonly EventWaitHandle _queueEmpty = new(true, EventResetMode.ManualReset);
        private readonly ConcurrentQueue<ActivityWithClaims> _activities = new();
        private volatile bool _stopped = false;

        /// <inheritdoc/>
        public bool QueueBackgroundActivity(ClaimsIdentity claimsIdentity, IChannelAdapter adapter, IActivity activity, bool proactive = false, string proactiveAudience = null, Type agentType = null, Func<InvokeResponse, Task> onComplete = null, IHeaderDictionary headers = null)
        {
            ArgumentNullException.ThrowIfNull(claimsIdentity);
            ArgumentNullException.ThrowIfNull(adapter);
            ArgumentNullException.ThrowIfNull(activity);

            if (_stopped)
            {
                return false;
            }
            
            // Copy to prevent unexpected side effects from later mutations of the original headers.
            var copyHeaders = headers != null ? new HeaderDictionary(headers.ToDictionary()) : [];

            _activities.Enqueue(new ActivityWithClaims 
            { 
                ChannelAdapter = adapter, 
                AgentType = agentType, 
                ClaimsIdentity = claimsIdentity, 
                Activity = activity, 
                IsProactive = proactive, 
                ProactiveAudience = proactiveAudience, 
                OnComplete = onComplete, 
                Headers = copyHeaders,
                TelemetryActivity = System.Diagnostics.Activity.Current?.CloneActivity()
            });
            _queueEmpty.Reset();
            _signal.Release();
            return true;
        }

        /// <inheritdoc/>
        public async Task<ActivityWithClaims> WaitForActivityAsync(CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken);

            _activities.TryDequeue(out ActivityWithClaims dequeued);
            if (_activities.IsEmpty)
            {
                _queueEmpty.Set();
            }

            return dequeued;
        }

        public void Stop(bool waitForEmpty = true)
        {
            _stopped = true;
            if (waitForEmpty)
            {
                _queueEmpty.WaitOne();
            }
        }
    }
}
