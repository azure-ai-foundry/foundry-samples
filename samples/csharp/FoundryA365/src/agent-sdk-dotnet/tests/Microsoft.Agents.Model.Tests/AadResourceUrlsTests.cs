// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class AadResourceUrlsTests
    {
        [Fact]
        public void AadResourceUrlsInits()
        {
            var resourceUrls = new List<string>() { "http://example.com" };

            var aadResourceUrls = new AadResourceUrls(resourceUrls);

            Assert.NotNull(aadResourceUrls);
            Assert.IsType<AadResourceUrls>(aadResourceUrls);
            Assert.Equal(resourceUrls, aadResourceUrls.ResourceUrls);
        }
        
        [Fact]
        public void AadResourceUrlsInitsWithNoArgs()
        {
            var aadResourceUrls = new AadResourceUrls();

            Assert.NotNull(aadResourceUrls);
            Assert.IsType<AadResourceUrls>(aadResourceUrls);
        }
    }
}
