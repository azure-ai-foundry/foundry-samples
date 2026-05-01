// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Telemetry;
using System;
using System.Diagnostics;

namespace Microsoft.Agents.Builder.Telemetry.Adapter.Scopes
{
    /// <summary>
    /// A <see cref="TelemetryScope"/> that traces the processing of an incoming activity
    /// through the channel adapter pipeline.
    /// </summary>
    /// <remarks>
    /// Call <see cref="Share"/> to associate the incoming <see cref="IActivity"/> with this
    /// scope so that its metadata (type, channel, conversation, delivery mode) is recorded
    /// as span tags and metric dimensions when the scope is disposed.
    /// </remarks>
    internal class ScopeProcess : TelemetryScope
    {
        private IActivity? _activity = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeProcess"/> class.
        /// </summary>
        public ScopeProcess() : base(Constants.ScopeProcess)
        {
        }

        /// <inheritdoc />
        /// <remarks>
        /// Enriches the span with activity metadata tags and records
        /// <see cref="Metrics.AdapterProcessDuration"/> and <see cref="Metrics.ActivitiesReceived"/>.
        /// </remarks>
        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception? error)
        {
            TagList metricTagList = new();
            if (_activity != null)
            {
                telemetryActivity.SetTag(TagNames.ActivityType, _activity.Type);
                telemetryActivity.SetTag(TagNames.ActivityChannelId, _activity.ChannelId?.ToString());
                telemetryActivity.SetTag(TagNames.ActivityDeliveryMode, _activity.DeliveryMode);
                telemetryActivity.SetTag(TagNames.ConversationId, _activity.Conversation?.Id);
                telemetryActivity.SetTag(TagNames.IsAgentic, _activity.IsAgenticRequest());

                metricTagList.Add(TagNames.ActivityType, _activity.Type);
                metricTagList.Add(TagNames.ActivityChannelId, _activity.ChannelId?.ToString());
            }

            Metrics.AdapterProcessDuration.Record(duration, metricTagList);
            Metrics.ActivitiesReceived.Add(1, metricTagList);
        }

        /// <summary>
        /// Associates an incoming <see cref="IActivity"/> with this scope so its metadata
        /// can be recorded as tags on the underlying span.
        /// </summary>
        /// <param name="activity">The incoming activity being processed.</param>
        public void Share(IActivity activity)
        {
            _activity = activity;
        }
    }
}
