// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using System;
using System.Diagnostics;

namespace Microsoft.Agents.Builder.Telemetry.App.Scopes
{
    /// <summary>
    /// A <see cref="TelemetryScope"/> that traces the full execution of a single agent turn.
    /// </summary>
    /// <remarks>
    /// Tags the <see cref="System.Diagnostics.Activity"/> with activity metadata (type, channel,
    /// conversation, activity ID) and authorization outcome. On successful completion, increments
    /// <see cref="Metrics.TurnCount"/> and records <see cref="Metrics.TurnDuration"/>; on error,
    /// increments <see cref="Metrics.TurnErrorCount"/> instead. Call <see cref="Share"/> to supply
    /// the route-authorization and route-match results after they are known.
    /// </remarks>
    internal class ScopeOnTurn : TelemetryScope
    {

        private readonly ITurnContext _turnContext;
        private bool? _routeAuthorized = null;
        private bool? _routeMatched = null;

        public ScopeOnTurn(ITurnContext turnContext) : base(Constants.ScopeOnTurn)
        {
            _turnContext = turnContext;
        }
        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception? error)
        {
            telemetryActivity.SetTag(TagNames.ActivityType, _turnContext.Activity.Type);
            telemetryActivity.SetTag(TagNames.ActivityChannelId, _turnContext.Activity.ChannelId?.ToString());
            telemetryActivity.SetTag(TagNames.ConversationId, _turnContext.Activity.Conversation?.Id);
            telemetryActivity.SetTag(TagNames.ActivityId, _turnContext.Activity.Id);
            telemetryActivity.SetTag(TagNames.RouteAuthorized, _routeAuthorized);
            telemetryActivity.SetTag(TagNames.RouteMatched, _routeMatched);

            TagList metricTags = new();
            metricTags.Add(TagNames.ActivityType, _turnContext.Activity.Type);
            metricTags.Add(TagNames.ActivityChannelId, _turnContext.Activity.ChannelId?.ToString());

            if (error == null)
            {
                Metrics.TurnCount.Add(1, metricTags);
                Metrics.TurnDuration.Record(duration, metricTags);
            }
            else
            {
                Metrics.TurnErrorCount.Add(1, metricTags);
            }
        }

        /// <summary>
        /// Associates route-authorization and route-match results with this scope so they
        /// can be recorded as span tags when the scope is disposed.
        /// </summary>
        /// <param name="routeAuthorized">Whether the incoming route was authorized.</param>
        /// <param name="routeMatched">Whether a route handler was matched for the incoming request.</param>
        public void Share(bool routeAuthorized, bool routeMatched)
        {
            _routeAuthorized = routeAuthorized;
            _routeMatched = routeMatched;
        }
    }
}