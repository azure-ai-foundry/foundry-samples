// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using System.Diagnostics.Metrics;

namespace Microsoft.Agents.Builder.Telemetry.Adapter
{
    /// <summary>
    /// Exposes the OpenTelemetry metric instruments used by the channel-adapter
    /// telemetry scopes to record activity processing statistics.
    /// </summary>
    /// <remarks>
    /// All instruments are created from <see cref="AgentsTelemetry.Meter"/> so they share
    /// the same source name and version as the rest of the SDK telemetry.
    /// </remarks>
    internal static class Metrics
    {
        /// <summary>
        /// Records the duration, in milliseconds, of processing a single incoming activity
        /// through the adapter pipeline.
        /// </summary>
        internal static Histogram<double> AdapterProcessDuration = AgentsTelemetry.Meter.CreateHistogram<double>(
            Constants.MetricAdapterProcessDuration,
            unit: "ms",
            description: "Duration of processing an activity in the adapter");

        /// <summary>
        /// Counts the total number of activities received by the agent.
        /// </summary>
        internal static Counter<long> ActivitiesReceived = AgentsTelemetry.Meter.CreateCounter<long>(
            Constants.MetricActivitiesReceived,
            unit: "activity",
            description: "Number of activities received by the adapter");

        /// <summary>
        /// Counts the total number of activities sent from the agent.
        /// </summary>
        internal static Counter<long> ActivitiesSent = AgentsTelemetry.Meter.CreateCounter<long>(
            Constants.MetricActivitiesSent,
            unit: "activity",
            description: "Number of activities sent by the adapter");

        /// <summary>
        /// Counts the total number of activities updated by the agent.
        /// </summary>
        internal static Counter<long> ActivitiesUpdated = AgentsTelemetry.Meter.CreateCounter<long>(
            Constants.MetricActivitiesUpdated,
            unit: "activity",
            description: "Number of activities updated by the adapter");

        /// <summary>
        /// Counts the total number of activities deleted by the agent.
        /// </summary>
        internal static Counter<long> ActivitiesDeleted = AgentsTelemetry.Meter.CreateCounter<long>(
            Constants.MetricActivitiesDeleted,
            unit: "activity",
            description: "Number of activities deleted by the adapter");
    }
}
