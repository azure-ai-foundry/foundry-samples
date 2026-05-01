// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class FileInfoCardTests
    {
        [Fact]
        public void FileInfoCardInits()
        {
            var uniqueId = "unique-file-id-123";
            var fileType = ".txt";
            var etag = "etag123";

            var fileInfoCard = new FileInfoCard(uniqueId, fileType, etag);

            Assert.NotNull(fileInfoCard);
            Assert.IsType<FileInfoCard>(fileInfoCard);
            Assert.Equal(uniqueId, fileInfoCard.UniqueId);
            Assert.Equal(fileType, fileInfoCard.FileType);
            Assert.Equal(etag, fileInfoCard.Etag);
        }

        [Fact]
        public void FileInfoCardInitsWithNoArgs()
        {
            var fileInfoCard = new FileInfoCard();

            Assert.NotNull(fileInfoCard);
            Assert.IsType<FileInfoCard>(fileInfoCard);
        }
    }
}
