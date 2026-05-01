// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class O365ConnectorCardHttpPOSTTests
    {
        [Fact]
        public void O365ConnectorCardHttpPOSTInits()
        {
            var type = "HttpPOST";
            var name = "Order Yoga Pants";
            var id = "orderYogaPants";
            var body = ProtocolJsonSerializer.ToJson(new { color = "red" });

            var httpPOST = new O365ConnectorCardHttpPOST(type, name, id, body);

            Assert.NotNull(httpPOST);
            Assert.IsType<O365ConnectorCardHttpPOST>(httpPOST);
            Assert.Equal(name, httpPOST.Name);
            Assert.Equal(id, httpPOST.Id);
            Assert.Equal(body, httpPOST.Body);
        }

        [Fact]
        public void O365ConnectorCardHttpPOSTInitsWithNoArgs()
        {
            var httpPOST = new O365ConnectorCardHttpPOST();

            Assert.NotNull(httpPOST);
            Assert.IsType<O365ConnectorCardHttpPOST>(httpPOST);
        }
    }
}
