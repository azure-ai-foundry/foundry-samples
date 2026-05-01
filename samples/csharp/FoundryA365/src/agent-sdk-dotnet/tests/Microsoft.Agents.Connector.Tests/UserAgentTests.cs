// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Connector;
using Moq;
using System.Net.Http;
using System.Net.Http.Headers;
using Xunit;

namespace Microsoft.Agents.Connector.Tests
{
    public class UserAgentTests
    {
        private readonly Mock<HttpClient> MockHttpClient = new();

        [Fact]
        public void AddDefaultUserAgent_ShouldReturnWhenUserAgentHeaderExists()
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            var client = new HttpClient(handlerMock.Object);
            client.DefaultRequestHeaders.Add("User-Agent", "testAgent");

            HttpClientExtensions.AddDefaultUserAgent(client);

            Assert.Single(client.DefaultRequestHeaders);
        }

        [Fact]
        public void AddDefaultUserAgent_ShouldReturnAddUserAgentHeader()
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            var client = new HttpClient(handlerMock.Object);

            Assert.Empty(client.DefaultRequestHeaders);

            HttpClientExtensions.AddDefaultUserAgent(client, additionalProductInfo: [new ProductInfoHeaderValue("test-product", "v1")]);

            Assert.Single(client.DefaultRequestHeaders);
        }
    }
}
