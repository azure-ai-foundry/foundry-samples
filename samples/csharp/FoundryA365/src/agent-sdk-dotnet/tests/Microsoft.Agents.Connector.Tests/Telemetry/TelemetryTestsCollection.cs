// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Xunit;

namespace Microsoft.Agents.Connector.Tests.Telemetry
{
    /// <summary>
    /// Disables parallel execution across all telemetry-scope test classes in this assembly
    /// so that the global <see cref="System.Diagnostics.ActivitySource"/> listener registered
    /// by each class does not capture activities emitted by another class running concurrently.
    /// </summary>
    [CollectionDefinition("TelemetryTests", DisableParallelization = true)]
    public class TelemetryTestsCollection { }
}
