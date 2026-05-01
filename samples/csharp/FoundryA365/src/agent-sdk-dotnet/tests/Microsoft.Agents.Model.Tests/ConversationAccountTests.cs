// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class ConversationAccountTests
    {
        [Fact]
        public void ConversationAccount_RoundTrip()
        {
            var conversationAccount = new ConversationAccount
            {
                Id = "conversation-id",
                Name = "Conversation Name",
                IsGroup = false,
                ConversationType = "personal",
                TenantId = "tenant-id"
            };

            var goodJson = LoadTestJson.LoadJson(conversationAccount);

            // Out
            var outJson = ProtocolJsonSerializer.ToJson(conversationAccount);

            Assert.Equal(goodJson, outJson);

            // In
            var inObj = ProtocolJsonSerializer.ToObject<ConversationAccount>(outJson);
            Assert.Equal(conversationAccount.Id, inObj.Id);
        }

        [Fact]
        public void ConversationAccount_RoundTripExtension()
        {
            var goodJson = "{\"isGroup\":false,\"conversationType\":\"personal\",\"tenantId\":\"tenant-id\",\"id\":\"conversation-id\",\"name\":\"Conversation Name\",\"extensionProperty\":\"extensionValue\"}";
            var conversationAccount = ProtocolJsonSerializer.ToObject<ConversationAccount>(goodJson);

            Assert.Equal("conversation-id", conversationAccount.Id);
            Assert.Equal("extensionValue", conversationAccount.Properties["extensionProperty"].GetString()); 

            // Out
            var outJson = ProtocolJsonSerializer.ToJson(conversationAccount);

            Assert.Equal(goodJson, outJson);

            // In
            var inObj = ProtocolJsonSerializer.ToObject<ConversationAccount>(outJson);
            Assert.Equal(conversationAccount.Id, inObj.Id);
            Assert.Equal("extensionValue", inObj.Properties["extensionProperty"].GetString());
        }
    }
}
