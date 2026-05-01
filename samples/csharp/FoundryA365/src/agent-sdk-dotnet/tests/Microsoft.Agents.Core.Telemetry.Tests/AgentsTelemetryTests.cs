// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using Xunit;

namespace Microsoft.Agents.Core.Telemetry.Tests
{
    public class AgentsTelemetryTests
    {
        [Fact]
        public void ActivitySource_ShouldHaveCorrectNameAndVersion()
        {
            Assert.Equal(AgentsTelemetry.SourceName, AgentsTelemetry.ActivitySource.Name);
            Assert.Equal(AgentsTelemetry.SourceVersion, AgentsTelemetry.ActivitySource.Version);
        }

        [Fact]
        public void Meter_ShouldHaveCorrectNameAndVersion()
        {
            Assert.Equal(AgentsTelemetry.SourceName, AgentsTelemetry.Meter.Name);
            Assert.Equal(AgentsTelemetry.SourceVersion, AgentsTelemetry.Meter.Version);
        }
    }
}
