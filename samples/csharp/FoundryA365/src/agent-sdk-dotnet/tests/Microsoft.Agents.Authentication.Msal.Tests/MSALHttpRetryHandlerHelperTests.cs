// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Msal.Model;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System;
using System.Net.Http;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Authentication.Msal.Tests
{
    public class MSALHttpRetryHandlerHelperTests
    {
        private readonly Mock<IOptions<MsalAuthConfigurationOptions>> _options;
        private readonly Mock<IServiceProvider> _service = new();
        private readonly MsalAuthConfigurationOptions _returnedOptions = new()
        {
            MSALRetryCount = 4
        };

        private readonly Mock<HttpMessageHandler> _handler = new();

        private const string RequestUri = "http://test.com";

        public MSALHttpRetryHandlerHelperTests()
        {
            _options = new Mock<IOptions<MsalAuthConfigurationOptions>>();
            _options.Setup(x => x.Value)
                .Returns(_returnedOptions)
                .Verifiable(Times.Once);
            
            _service.Setup(x => x.GetService(typeof(IOptions<MsalAuthConfigurationOptions>)))
                .Returns(_options.Object)
                .Verifiable(Times.Once);
        }

        [Fact]
        public void Constructor_ShouldInstantiateCorrectly()
        {
            var retryHelper = new MSALHttpRetryHandlerHelper(_service.Object);

            Assert.NotNull(retryHelper);
            Mock.Verify(_service);
        }

        [Fact]
        public async Task SendAsync_ShouldReturnSuccessfulResponse()
        {
            _handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK))
                .Verifiable(Times.Once);

            var retryHandler = new MSALHttpRetryHandlerHelper(_service.Object)
            {
                InnerHandler = _handler.Object
            };

            var httpClient = new HttpClient(retryHandler);

            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, RequestUri));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Mock.Verify(_handler);
        }

        [Fact]
        public async Task SendAsync_ShouldReturnSuccessfulResponseAfterRetries()
        {
            _handler.Protected()
                .SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            var retryHandler = new MSALHttpRetryHandlerHelper(_service.Object)
            {
                InnerHandler = _handler.Object
            };

            var httpClient = new HttpClient(retryHandler);

            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, RequestUri));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            _handler.Protected().Verify("SendAsync", Times.Exactly(4), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task SendAsync_ShouldReturnResponseOnNonRetryableFailure()
        {
            _handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest))
                .Verifiable(Times.Once);

            var retryHandler = new MSALHttpRetryHandlerHelper(_service.Object)
            {
                InnerHandler = _handler.Object
            };

            var httpClient = new HttpClient(retryHandler);

            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, RequestUri));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Mock.Verify(_handler);
        }

        [Fact]
        public async Task SendAsync_ShouldReturnResponseAfterExhaustsAllRetries()
        {
            _handler.Protected()
                .SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout));

            var retryHandler = new MSALHttpRetryHandlerHelper(_service.Object)
            {
                InnerHandler = _handler.Object
            };

            var httpClient = new HttpClient(retryHandler);

            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, RequestUri));

            Assert.Equal(HttpStatusCode.RequestTimeout, response.StatusCode);
            _handler.Protected().Verify("SendAsync", Times.Exactly(4), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }
    }
}
