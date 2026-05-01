// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class AnimationCardTests
    {
        [Fact]
        public void SuccessfullyInitAnimationCard()
        {
            var title = "title";
            var subtitle = "subtitle";
            var text = "text";
            var image = new ThumbnailUrl("http://example.com", "example image");
            var media = new List<MediaUrl>() { new MediaUrl("http://fakeMediaUrl.com", "media url profile") };
            var buttons = new List<CardAction>()
            {
                new CardAction("cardActionType", "cardActionTitle", "image", "text", "displayText", new { }, new { }),
            };
            var shareable = true;
            var autoloop = true;
            var autostart = true;
            var aspect = "aspect";
            var value = new { };
            var duration = "1000";

            var animationCard = new AnimationCard(
                    title,
                    subtitle,
                    text,
                    image,
                    media,
                    buttons,
                    shareable,
                    autoloop,
                    autostart,
                    aspect,
                    value,
                    duration);

            Assert.NotNull(animationCard);
            Assert.IsType<AnimationCard>(animationCard);
            Assert.Equal(title, animationCard.Title);
            Assert.Equal(subtitle, animationCard.Subtitle);
            Assert.Equal(text, animationCard.Text);
            Assert.Equal(image, animationCard.Image);
            Assert.Equal(media, animationCard.Media);
            Assert.Equal(buttons, animationCard.Buttons);
            Assert.Equal(shareable, animationCard.Shareable);
            Assert.Equal(autoloop, animationCard.Autoloop);
            Assert.Equal(autostart, animationCard.Autostart);
            Assert.Equal(aspect, animationCard.Aspect);
            Assert.Equal(value, animationCard.Value);
            Assert.Equal(duration, animationCard.Duration);
        }

        [Fact]
        public void AnimationCardRoundTripWithValueClass()
        {
            var card = new AnimationCard(
                    "title",
                    "subtitle",
                    "text",
                    new ThumbnailUrl("http://example.com", "example image"),
                    new List<MediaUrl>() { new MediaUrl("http://fakeMediaUrl.com", "media url profile") },
                    new List<CardAction>()
                    {
                        new CardAction("cardActionType", "cardActionTitle", "image", "text", "displayText"), //, new { }, new { }),
                    },
                    true,
                    true,
                    true,
                    "aspect",
                    new AnimationCardValue { Property1 = "prop1" },
                    "1000");

            var json = ProtocolJsonSerializer.ToJson(card);
                
            var deserializedCard = ProtocolJsonSerializer.ToObject<AnimationCard>(json);

            Assert.Equal(json, ProtocolJsonSerializer.ToJson(deserializedCard));
        }

        [Fact]
        public void AnimationCardRoundTripWithValueAnon()
        {
            var card = new AnimationCard(
                    "title",
                    "subtitle",
                    "text",
                    null,
                    null,
                    null,
                    true,
                    true,
                    true,
                    "aspect",
                    new { property1 = "prop1" },
                    "1000");

            var json = ProtocolJsonSerializer.ToJson(card);

            var deserializedCard = ProtocolJsonSerializer.ToObject<AnimationCard>(json);

            Assert.Equal(json, ProtocolJsonSerializer.ToJson(deserializedCard));
        }
    }

    class AnimationCardValue
    {
        public string Property1 { get; set; }
    }
}
