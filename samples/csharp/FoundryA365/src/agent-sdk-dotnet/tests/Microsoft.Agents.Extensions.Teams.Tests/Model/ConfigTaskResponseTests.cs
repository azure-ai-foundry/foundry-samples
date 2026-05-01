// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class ConfigTaskResponseTests
    {
        [Fact]
        public void ConfigTaskResponseInitWithNoArgs()
        {
            var configTaskResponse = new ConfigTaskResponse();

            Assert.NotNull(configTaskResponse);
            Assert.IsType<ConfigTaskResponse>(configTaskResponse);
            Assert.Equal("config", configTaskResponse.ResponseType);
        }
    }
}
