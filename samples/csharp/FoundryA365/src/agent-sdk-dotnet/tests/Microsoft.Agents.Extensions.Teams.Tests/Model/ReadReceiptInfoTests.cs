// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class ReadReceiptInfoTests
    {
        [Theory]
        [InlineData("1000", "1000", true)]
        [InlineData("1001", "1000", true)]
        [InlineData("1000", "1001", false)]
        [InlineData("1000", null, false)]
        [InlineData(null, "1000", false)]
        public void ReadReceiptInfoTest(string lastRead, string compare, bool isRead)
        {
            var info = new ReadReceiptInfo(lastRead);

            Assert.Equal(info.LastReadMessageId, lastRead);
            Assert.Equal(info.IsMessageRead(compare), isRead);
            Assert.Equal(ReadReceiptInfo.IsMessageRead(compare, lastRead), isRead);
        }
    }
}
