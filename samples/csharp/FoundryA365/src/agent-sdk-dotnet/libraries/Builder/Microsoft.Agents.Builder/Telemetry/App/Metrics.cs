// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using System.Diagnostics.Metrics;

namespace Microsoft.Agents.Builder.Telemetry.App
{
    /// <summary>
    /// Exposes the metric instruments used by the agent application
    /// turn-processing telemetry scopes.
    /// </summary>
    /// <remarks>
    /// All instruments are created from <see cref="AgentsTelemetry.Meter"/> so they share
    /// the same source name and version as the rest of the Agents SDK telemetry.
    /// </remarks>
    internal static class Metrics
    {
        /// <summary>
        /// Counts the total number of turns successfully processed by the agent.
        /// </summary>
        internal static Counter<long> TurnCount = AgentsTelemetry.Meter.CreateCounter<long>(
            Constants.MetricTurnCount,
            unit: "turn",
            description: "Number of turns processed by the agent");

        /// <summary>
        /// Counts the total number of turns that resulted in an error.
        /// </summary>
        internal static Counter<long> TurnErrorCount = AgentsTelemetry.Meter.CreateCounter<long>(
            Constants.MetricTurnErrorCount,
            unit: "turn",
            description: "Number of turns that resulted in an error");

        /// <summary>
        /// Records the duration, in milliseconds, of processing each turn.
        /// </summary>
        internal static Histogram<double> TurnDuration = AgentsTelemetry.Meter.CreateHistogram<double>(
            Constants.MetricTurnDuration,
            unit: "ms",
            description: "Duration of processing a turn in milliseconds");
    }
}