// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class MessagingExtensionResponseTests
    {
        [Fact]
        public void MessagingExtensionResponseInits()
        {
            var composeExtension = new MessagingExtensionResult("list", "message", null, null, "happy dance");
            var cacheInfo = new CacheInfo();

            var msgExtResponse = new MessagingExtensionResponse(composeExtension)
            {
                CacheInfo = cacheInfo
            };

            Assert.NotNull(msgExtResponse);
            Assert.IsType<MessagingExtensionResponse>(msgExtResponse);
            Assert.Equal(composeExtension, msgExtResponse.ComposeExtension);
            Assert.Equal(cacheInfo, msgExtResponse.CacheInfo);
        }

        [Fact]
        public void MessagingExtensionResponseInitsWithNoArgs()
        {
            var msgExtResponse = new MessagingExtensionResponse();

            Assert.NotNull(msgExtResponse);
            Assert.IsType<MessagingExtensionResponse>(msgExtResponse);
        }
    }
}
