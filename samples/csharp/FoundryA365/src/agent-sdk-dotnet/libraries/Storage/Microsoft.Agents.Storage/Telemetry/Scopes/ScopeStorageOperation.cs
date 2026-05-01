// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using System;
using System.Diagnostics;

#nullable enable

namespace Microsoft.Agents.Storage.Telemetry.Scopes
{
    /// <summary>
    /// A <see cref="Microsoft.Agents.Core.Telemetry.TelemetryScope"/> that traces a single storage operation and records
    /// associated metrics.
    /// </summary>
    /// <remarks>
    /// Derived classes supply the <see cref="System.Diagnostics.Activity"/> name and operation
    /// type via the constructor. When disposed, the scope tags the activity with the key count
    /// and operation name, and records values for <see cref="Microsoft.Agents.Storage.Telemetry.Metrics.OperationDuration"/> and
    /// <see cref="Microsoft.Agents.Storage.Telemetry.Metrics.OperationTotal"/>.
    /// </remarks>
    public class ScopeStorageOperation : TelemetryScope
    {
        private readonly string _operationName;
        private readonly int _keyCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Storage.Telemetry.Scopes.ScopeStorageOperation"/> class.
        /// </summary>
        /// <param name="scopeName">The name for the underlying <see cref="System.Diagnostics.Activity"/>.</param>
        /// <param name="operationName">The logical storage operation name (e.g., "read", "write", "delete").</param>
        /// <param name="keyCount">The number of storage keys involved in the operation.</param>
        public ScopeStorageOperation(string scopeName, string operationName, int keyCount) : base(scopeName)
        {
            _operationName = operationName;
            _keyCount = keyCount;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Tags the activity with <see cref="Microsoft.Agents.Core.Telemetry.TagNames.KeyCount"/> and <see cref="Microsoft.Agents.Core.Telemetry.TagNames.StorageOperation"/>,
        /// then records <see cref="Microsoft.Agents.Storage.Telemetry.Metrics.OperationDuration"/> and increments <see cref="Microsoft.Agents.Storage.Telemetry.Metrics.OperationTotal"/>.
        /// </remarks>
        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception? exception)
        {
            telemetryActivity.SetTag(TagNames.KeyCount, _keyCount);
            telemetryActivity.SetTag(TagNames.StorageOperation, _operationName);

            TagList metricTags = new();
            metricTags.Add(TagNames.StorageOperation, _operationName);

            Metrics.OperationDuration.Record(duration, metricTags);
            Metrics.OperationTotal.Add(1, metricTags);
        }
    }
}