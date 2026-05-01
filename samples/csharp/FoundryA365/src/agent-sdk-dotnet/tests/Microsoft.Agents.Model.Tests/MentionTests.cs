// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class MentionTests
    {
        [Fact]
        public void MentionInits()
        {
            var mentioned = new ChannelAccount("Id1", "Name1", "Role1", "aadObjectId1");
            var text = "hi @Name1";
            var type = "mention";

            var mention = new Mention(mentioned, text, type);

            Assert.NotNull(mention);
            Assert.IsType<Mention>(mention);
            Assert.Equal(mentioned, mention.Mentioned);
            Assert.Equal(text, mention.Text);
            Assert.Equal(type, mention.Type);
        }

        [Fact]
        public void MentionInitsWithNoArgs()
        {
            var mention = new Mention();

            Assert.NotNull(mention);
            Assert.IsType<Mention>(mention);
        }

        [Fact]
        public void MentionRoundTrip()
        {
            var outMention = new Mention()
            {
                Text = "imamention",
                Mentioned = new ChannelAccount()
                {
                    Id = "id",
                    Name = "name",
                }
            };

            var json = ProtocolJsonSerializer.ToJson(outMention);
            var inEntity = ProtocolJsonSerializer.ToObject<Entity>(json);

            Assert.IsAssignableFrom<Mention>(inEntity);

            var inMention = inEntity as Mention;
            Assert.Equal(outMention.Text, inMention.Text);
            Assert.NotNull(inMention.Mentioned);
            Assert.Equal(outMention.Mentioned.Name, inMention.Mentioned.Name);
            Assert.Equal(outMention.Mentioned.Id, inMention.Mentioned.Id);
        }

        [Fact]
        public void MentionTypedDeserialize()
        {
            var json = "{\"entities\": [{\"type\": \"mention\", \"text\": \"hi @Name1\", \"mentioned\": {\"id\": \"Id1\"}}]}";
            var activity = ProtocolJsonSerializer.ToObject<IActivity>(json);

            Assert.NotNull(activity.Entities);
            Assert.NotEmpty(activity.Entities);
            Assert.IsType<Mention>(activity.Entities[0]);
            var mention = activity.Entities[0] as Mention;
            Assert.Equal("hi @Name1", mention.Text);
            Assert.NotNull(mention.Mentioned);
            Assert.Equal("Id1", mention.Mentioned.Id);
        }
    }
}
