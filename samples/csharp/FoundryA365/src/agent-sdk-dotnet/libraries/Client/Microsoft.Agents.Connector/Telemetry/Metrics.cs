// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;
using Microsoft.Agents.Core.Telemetry;

namespace Microsoft.Agents.Connector.Telemetry
{
    /// <summary>
    /// OpenTelemetry metric instruments for the connector and user-token REST clients.
    /// </summary>
    internal static class Metrics
    {
        internal static Counter<long> ConnectorRequestCount = AgentsTelemetry.Meter.CreateCounter<long>(
            Constants.MetricConnectorRequestCount,
            unit: "request",
            description: "Total number of requests made by connector clients"
            );

        internal static Histogram<double> ConnectorRequestDuration = AgentsTelemetry.Meter.CreateHistogram<double>(
            Constants.MetricConnectorRequestDuration,
            unit: "ms",
            description: "Duration of requests made by connector clients."
            );

        internal static Counter<long> UserTokenRestClientRequestCount = AgentsTelemetry.Meter.CreateCounter<long>(
            Constants.MetricUserTokenRestClientRequestCount,
            unit: "request",
            description: "Total number of requests made by user token rest client"
            );

        internal static Histogram<double> UserTokenRestClientRequestDuration = AgentsTelemetry.Meter.CreateHistogram<double>(
            Constants.MetricUserTokenRestClientRequestDuration,
            unit: "ms",
            description: "Duration of requests made by user token rest client."
            );
    }
}
