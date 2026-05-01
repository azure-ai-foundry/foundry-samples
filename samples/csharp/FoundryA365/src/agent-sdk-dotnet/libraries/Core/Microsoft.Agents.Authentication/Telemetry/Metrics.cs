using Microsoft.Agents.Core.Telemetry;
using System.Diagnostics.Metrics;

namespace Microsoft.Agents.Authentication.Telemetry
{
    /// <summary>
    /// Exposes the metric instruments used by the authentication telemetry
    /// scopes to record token-request statistics.
    /// </summary>
    /// <remarks>
    /// All instruments are created from <see cref="Microsoft.Agents.Core.Telemetry.AgentsTelemetry.Meter"/> so they share
    /// the same source name and version as the rest of the Agents SDK telemetry.
    /// </remarks>
    internal static class Metrics
    {
        /// <summary>
        /// Counts the total number of token requests made to the authentication service.
        /// </summary>
        internal static Counter<long> TokenRequestCount = AgentsTelemetry.Meter.CreateCounter<long>(
            Constants.MetricTokenRequestCount,
            unit: "request",
            description: "Number of token requests made to the authentication service");

        /// <summary>
        /// Records the duration, in milliseconds, of each token request to the authentication service.
        /// </summary>
        internal static Histogram<double> TokenRequestDuration = AgentsTelemetry.Meter.CreateHistogram<double>(
            Constants.MetricTokenRequestDuration,
            unit: "ms",
            description: "Duration of token requests to the authentication service in milliseconds");
    }
}