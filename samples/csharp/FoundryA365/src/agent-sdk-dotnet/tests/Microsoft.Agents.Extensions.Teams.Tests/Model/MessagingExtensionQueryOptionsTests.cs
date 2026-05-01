// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class MessagingExtensionQueryOptionsTests
    {
        [Fact]
        public void MessagingExtensionQueryOptionsInits()
        {
            var skip = 0;
            var count = 2;

            var msgExtQueryOptions = new MessagingExtensionQueryOptions(skip, count);

            Assert.NotNull(msgExtQueryOptions);
            Assert.IsType<MessagingExtensionQueryOptions>(msgExtQueryOptions);
            Assert.Equal(skip, msgExtQueryOptions.Skip);
            Assert.Equal(count, msgExtQueryOptions.Count);
        }

        [Fact]
        public void MessagingExtensionQueryOptionsInitsWithNoArgs()
        {
            var msgExtQueryOptions = new MessagingExtensionQueryOptions();

            Assert.NotNull(msgExtQueryOptions);
            Assert.IsType<MessagingExtensionQueryOptions>(msgExtQueryOptions);
        }
    }
}
