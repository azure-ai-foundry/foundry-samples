// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class AudioCardTests
    {
        [Fact]
        public void AudioCardInit()
        {
            var title = "title";
            var subtitle = "subtitle";
            var text = "text";
            var image = new ThumbnailUrl("https://example.com", "example image");
            var media = new List<MediaUrl>() { new MediaUrl("http://exampleMedia.com", "profile") };
            var buttons = new List<CardAction>() { new CardAction("type", "title", "image", "text", "displayText", new { }, new { }) };
            var shareable = true;
            var autoloop = true;
            var autostart = true;
            var aspect = "aspect";
            var value = new { };
            var duration = "duration";

            var audioCard = new AudioCard(
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

            Assert.NotNull(audioCard);
            Assert.IsType<AudioCard>(audioCard);
            Assert.Equal(title, audioCard.Title);
            Assert.Equal(subtitle, audioCard.Subtitle);
            Assert.Equal(text, audioCard.Text);
            Assert.Equal(image, audioCard.Image);
            Assert.Equal(media, audioCard.Media);
            Assert.Equal(buttons, audioCard.Buttons);
            Assert.Equal(shareable, audioCard.Shareable);
            Assert.Equal(autoloop, audioCard.Autoloop);
            Assert.Equal(autostart, audioCard.Autostart);
            Assert.Equal(aspect, audioCard.Aspect);
            Assert.Equal(value, audioCard.Value);
            Assert.Equal(duration, audioCard.Duration);
        }

        [Fact]
        public void AudioCardInitsWithNoArgs()
        {
            var audioCard = new AudioCard();

            Assert.NotNull(audioCard);
            Assert.IsType<AudioCard>(audioCard);
        }

        [Fact]
        public void AudioCard_Roundtrip()
        {
            var card = new AudioCard(
                title: "title",
                subtitle: "subtitle",
                text: "text",
                image: new ThumbnailUrl("https://example.com", "example image"),
                media: new List<MediaUrl>() { new MediaUrl("http://exampleMedia.com", "profile") },
                buttons: new List<CardAction>() { new CardAction("type", "title", "image", "text", "displayText") },
                shareable: true,
                autoloop: true,
                autostart: true,
                aspect: "aspect",
                value: new AudioCardValue { Property1 = "value1", Property2 = 2 },
                duration: "duration");
            
            var json = ProtocolJsonSerializer.ToJson(card);

            var deserializedCard = ProtocolJsonSerializer.ToObject<AudioCard>(json);
            Assert.NotNull(deserializedCard);
            Assert.NotNull(deserializedCard.Value);
            Assert.IsType<JsonElement>(deserializedCard.Value, exactMatch: false); 
            Assert.Equal("value1", ((JsonElement)deserializedCard.Value).GetProperty("property1").GetString());
            Assert.Equal(2, ((JsonElement)deserializedCard.Value).GetProperty("property2").GetInt32());

            Assert.Equal(json, ProtocolJsonSerializer.ToJson(deserializedCard));
        }
    }

    class AudioCardValue
    {
        public string Property1 { get; set; }
        public int Property2 { get; set; }
    }
}
