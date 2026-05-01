// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class ConversationParametersTests
    {
        [Fact]
        public void ConversationParametersRoundTrip()
        {
            var outConversation = new ConversationParameters()
            {
                IsGroup = false,
                Agent = new ChannelAccount() { Id = "agent" },
                Members = [new ChannelAccount() { Id = "member"} ],
                TopicName = "topicName",
                Activity = new Activity() { Type = "message" },
                TenantId = "tenantId"
            };

            var outJson = ProtocolJsonSerializer.ToJson(outConversation);

            // Specifically, should include: "bot": {} and not "agent": {}
#if SKIP_EMPTY_LISTS
            var outExpected = "{\"isGroup\":false,\"bot\":{\"id\":\"agent\"},\"members\":[{\"id\":\"member\"}],\"topicName\":\"topicName\",\"tenantId\":\"tenantId\",\"activity\":{\"type\":\"message\"}}";
#else
            var outExpected = "{\"isGroup\":false,\"bot\":{\"id\":\"agent\"},\"members\":[{\"id\":\"member\"}],\"topicName\":\"topicName\",\"tenantId\":\"tenantId\",\"activity\":{\"type\":\"message\",\"membersAdded\":[],\"membersRemoved\":[],\"reactionsAdded\":[],\"reactionsRemoved\":[],\"attachments\":[],\"entities\":[],\"listenFor\":[],\"textHighlights\":[]}}";
#endif

            Assert.Equal(outExpected, outJson);

            var inConversation = ProtocolJsonSerializer.ToObject<ConversationParameters>(outJson);
            Assert.Equal(outConversation.Agent.Id, inConversation.Agent.Id);
        }
    }
}
