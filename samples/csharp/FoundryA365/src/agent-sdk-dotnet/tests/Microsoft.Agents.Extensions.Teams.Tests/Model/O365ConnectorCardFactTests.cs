// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class O365ConnectorCardFactTests
    {
        [Fact]
        public void O365ConnectorCardFactInits()
        {
            var name = "BugId";
            var value = "1234";

            var fact = new O365ConnectorCardFact(name, value);

            Assert.NotNull(fact);
            Assert.IsType<O365ConnectorCardFact>(fact);
            Assert.Equal(name, fact.Name);
            Assert.Equal(value, fact.Value);
        }

        [Fact]
        public void O365ConnectorCardFactInitsWithNoArgs()
        {
            var fact = new O365ConnectorCardFact();

            Assert.NotNull(fact);
            Assert.IsType<O365ConnectorCardFact>(fact);
        }
    }
}
