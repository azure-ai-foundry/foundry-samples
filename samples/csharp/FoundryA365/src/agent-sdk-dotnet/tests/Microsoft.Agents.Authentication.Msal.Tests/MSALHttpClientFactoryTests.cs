// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Moq;
using System;
using System.Net.Http;
using Xunit;

namespace Microsoft.Agents.Authentication.Msal.Tests
{
    public class MSALHttpClientFactoryTests
    {
        private readonly Mock<IServiceProvider> _service = new();

        [Fact]
        public void Constructor_ShouldInstantiateCorrectly()
        {
            var factory = new MSALHttpClientFactory(_service.Object);

            Assert.NotNull(factory);
        }

        [Fact]
        public void GetHttpClient_ShouldThrowOnNullIHttpClientFactory()
        {
            var factory = new MSALHttpClientFactory(_service.Object);
            
            Assert.Throws<InvalidOperationException>(factory.GetHttpClient);
        }

        [Fact]
        public void GetHttpClient_ShouldReturnClient()
        {
            var baseAddress = new Uri("https://botframework.com");
            
            _service.Setup(x => x.GetService(typeof(IHttpClientFactory)))
                .Returns(new TestHttpClientFactory())
                .Verifiable(Times.Once);
            
            var factory = new MSALHttpClientFactory(_service.Object);
            var client = factory.GetHttpClient();

            Assert.NotNull(client);
            Assert.Equal(baseAddress, client.BaseAddress);
            Mock.Verify(_service);
        }

        private class TestHttpClient : HttpClient
        {
            public TestHttpClient()
            {
                BaseAddress = new Uri("https://botframework.com");
            }
        }

        private class TestHttpClientFactory : IHttpClientFactory
        {
            public HttpClient CreateClient(string name)
            {
                return new TestHttpClient();
            }
        }
    }
}
