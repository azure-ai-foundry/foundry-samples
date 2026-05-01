// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class AICitationTests
    {
        [Fact]
        public void AIEntity_Roundtrip()
        {
            // Arrange
            var entityOut = new AIEntity()
            {
                Citation = [
                    new ClientCitation()
                    {
                        Position = 1,
                        Appearance = new ClientCitationAppearance()
                        {
                            Name = "name",
                            Text = "text",
                            Url = "url",
                            Image = new AppearanceImage()
                            {
                                Name = ClientCitationsIconNameEnum.Image,// "image",
                            }
                        }
                    }
                ]
            };

            var expected = "{\"@type\":\"Message\",\"@context\":\"https://schema.org\",\"@id\":\"\",\"additionalType\":[],\"citation\":[{\"position\":1,\"appearance\":{\"name\":\"name\",\"text\":\"text\",\"url\":\"url\",\"image\":{\"type\":\"ImageObject\",\"name\":\"Image\"},\"@type\":\"DigitalDocument\"},\"@type\":\"Claim\"}],\"type\":\"https://schema.org/Message\"}";
   
            // Test serialize
            var jsonOut = ProtocolJsonSerializer.ToJson(entityOut);
            Assert.Equal(expected, jsonOut);

            // Test deserialize
            var entityIn = ProtocolJsonSerializer.ToObject<Entity>(jsonOut);
            Assert.IsType<AIEntity>(entityIn);

            var deserializeJson = ProtocolJsonSerializer.ToJson(entityIn);
            Assert.Equal(expected, deserializeJson);
        }

        [Fact]
        public void AIEntity_Properties()
        {
            var jsonIn = "{\"@type\":\"Message\",\"@context\":\"https://schema.org\",\"@id\":\"\",\"additionalType\":[\"AIGeneratedContent\"],\"citation\":[],\"type\":\"https://schema.org/Message\",\"customProperty\":\"customValue\",\"additional\":{\"myprops\":{\"a\":\"b\"}}}";

            var entity = ProtocolJsonSerializer.ToObject<Entity>(jsonIn);
            Assert.IsAssignableFrom<AIEntity>(entity);

            var jsonOut = ProtocolJsonSerializer.ToJson(entity);
            Assert.Equal(jsonIn, jsonOut);
        }
    }
}
