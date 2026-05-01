// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class AttachmentTests
    {
        [Fact]
        public void Attachment_RoundTrip()
        {
            var attachment = new Microsoft.Agents.Core.Models.Attachment
            {
                ContentType = "text/plain",
                ContentUrl = "http://example.com/file.txt",
                Content = "This is the content of the file.",
                Name = "file.txt",
                ThumbnailUrl = "http://example.com/thumbnail.jpg"
            };

            var json = ProtocolJsonSerializer.ToJson(attachment);
            var deserializedAttachment = ProtocolJsonSerializer.ToObject<Microsoft.Agents.Core.Models.Attachment>(json);

            Assert.Equal(json, ProtocolJsonSerializer.ToJson(deserializedAttachment));
        }

        [Fact]
        public void Attachment_RoundTripProperties()
        {
            var json = "{\"contentType\":\"text/plain\",\"contentUrl\":\"http://example.com/file.txt\",\"content\":\"This is the content of the file.\",\"name\":\"file.txt\",\"thumbnailUrl\":\"http://example.com/thumbnail.jpg\",\"prop1\":\"value1\"}";
            var deserializedAttachment = ProtocolJsonSerializer.ToObject<Microsoft.Agents.Core.Models.Attachment>(json);

            Assert.Equal(json, ProtocolJsonSerializer.ToJson(deserializedAttachment));
        }
    }
}
