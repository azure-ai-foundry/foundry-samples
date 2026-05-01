// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class CacheInfoTests
    {
        [Fact]
        public void CacheInfoInits()
        {
            var cacheType = "cacheType";
            var cacheDuration = 1000;
            var cacheInfo = new CacheInfo(cacheType, cacheDuration);

            Assert.NotNull(cacheInfo);
            Assert.IsType<CacheInfo>(cacheInfo);
            Assert.Equal(cacheType, cacheInfo.CacheType);
            Assert.Equal(cacheDuration, cacheInfo.CacheDuration);
        }

        [Fact]
        public void CacheInfoInitsWithNoArgs()
        {
            var cacheInfo = new CacheInfo();

            Assert.NotNull(cacheInfo);
            Assert.IsType<CacheInfo>(cacheInfo);
        }
    }
}
