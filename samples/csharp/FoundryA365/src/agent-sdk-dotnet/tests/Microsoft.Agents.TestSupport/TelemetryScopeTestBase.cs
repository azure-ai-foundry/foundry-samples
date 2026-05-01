// Copyright (c) Microsoft Corporation. All rights reserved.
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.TestSupport
{
    /// <summary>
    /// Base class for telemetry scope tests. Registers an <see cref="System.Diagnostics.ActivityListener"/>
    /// scoped to <see cref="AgentsTelemetry.SourceName"/> for the duration of each test and exposes
    /// the collected started/stopped <see cref="System.Diagnostics.Activity"/> instances.
    /// </summary>
    public abstract class TelemetryScopeTestBase : IDisposable
    {
        private readonly System.Diagnostics.ActivityListener _listener;

        protected readonly List<System.Diagnostics.Activity> StartedActivities = new();
        protected readonly List<System.Diagnostics.Activity> StoppedActivities = new();

        protected TelemetryScopeTestBase()
        {
            _listener = new System.Diagnostics.ActivityListener
            {
                ShouldListenTo = source => source.Name == AgentsTelemetry.SourceName,
                Sample = (ref System.Diagnostics.ActivityCreationOptions<System.Diagnostics.ActivityContext> options) =>
                    System.Diagnostics.ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => StartedActivities.Add(activity),
                ActivityStopped = activity => StoppedActivities.Add(activity)
            };
            System.Diagnostics.ActivitySource.AddActivityListener(_listener);
            StartedActivities.Clear();
            StoppedActivities.Clear();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _listener.ActivityStarted = null;
                _listener.ActivityStopped = null;
                _listener.Dispose();
            }
        }
    }
}
