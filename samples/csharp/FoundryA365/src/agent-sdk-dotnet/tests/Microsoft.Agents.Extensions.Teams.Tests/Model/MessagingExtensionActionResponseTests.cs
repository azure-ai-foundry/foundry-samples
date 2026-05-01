// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class MessagingExtensionActionResponseTests
    {
        [Fact]
        public void MessagingExtensionActionResponseInits()
        {
            var task = new TaskModuleResponseBase("message");
            var composeExtension = new MessagingExtensionResult("list", "message", null, null, "with a personality like sunshine");
            var cacheInfo = new CacheInfo();

            var msgExtActionResponse = new MessagingExtensionActionResponse(task, composeExtension)
            {
                CacheInfo = cacheInfo
            };

            Assert.NotNull(msgExtActionResponse);
            Assert.IsType<MessagingExtensionActionResponse>(msgExtActionResponse);
            Assert.Equal(task, msgExtActionResponse.Task);
            Assert.Equal(composeExtension, msgExtActionResponse.ComposeExtension);
            Assert.Equal(cacheInfo, msgExtActionResponse.CacheInfo);
        }

        [Fact]
        public void MessagingExtensionActionResponseInitsWithNoArgs()
        {
            var msgExtActionResponse = new MessagingExtensionActionResponse();

            Assert.NotNull(msgExtActionResponse);
            Assert.IsType<MessagingExtensionActionResponse>(msgExtActionResponse);
        }

        [Fact]
        public void MessagingExtensionActionResponseRoundTrip()
        {
            var task = new TaskModuleResponseBase("message") { Properties = ProtocolJsonSerializer.ToJsonElements(new { prop1 = "prop1" }) };
            var composeExtension = new MessagingExtensionResult()
            {
                AttachmentLayout = "attachmentLayout",
                Type = "type",
                Attachments = [new MessagingExtensionAttachment() { }],
                SuggestedActions = new MessagingExtensionSuggestedAction() { Actions = [new CardAction() 
                    {
                        Type = "type",
                        Title = "title",
                        Image = "image",
                        ImageAltText = "imageAltText",
                        Text = "text",
                        DisplayText = "displayText",
                        Value = new { value = "value" },
                        ChannelData = new { channelData = "channelData"}
                    }] },
                Text = "text",
                ActivityPreview = new Activity()
            };
    
            var msgExtActionResponse = new MessagingExtensionActionResponse(task, composeExtension)
            {
                CacheInfo = new CacheInfo() { CacheDuration = 1, CacheType = "cacheType"}
            };

            // Known good
            var goodJson = LoadTestJson.LoadJson(msgExtActionResponse);

            // Out
            var json = ProtocolJsonSerializer.ToJson(msgExtActionResponse);
            Assert.Equal(goodJson, json);

            // In
            var inObj = ProtocolJsonSerializer.ToObject<MessagingExtensionActionResponse>(json);
            json = ProtocolJsonSerializer.ToJson(inObj);
            Assert.Equal(goodJson, json);
        }

    }
}
