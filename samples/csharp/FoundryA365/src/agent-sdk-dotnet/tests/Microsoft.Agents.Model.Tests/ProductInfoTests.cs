// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    [Collection("ProductInfo Collection")]
    public class ProductInfoTests()
    {
        [Fact]
        public void ChannelIdTests()
        {
            // Is Teams?
            string msTeamsJson = "{\"channelId\":\"msteams\"}";
            var activity = ProtocolJsonSerializer.ToObject<IActivity>(msTeamsJson);
            Assert.True(Channels.Msteams == activity.ChannelId);

            // Case insensitive
            msTeamsJson = "{\"channelId\":\"mStEaMs\"}";
            activity = ProtocolJsonSerializer.ToObject<IActivity>(msTeamsJson);
            Assert.True(Channels.Msteams == activity.ChannelId);

            // ChannelId construction
            var channelId = new ChannelId(Channels.Msteams);
            Assert.Equal(Channels.Msteams, channelId);

            channelId = new ChannelId("1:2:3");
            Assert.Equal("1", channelId.Channel);
            Assert.Equal("2:3", channelId.SubChannel);
            Assert.Equal("1:2:3", channelId);

            // Can change SubChannel
            channelId = new ChannelId(Channels.M365Copilot);
            Assert.Equal(Channels.M365Copilot, channelId.ToString());
            channelId.SubChannel = "TEST";
            Assert.Equal("msteams:TEST", channelId.ToString());

            // With formatted value
            channelId = new ChannelId(Channels.M365Copilot);
            Assert.Equal(Channels.M365Copilot, channelId);
            Assert.True(Channels.M365Copilot == channelId);
            Assert.Equal(Channels.Msteams, channelId.Channel);
            Assert.True(activity.ChannelId.IsParentChannel(Channels.Msteams));
            Assert.Equal(Channels.M365CopilotSubChannel, channelId.SubChannel);

            // nulls
            activity = new Activity() { ChannelId = null };
            Assert.False(Channels.Msteams == activity.ChannelId);

            activity = new Activity() { ChannelId = Channels.Msteams };
            Assert.NotNull(activity.ChannelId);

            // Equality
            var channelId1 = new ChannelId(Channels.Msteams);
            var channelId2 = new ChannelId(Channels.Msteams);
            var channelId3 = new ChannelId(Channels.M365Copilot);
            Assert.Equal(channelId1, channelId2);
            Assert.NotEqual(channelId1, channelId3);

            // conversion
            channelId = new ChannelId(Channels.M365Copilot);
            string str = channelId;
            Assert.Equal(str, channelId.ToString());
        }

        [Fact]
        public void SubChannelTest()
        {
            // Is M365Copilot?
            string m365CopilotJson = "{\"channelId\":\"msteams\",\"membersAdded\":[],\"membersRemoved\":[],\"reactionsAdded\":[],\"reactionsRemoved\":[],\"attachments\":[],\"entities\":[{\"id\":\"COPILOT\",\"type\":\"ProductInfo\"}],\"listenFor\":[],\"textHighlights\":[]}";
            var activity = ProtocolJsonSerializer.ToObject<IActivity>(m365CopilotJson);
            Assert.True(Channels.M365Copilot == activity.ChannelId);

            // Base channel vs subchannel eval
            Assert.Equal(Channels.Msteams, activity.ChannelId.Channel);
            Assert.Equal(Channels.M365CopilotSubChannel, activity.ChannelId.SubChannel);
            Assert.True(activity.ChannelId.IsParentChannel(Channels.Msteams));

            // Serialize back out correctly
            var json = ProtocolJsonSerializer.ToJson(activity);
            Assert.Equal(m365CopilotJson, json);

            // Can update ProductInfo from ChannelIds
            activity.ChannelId.SubChannel = "TEST";
            activity = ProtocolJsonSerializer.ToObject<IActivity>(ProtocolJsonSerializer.ToJson(activity));
            var productInfo = activity.GetProductInfoEntity();
            Assert.NotNull(productInfo);
            Assert.Equal("TEST", productInfo.Id);
            Assert.Equal("msteams:TEST", activity.ChannelId);
        }

        [Fact]
        public void SubChannelWithoutEntityTest()
        {
            IActivity activity = new Activity()
            {
                ChannelId = Channels.M365Copilot
            };

            var json = ProtocolJsonSerializer.ToJson(activity);

            // Verify has ProductInfo Entity
            string m365CopilotJson = "{\"channelId\":\"msteams\",\"membersAdded\":[],\"membersRemoved\":[],\"reactionsAdded\":[],\"reactionsRemoved\":[],\"attachments\":[],\"entities\":[{\"id\":\"COPILOT\",\"type\":\"ProductInfo\"}],\"listenFor\":[],\"textHighlights\":[]}";
            Assert.Equal(m365CopilotJson, json);

            activity = ProtocolJsonSerializer.ToObject<IActivity>(json);
            Assert.True(Channels.M365Copilot == activity.ChannelId);
            Assert.NotNull(activity.GetProductInfoEntity());
        }

        /*
        [Fact]
        public void FullNotationOffTest()
        {
            ProtocolJsonSerializer.ChannelIdIncludesProduct = false;

            // Is M365Copilot?
            string m365CopilotJson = "{\"channelId\":\"msteams\",\"membersAdded\":[],\"membersRemoved\":[],\"reactionsAdded\":[],\"reactionsRemoved\":[],\"attachments\":[],\"entities\":[{\"id\":\"COPILOT\",\"type\":\"ProductInfo\"}],\"listenFor\":[],\"textHighlights\":[]}";
            var activity = ProtocolJsonSerializer.ToObject<IActivity>(m365CopilotJson);

            // This should just be "msteams"
            Assert.True(Channels.Msteams == activity.ChannelId);

            // Base channel vs subchannel eval
            Assert.Equal(Channels.Msteams, activity.ChannelId.Channel);
            Assert.Equal(Channels.M365CopilotSubChannel, activity.ChannelId.SubChannel);
            Assert.True(activity.ChannelId.IsParentChannel(Channels.Msteams));

            // Serialize back out correctly
            var json = ProtocolJsonSerializer.ToJson(activity);
            Assert.Equal(m365CopilotJson, json);

            // Can update ProductInfo from ChannelIds
            activity.ChannelId.SubChannel = "TEST";
            activity = ProtocolJsonSerializer.ToObject<IActivity>(ProtocolJsonSerializer.ToJson(activity));
            var productInfo = activity.GetProductInfoEntity();
            Assert.NotNull(productInfo);
            Assert.Equal("TEST", productInfo.Id);
            Assert.Equal("msteams", activity.ChannelId);

            ProtocolJsonSerializer.ChannelIdIncludesProduct = true;
        }
        */
    }
}
