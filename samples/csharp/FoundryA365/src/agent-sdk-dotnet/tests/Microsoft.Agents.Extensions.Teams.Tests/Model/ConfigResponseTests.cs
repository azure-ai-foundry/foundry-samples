// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class ConfigResponseTests
    {
        [Fact]
        public void ConfigResponseInitsWithNoArgs()
        {
            var configResponse = new ConfigResponse<BotConfigAuth>();

            Assert.NotNull(configResponse);
            Assert.IsType<ConfigResponse<BotConfigAuth>>(configResponse);
        }
    }
}
