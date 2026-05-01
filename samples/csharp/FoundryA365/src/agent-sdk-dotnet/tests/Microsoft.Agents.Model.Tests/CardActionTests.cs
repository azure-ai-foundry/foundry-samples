// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class CardActionTests
    {
        [Fact]
        public void TestImplicitConversation()
        {
            SuggestedActions(new CardAction[]
            {
                "x",
                "y",
                "z"
            });

            void SuggestedActions(IList<CardAction> actions)
            {
                Assert.Equal("x", actions[0].Title);
                Assert.Equal("x", actions[0].Value);
                Assert.Equal("y", actions[1].Title);
                Assert.Equal("y", actions[1].Value);
                Assert.Equal("z", actions[2].Title);
                Assert.Equal("z", actions[2].Value);
            }
        }

        [Fact]
        public void FromString()
        {
            var sut = CardAction.FromString("my action");
            Assert.Equal("my action", sut.Title);
            Assert.Equal("my action", sut.Value);
        }

        [Fact]
        public void CardActionInits()
        {
            var type = "type";
            var title = "title";
            var image = "image";
            var text = "text";
            var displayText = "displayText";
            var value = new { };
            var channelData = new { };
            var imageAltText = "imageAltText";

            var cardAction = new CardAction(type, title, image, text, displayText, value, channelData)
            {
                ImageAltText = imageAltText
            };

            Assert.NotNull(cardAction);
            Assert.IsType<CardAction>(cardAction);
            Assert.Equal(type, cardAction.Type);
            Assert.Equal(title, cardAction.Title);
            Assert.Equal(image, cardAction.Image);
            Assert.Equal(text, cardAction.Text);
            Assert.Equal(displayText, cardAction.DisplayText);
            Assert.Equal(value, cardAction.Value);
            Assert.Equal(channelData, cardAction.ChannelData);
            Assert.Equal(imageAltText, cardAction.ImageAltText);
        }

        [Fact]
        public void CardActionInitsWithNoArgs()
        {
            var cardAction = new CardAction();

            Assert.NotNull(cardAction);
            Assert.IsType<CardAction>(cardAction);
        }

        [Fact]
        public void CardActionRoundTrip()
        {
            var cardAction = new CardAction
            {
                Text = "text",
                Title = "title",
                Value = new CardActionValue() { PropertyA = "propA", PropertyB = "propB" },
                ChannelData = new CardActionChannelData() { Property1 = "prop1", Property2 = "prop2" }
            };

            var outJson = ProtocolJsonSerializer.ToJson(cardAction);

            var inCardAction = ProtocolJsonSerializer.ToObject<CardAction>(outJson);

            Assert.Equal(outJson, ProtocolJsonSerializer.ToJson(inCardAction));
        }
    }

    class CardActionChannelData
    {
        public string Property1 { get; set; }
        public string Property2 { get; set; }
    }

    class CardActionValue
    {
        public string PropertyA { get; set; }
        public string PropertyB { get; set; }
    }
}
