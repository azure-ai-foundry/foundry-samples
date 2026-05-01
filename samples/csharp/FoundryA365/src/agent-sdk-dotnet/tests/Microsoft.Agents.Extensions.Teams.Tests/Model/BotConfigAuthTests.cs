// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class BotConfigAuthTests
    {
        [Fact]
        public void BotConfigAuthInitsWithNoArgs()
        {
            var botConfigAuthResponse = new BotConfigAuth()
            {
                SuggestedActions = new SuggestedActions()
                {
                    To = ["id1", "id2"],

                }
            };

            Assert.NotNull(botConfigAuthResponse);
            Assert.IsType<BotConfigAuth>(botConfigAuthResponse);
            Assert.Equal("auth", botConfigAuthResponse.Type);
        }

        [Fact]
        public void BotConfigAuthRoundTrip()
        {
            var botConfigAuth = new BotConfigAuth()
            {
                SuggestedActions = new SuggestedActions()
                {
                    To = ["id1", "id2"],
                    Actions = [ 
                        new CardAction() 
                        {
                            Type = "type",
                            Title = "title",
                            Image = "image",
                            ImageAltText = "imageAltText",
                            Text = "text",
                            DisplayText = "displayText",
                            Value = new { value = "value" },
                            ChannelData = new { channelData = "channelData"}
                        }
                    ]
                }
            };

            // Known good
            var goodJson = LoadTestJson.LoadJson(botConfigAuth);

            // Out
            var json = ProtocolJsonSerializer.ToJson(botConfigAuth);
            Assert.Equal(goodJson, json);

            // In
            var inObj = ProtocolJsonSerializer.ToObject<BotConfigAuth>(json);
            json = ProtocolJsonSerializer.ToJson(inObj);
            Assert.Equal(goodJson, json);
        }
    }
}
