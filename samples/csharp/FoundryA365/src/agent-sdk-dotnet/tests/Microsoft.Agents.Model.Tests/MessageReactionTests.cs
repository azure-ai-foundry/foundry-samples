// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class MessageReactionTests
    {
        [Fact]
        public void MessageReactionInits()
        {
            var type = "like";
            var messageReaction = new MessageReaction(type);

            Assert.NotNull(messageReaction);
            Assert.IsType<MessageReaction>(messageReaction);
            Assert.Equal(type, messageReaction.Type);
        }

        [Fact]
        public void MessageReactionInitsWithNoArgs()
        {
            var messageReaction = new MessageReaction();

            Assert.NotNull(messageReaction);
            Assert.IsType<MessageReaction>(messageReaction);
        }

        [Fact]
        public void MessageReaction_RoundTrip()
        {
            var jsonIn = "{\"type\":\"like\"}";

            var messageReaction = ProtocolJsonSerializer.ToObject<MessageReaction>(jsonIn);

            var jsonOut = ProtocolJsonSerializer.ToJson(messageReaction);
            Assert.Equal(jsonIn, jsonOut);
        }

        [Fact]
        public void MessageReaction_RoundTrip_WithExtendedProperties()
        {
            var jsonIn = "{\"type\":\"like\",\"prop1\":\"value1\",\"prop2\":\"value2\"}";

            var messageReaction = ProtocolJsonSerializer.ToObject<MessageReaction>(jsonIn);
            Assert.Equal(2, messageReaction.Properties.Count);

            var jsonOut = ProtocolJsonSerializer.ToJson(messageReaction);
            Assert.Equal(jsonIn, jsonOut);
        }
    }
}
