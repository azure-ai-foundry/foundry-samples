// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class MessageActionsPayloadConversationsTests
    {
        [Fact]
        public void MessageActionsPayloadConversationsInits()
        {
            var conversationIdentityType = "team";
            var id = "BFSE";
            var displayName = "Bot Framework Support Engineers";

            var msgActionsPayloadConversation = new MessageActionsPayloadConversation(conversationIdentityType, id, displayName);

            Assert.NotNull(msgActionsPayloadConversation);
            Assert.IsType<MessageActionsPayloadConversation>(msgActionsPayloadConversation);
            Assert.Equal(conversationIdentityType, msgActionsPayloadConversation.ConversationIdentityType);
            Assert.Equal(id, msgActionsPayloadConversation.Id);
            Assert.Equal(displayName, msgActionsPayloadConversation.DisplayName);
        }

        [Fact]
        public void MessageActionsPayloadConversationsInitsWithNoArgs()
        {
            var msgActionsPayloadConversation = new MessageActionsPayloadConversation();

            Assert.NotNull(msgActionsPayloadConversation);
            Assert.IsType<MessageActionsPayloadConversation>(msgActionsPayloadConversation);
        }
    }
}
