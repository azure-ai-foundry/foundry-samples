// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Telemetry;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Builder.Telemetry.Adapter.Scopes
{
    /// <summary>
    /// A <see cref="TelemetryScope"/> that traces the deletion of an activity through
    /// the channel adapter.
    /// </summary>
    /// <remarks>
    /// Records the activity type and conversation identifier as span tags and increments
    /// the <see cref="Metrics.ActivitiesDeleted"/> counter.
    /// </remarks>
    internal class ScopeDeleteActivity : TelemetryScope
    {
        private readonly IActivity _activity;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeDeleteActivity"/> class.
        /// </summary>
        /// <param name="activity">The activity being deleted.</param>
        public ScopeDeleteActivity(IActivity activity) : base(Constants.ScopeDeleteActivity)
        {
            _activity = activity;
        }

        /// <inheritdoc />
        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception? error)
        {
            telemetryActivity.SetTag(TagNames.ActivityType, _activity.Type);
            telemetryActivity.SetTag(TagNames.ConversationId, _activity.Conversation?.Id);

            Metrics.ActivitiesDeleted.Add(
                1,
                new KeyValuePair<string, object?>(TagNames.ActivityChannelId, _activity.ChannelId)
            );
        }
    }
}
