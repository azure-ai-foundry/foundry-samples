// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Agents.Core.Telemetry;
using Xunit;

namespace Microsoft.Agents.Core.Telemetry.Tests
{
    public class TelemetryUtilsTests
    {
        [Fact]
        public void FormatScopes_NullScopes_ReturnsUnknown()
        {
            var result = TelemetryUtils.FormatScopes(null);
            Assert.Equal(TelemetryUtils.Unknown, result);
        }

        [Fact]
        public void FormatScopes_EmptyScopes_ReturnsUnknown()
        {
            var result = TelemetryUtils.FormatScopes(new List<string>());
            Assert.Equal(TelemetryUtils.Unknown, result);
        }

        [Fact]
        public void FormatScopes_SingleScope_ReturnsScopeValue()
        {
            var scopes = new List<string> { "https://graph.microsoft.com/.default" };
            var result = TelemetryUtils.FormatScopes(scopes);
            Assert.Equal("https://graph.microsoft.com/.default", result);
        }

        [Fact]
        public void FormatScopes_MultipleScopes_ReturnsCommaSeparated()
        {
            var scopes = new List<string> { "scope1", "scope2", "scope3" };
            var result = TelemetryUtils.FormatScopes(scopes);
            Assert.Equal("scope1,scope2,scope3", result);
        }

        [Fact]
        public void Unknown_ShouldBeUnknownString()
        {
            Assert.Equal("unknown", TelemetryUtils.Unknown);
        }
    }
}
