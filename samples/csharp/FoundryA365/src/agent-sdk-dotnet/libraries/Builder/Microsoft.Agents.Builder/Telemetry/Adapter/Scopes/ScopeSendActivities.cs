// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Telemetry;
using System;
using System.Linq;

namespace Microsoft.Agents.Builder.Telemetry.Adapter.Scopes
{
    /// <summary>
    /// A <see cref="TelemetryScope"/> that traces the sending of one or more outgoing
    /// activities through the channel adapter.
    /// </summary>
    /// <remarks>
    /// Records the batch count, conversation identifier, and increments the
    /// <see cref="Metrics.ActivitiesSent"/> counter for each activity in the batch.
    /// </remarks>
    internal class ScopeSendActivities : TelemetryScope
    {
        private readonly IActivity[] _activities;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeSendActivities"/> class.
        /// </summary>
        /// <param name="activities">The outgoing activities being sent.</param>
        public ScopeSendActivities(IActivity[] activities) : base(Constants.ScopeSendActivities)
        {
            _activities = activities;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Tags the span with the activity count and conversation identifier, and
        /// increments <see cref="Metrics.ActivitiesSent"/> once per activity.
        /// </remarks>
        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception? error)
        {
            if (_activities == null || _activities.Length == 0)
            {
                return;
            }

            int count = _activities.Length;
            telemetryActivity.SetTag(TagNames.ActivityCount, count);
            telemetryActivity.SetTag(TagNames.ConversationId, _activities.First().Conversation?.Id);

            foreach (var activity in _activities)
            {
                Metrics.ActivitiesSent.Add(
                    1,
                    new(TagNames.ActivityType, activity.Type),
                    new(TagNames.ActivityChannelId, activity.ChannelId)
                );
            }
        }
    }
}
