// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class MessagingExtensionParametersTests
    {
        [Fact]
        public void MessagingExtensionParametersInits()
        {
            var name = "pandaCount";
            var value = 3;

            var msgExtParams = new MessagingExtensionParameter(name, value);

            Assert.NotNull(msgExtParams);
            Assert.IsType<MessagingExtensionParameter>(msgExtParams);
            Assert.Equal(name, msgExtParams.Name);
            Assert.Equal(value, msgExtParams.Value);
        }

        [Fact]
        public void MessagingExtensionParametersInitsWithNoArgs()
        {
            var msgExtParams = new MessagingExtensionParameter();

            Assert.NotNull(msgExtParams);
            Assert.IsType<MessagingExtensionParameter>(msgExtParams);
        }
    }
}
