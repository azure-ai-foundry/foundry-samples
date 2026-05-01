// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Storage.Telemetry
{
    /// <summary>
    /// Defines the <see cref="System.Diagnostics.Activity"/> and metric names used by the storage telemetry scopes.
    /// </summary>
    internal static class Constants
    {
        /// <summary>Activity name for a storage read operation.</summary>
        internal static readonly string ScopeRead = "agents.storage.read";

        /// <summary>Activity name for a storage write operation.</summary>
        internal static readonly string ScopeWrite = "agents.storage.write";

        /// <summary>Activity name for a storage delete operation.</summary>
        internal static readonly string ScopeDelete = "agents.storage.delete";

        /// <summary>Metric name for the counter of total storage operations performed.</summary>
        internal static readonly string MetricOperationTotal = "agents.storage.operation.total";

        /// <summary>Metric name for the histogram that records storage operation duration in milliseconds.</summary>
        internal static readonly string MetricOperationDuration = "agents.storage.operation.duration";

        /// <summary>Operation name for a read.</summary>
        internal static readonly string OperationRead = "read";

        /// <summary>Operation name for a write.</summary>
        internal static readonly string OperationWrite = "write";

        /// <summary>Operation name for a delete.</summary>
        internal static readonly string OperationDelete = "delete";
    }
}