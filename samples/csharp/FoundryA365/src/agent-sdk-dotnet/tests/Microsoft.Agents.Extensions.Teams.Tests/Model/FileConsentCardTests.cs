// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class FileConsentCardTests
    {
        [Fact]
        public void FileConsentCardInits()
        {
            var description = "A text document about butterflies";
            long sizeInBytes = 4000;
            var acceptContext = "free flow schema sent back in Value field of Activity with file upload consent";
            var declineContext = "free flow schema sent back in Value field of Activity with decline of file upload";

            var fileConsentCard = new FileConsentCard(description, sizeInBytes, acceptContext, declineContext);

            Assert.NotNull(fileConsentCard);
            Assert.IsType<FileConsentCard>(fileConsentCard);
            Assert.Equal(description, fileConsentCard.Description);
            Assert.Equal(sizeInBytes, fileConsentCard.SizeInBytes);
            Assert.Equal(acceptContext, fileConsentCard.AcceptContext);
            Assert.Equal(declineContext, fileConsentCard.DeclineContext);
        }

        [Fact]
        public void FileConsentCardInitsWithNoArgs()
        {
            var fileConsentCard = new FileConsentCard();

            Assert.NotNull(fileConsentCard);
            Assert.IsType<FileConsentCard>(fileConsentCard);
        }

        [Fact]
        public void FileConsentCardRoundTrip()
        {
            var fileConsentCard = new FileConsentCard()
            {
                Description = "description",
                SizeInBytes = 1,
                AcceptContext = new { acceptContext = "acceptContext" },
                DeclineContext = new { declineContext = "acceptContext" }
            };

            // Known good
            var goodJson = LoadTestJson.LoadJson(fileConsentCard);

            // Out
            var json = ProtocolJsonSerializer.ToJson(fileConsentCard);
            Assert.Equal(goodJson, json);

            // In
            var inObj = ProtocolJsonSerializer.ToObject<FileConsentCard>(json);
            json = ProtocolJsonSerializer.ToJson(inObj);
            Assert.Equal(goodJson, json);
        }
    }
}
