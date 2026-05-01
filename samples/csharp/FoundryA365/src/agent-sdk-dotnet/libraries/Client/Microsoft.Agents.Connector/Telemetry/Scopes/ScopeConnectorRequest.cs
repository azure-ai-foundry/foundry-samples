// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using System;
using System.Diagnostics;

#nullable enable

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    /// <summary>
    /// A <see cref="Microsoft.Agents.Core.Telemetry.TelemetryScope"/> that traces a connector REST client request and records
    /// conversation and activity identifiers as span tags together with request count and duration metrics.
    /// </summary>
    internal class ScopeConnectorRequest : TelemetryScope
    {
        private readonly string? _conversationId;
        private readonly string? _activityId;

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeConnectorRequest"/> class.
        /// </summary>
        /// <param name="scopeName">The name of the telemetry span.</param>
        /// <param name="conversationId">The conversation ID to associate with the span, or <see langword="null"/>.</param>
        /// <param name="activityId">The activity ID to associate with the span, or <see langword="null"/>.</param>
        public ScopeConnectorRequest(string scopeName, string? conversationId = null, string? activityId = null) : base(scopeName)
        {
            _conversationId = conversationId;
            _activityId = activityId;
        }

        protected override void Callback(Activity activity, double duration, Exception? exception)
        {
            if (_conversationId != null)
            {
                activity.SetTag(TagNames.ConversationId, _conversationId);
            }

            if (_activityId != null)
            {
                activity.SetTag(TagNames.ActivityId, _activityId);
            }

            Metrics.ConnectorRequestCount.Add(1);
            Metrics.ConnectorRequestDuration.Record(duration);
        }
    }
}
