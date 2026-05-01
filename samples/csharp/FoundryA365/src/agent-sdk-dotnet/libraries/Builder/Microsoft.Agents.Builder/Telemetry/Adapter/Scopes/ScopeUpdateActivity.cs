// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Telemetry;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Builder.Telemetry.Adapter.Scopes
{
    /// <summary>
    /// A <see cref="TelemetryScope"/> that traces an update to an existing activity
    /// through the channel adapter.
    /// </summary>
    /// <remarks>
    /// Records the activity identifier and conversation identifier as span tags and
    /// increments the <see cref="Metrics.ActivitiesUpdated"/> counter.
    /// </remarks>
    internal class ScopeUpdateActivity : TelemetryScope
    {
        private readonly IActivity _activity;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeUpdateActivity"/> class.
        /// </summary>
        /// <param name="activity">The activity being updated.</param>
        public ScopeUpdateActivity(IActivity activity) : base(Constants.ScopeUpdateActivity)
        {
            _activity = activity;
        }

        /// <inheritdoc />
        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception? error)
        {
            telemetryActivity.SetTag(TagNames.ActivityId, _activity.Id);
            telemetryActivity.SetTag(TagNames.ConversationId, _activity.Conversation?.Id);

            Metrics.ActivitiesUpdated.Add(
                1,
                new KeyValuePair<string, object?>(TagNames.ActivityChannelId, _activity.ChannelId)
            );
        }
    }
}
