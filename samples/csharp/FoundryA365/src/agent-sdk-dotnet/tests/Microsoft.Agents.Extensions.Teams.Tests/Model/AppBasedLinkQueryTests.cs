// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Models;
using System.IO;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class AppBasedLinkQueryTests
    {
        [Fact]
        public void AppBasedLinkQueryInits()
        {
            var url = "http://example.com";
            var state = "magicCode";

            var appBasedLinkQuery = new AppBasedLinkQuery(url)
            {
                State = state
            };

            Assert.NotNull(appBasedLinkQuery);
            Assert.IsType<AppBasedLinkQuery>(appBasedLinkQuery);
            Assert.Equal(url, appBasedLinkQuery.Url);
            Assert.Equal(state, appBasedLinkQuery.State);
        }

        [Fact]
        public void AppBasedLinkQueryInitsWithNoArgs()
        {
            var appBasedLinkQuery = new AppBasedLinkQuery();

            Assert.NotNull(appBasedLinkQuery);
            Assert.IsType<AppBasedLinkQuery>(appBasedLinkQuery);
        }

        [Fact]
        public void AppBasedLinkQueryRoundTrip()
        {
            var url = "http://example.com";
            var state = "magicCode";

            var appBasedLinkQuery = new AppBasedLinkQuery(url)
            {
                State = state
            };

            // Known good
            var goodJson = LoadTestJson.LoadJson(appBasedLinkQuery);

            // Out
            var json = ProtocolJsonSerializer.ToJson(appBasedLinkQuery);
            Assert.Equal(goodJson, json);

            // In
            var inObj = ProtocolJsonSerializer.ToObject<AppBasedLinkQuery>(json);
            json = ProtocolJsonSerializer.ToJson(inObj);
            Assert.Equal(goodJson, json);
        }
    }
}
