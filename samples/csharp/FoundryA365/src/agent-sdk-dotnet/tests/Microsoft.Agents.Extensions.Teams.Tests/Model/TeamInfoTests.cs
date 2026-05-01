// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class TeamInfoTests
    {
        [Fact]
        public void TeamInfoInits()
        {
            var id = "supportEngineers";
            var name = "Support Engineers";

            var teamInfo = new TeamInfo(id, name);

            Assert.NotNull(teamInfo);
            Assert.IsType<TeamInfo>(teamInfo);
            Assert.Equal(id, teamInfo.Id);
            Assert.Equal(name, teamInfo.Name);
        }

        [Fact]
        public void TeamInfoInitsWithNoArgs()
        {
            var teamInfo = new TeamInfo();

            Assert.NotNull(teamInfo);
            Assert.IsType<TeamInfo>(teamInfo);
        }
    }
}
