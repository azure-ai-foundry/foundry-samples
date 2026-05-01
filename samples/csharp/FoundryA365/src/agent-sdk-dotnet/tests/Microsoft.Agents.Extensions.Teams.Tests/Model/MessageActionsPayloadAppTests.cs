// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class MessageActionsPayloadAppTests
    {
        [Fact]
        public void MessageActionPayloadAppInits()
        {
            var applicationIdentityType = "bot";
            var id = "spiffy-qna-bot-id";
            var displayName = "Contoso Bot";

            var messageActionPayloadApp = new MessageActionsPayloadApp(applicationIdentityType, id, displayName);

            Assert.NotNull(messageActionPayloadApp);
            Assert.IsType<MessageActionsPayloadApp>(messageActionPayloadApp);
            Assert.Equal(applicationIdentityType, messageActionPayloadApp.ApplicationIdentityType);
            Assert.Equal(id, messageActionPayloadApp.Id);
            Assert.Equal(displayName, messageActionPayloadApp.DisplayName);
        }

        [Fact]
        public void MessageActionPayloadAppInitsWithNoArgs()
        {
            var messageActionPayloadApp = new MessageActionsPayloadApp();

            Assert.NotNull(messageActionPayloadApp);
            Assert.IsType<MessageActionsPayloadApp>(messageActionPayloadApp);
        }
    }
}
