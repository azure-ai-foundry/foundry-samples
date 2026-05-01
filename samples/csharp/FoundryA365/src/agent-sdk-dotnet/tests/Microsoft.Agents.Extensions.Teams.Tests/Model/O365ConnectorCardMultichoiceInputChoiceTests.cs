// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class O365ConnectorCardMultichoiceInputChoiceTests
    {
        [Fact]
        public void O365ConnectorCardMultichoiceInputChoiceInits()
        {
            var display = "C# in Depth";
            var value = "csharpInDepth";

            var choice = new O365ConnectorCardMultichoiceInputChoice(display, value);

            Assert.NotNull(choice);
            Assert.IsType<O365ConnectorCardMultichoiceInputChoice>(choice);
            Assert.Equal(display, choice.Display);
            Assert.Equal(value, choice.Value);
        }

        [Fact]
        public void O365ConnectorCardMultichoiceInputChoiceInitsWithNoArgs()
        {
            var choice = new O365ConnectorCardMultichoiceInputChoice();

            Assert.NotNull(choice);
            Assert.IsType<O365ConnectorCardMultichoiceInputChoice>(choice);
        }
    }
}
