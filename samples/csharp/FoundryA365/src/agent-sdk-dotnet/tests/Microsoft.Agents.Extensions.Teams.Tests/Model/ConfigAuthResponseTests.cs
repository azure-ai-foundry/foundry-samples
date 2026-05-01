// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class ConfigAuthResponseTests
    {
        [Fact]
        public void ConfigAuthResponseInitWithNoArgs()
        {
            var configAuthResponse = new ConfigAuthResponse();

            Assert.NotNull(configAuthResponse);
            Assert.IsType<ConfigAuthResponse>(configAuthResponse);
            Assert.Equal("config", configAuthResponse.ResponseType);
        }
    }
}
