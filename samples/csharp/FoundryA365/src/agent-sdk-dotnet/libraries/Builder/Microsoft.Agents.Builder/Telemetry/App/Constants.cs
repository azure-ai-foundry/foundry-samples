// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Builder.Telemetry.App
{
    /// <summary>
    /// Defines the <see cref="System.Diagnostics.Activity"/> and metric names used by the agent application
    /// turn-processing telemetry scopes.
    /// </summary>
    internal static class Constants
    {
        /// <summary>Activity name for the overall turn execution.</summary>
        internal static readonly string ScopeOnTurn = "agents.app.run";

        /// <summary>Activity name for executing a matched route handler.</summary>
        internal static readonly string ScopeRouteHandler = "agents.app.route_handler";

        /// <summary>Activity name for the before-turn middleware pipeline.</summary>
        internal static readonly string ScopeBeforeTurn = "agents.app.before_turn";

        /// <summary>Activity name for the after-turn middleware pipeline.</summary>
        internal static readonly string ScopeAfterTurn = "agents.app.after_turn";

        /// <summary>Activity name for downloading file attachments during a turn.</summary>
        internal static readonly string ScopeDownloadFiles = "agents.app.download_files";

        /// <summary>Metric name for the counter of turns processed by the agent.</summary>
        internal static readonly string MetricTurnCount = "agents.turn.count";

        /// <summary>Metric name for the counter of turns that resulted in an error.</summary>
        internal static readonly string MetricTurnErrorCount = "agents.turn.error.count";

        /// <summary>Metric name for the histogram that records turn-processing duration in milliseconds.</summary>
        internal static readonly string MetricTurnDuration = "agents.turn.duration";
    }
}