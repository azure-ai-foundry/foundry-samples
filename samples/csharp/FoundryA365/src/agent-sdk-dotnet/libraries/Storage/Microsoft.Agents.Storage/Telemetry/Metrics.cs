// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using System.Diagnostics.Metrics;

namespace Microsoft.Agents.Storage.Telemetry
{
    /// <summary>
    /// Exposes the metric instruments used by the storage telemetry scopes
    /// to record operation statistics.
    /// </summary>
    /// <remarks>
    /// All instruments are created from <see cref="Microsoft.Agents.Core.Telemetry.AgentsTelemetry.Meter"/> so they share
    /// the same source name and version as the rest of the Agents SDK telemetry.
    /// </remarks>
    internal static class Metrics
    {
        /// <summary>
        /// Counts the total number of storage operations performed.
        /// </summary>
        internal static readonly Counter<long> OperationTotal = AgentsTelemetry.Meter.CreateCounter<long>(
            Constants.MetricOperationTotal,
            description: "Total number of storage operations performed.",
            unit: "operation");

        /// <summary>
        /// Records the duration, in milliseconds, of each storage operation.
        /// </summary>
        internal static readonly Histogram<double> OperationDuration = AgentsTelemetry.Meter.CreateHistogram<double>(
            Constants.MetricOperationDuration,
            description: "Duration of storage operations in milliseconds.",
            unit: "ms");
    }
}