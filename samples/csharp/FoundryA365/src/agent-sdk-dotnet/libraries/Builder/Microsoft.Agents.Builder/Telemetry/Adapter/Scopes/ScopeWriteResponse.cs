// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Telemetry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Agents.Builder.Telemetry.Adapter.Scopes
{
    internal class ScopeWriteResponse : TelemetryScope
    {
        private readonly IList<IActivity> _activities;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeWriteResponse"/> class.
        /// </summary>
        /// <param name="activities">The outgoing activities being sent.</param>
        public ScopeWriteResponse(IList<IActivity>? activities = null) : base(Constants.ScopeWriteResponse)
        {
            _activities = activities ?? new List<IActivity>();
        }

        public ScopeWriteResponse(IActivity activity) : base(Constants.ScopeWriteResponse)
        {
            _activities = new List<IActivity>();
            _activities.Add(activity);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Tags the span with the activity count and conversation identifier, and
        /// increments <see cref="Metrics.ActivitiesSent"/> once per activity.
        /// </remarks>
        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception? error)
        {
            if (_activities.Count == 0)
            {
                return;
            }

            int count = _activities.Count;
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
