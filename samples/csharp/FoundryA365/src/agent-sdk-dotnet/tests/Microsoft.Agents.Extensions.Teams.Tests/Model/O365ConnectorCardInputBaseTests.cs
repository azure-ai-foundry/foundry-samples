// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class O365ConnectorCardInputBaseTests
    {
        [Fact]
        public void O365ConnectorCardInputBaseInits()
        {
            var type = "textInput";
            var id = "firstName";
            var isRequired = true;
            var title = "Profile";
            var value = "First Name";

            var inputBase = new O365ConnectorCardInputBase(type, id, isRequired, title, value);

            Assert.NotNull(inputBase);
            Assert.IsType<O365ConnectorCardInputBase>(inputBase);
            Assert.Equal(type, inputBase.Type);
            Assert.Equal(id, inputBase.Id);
            Assert.Equal(isRequired, inputBase.IsRequired);
            Assert.Equal(title, inputBase.Title);
            Assert.Equal(value, inputBase.Value);
        }

        [Fact]
        public void O365ConnectorCardInputBaseInitsWithNoArgs()
        {
            var inputBase = new O365ConnectorCardInputBase();

            Assert.NotNull(inputBase);
            Assert.IsType<O365ConnectorCardInputBase>(inputBase);
        }
    }
}
