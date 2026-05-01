// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class ConversationReferenceTests
    {
        [Fact]
        public void ConversationReferenceRoundTrip()
        {
            var outReference = new ConversationReference()
            {
                ActivityId = "id",
                User = new ChannelAccount() { Id = "user" },
                Agent = new ChannelAccount() {  Id = "agent" },
                Conversation = new ConversationAccount() { Id = "conversation" },
                ChannelId = "channelId",
                Locale = "locale",
                ServiceUrl = "serviceUrl"
            };

            var outJson = ProtocolJsonSerializer.ToJson(outReference);

            // Specifically, should include: "bot": {} and not "agent": {}
            var outExpected = "{\"activityId\":\"id\",\"user\":{\"id\":\"user\"},\"bot\":{\"id\":\"agent\"},\"conversation\":{\"id\":\"conversation\"},\"channelId\":\"channelId\",\"serviceUrl\":\"serviceUrl\",\"locale\":\"locale\"}";

            Assert.Equal(outExpected, outJson);

            var inReference = ProtocolJsonSerializer.ToObject<ConversationReference>(outJson);
            Assert.Equal(outReference.ActivityId, inReference.ActivityId);
            Assert.Equal(outReference.User.Id, inReference.User.Id);
            Assert.Equal(outReference.Agent.Id, inReference.Agent.Id);
            Assert.Equal(outReference.Conversation.Id, inReference.Conversation.Id);
            Assert.Equal(outReference.ChannelId, inReference.ChannelId);
            Assert.Equal(outReference.Locale, inReference.Locale);
            Assert.Equal(outReference.ServiceUrl, inReference.ServiceUrl);
        }

        [Fact]
        public void InActivityConversationReferenceSerialize()
        {
            var activity = new Activity()
            {
                RelatesTo = new ConversationReference()
                {
                    ActivityId = "id",
                    User = new ChannelAccount() { Id = "user" },
                    Agent = new ChannelAccount() { Id = "agent" },
                    Conversation = new ConversationAccount() { Id = "conversation" },
                    ChannelId = "channelId",
                    Locale = "locale",
                    ServiceUrl = "serviceUrl"
                }
            };

            var outJson = ProtocolJsonSerializer.ToJson(activity);
#if SKIP_EMPTY_LISTS
            var outExpected = "{\"relatesTo\":{\"activityId\":\"id\",\"user\":{\"id\":\"user\"},\"bot\":{\"id\":\"agent\"},\"conversation\":{\"id\":\"conversation\"},\"channelId\":\"channelId\",\"serviceUrl\":\"serviceUrl\",\"locale\":\"locale\"}}";
#else
            var outExpected = "{\"membersAdded\":[],\"membersRemoved\":[],\"reactionsAdded\":[],\"reactionsRemoved\":[],\"attachments\":[],\"entities\":[],\"relatesTo\":{\"activityId\":\"id\",\"user\":{\"id\":\"user\"},\"bot\":{\"id\":\"agent\"},\"conversation\":{\"id\":\"conversation\"},\"channelId\":\"channelId\",\"serviceUrl\":\"serviceUrl\",\"locale\":\"locale\"},\"listenFor\":[],\"textHighlights\":[]}";
#endif
            Assert.Equal(outExpected, outJson);
        }
    }
}
