// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Connector.RestClients;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Moq;
using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Connector.Tests
{
    public class BotSignInRestClientTests
    {
        private static readonly Uri Endpoint = new("http://localhost");
        private const string State = "state";
        private const string CodeCallenge = "code-challenge";
        private const string EmulatorUrl = "emulator-url";
        private const string FinalRedirect = "final-redirect";
        private static readonly Mock<HttpClient> MockHttpClient = new();

        [Fact]
        public void Constructor_ShouldInstantiateCorrectly()
        {
            var client = UseClient();
            Assert.NotNull(client);
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullTransport()
        {
            Assert.Throws<ArgumentNullException>(() => new AgentSignInRestClient(null));
        }

        [Fact]
        public async Task GetSignInUrlAsync_ShouldThrowOnNullState()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetSignInUrlAsync(null, CodeCallenge, EmulatorUrl, FinalRedirect));
        }

        [Fact]
        public async Task GetSignInUrlAsync_ShouldThrowOnError()
        {
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

            var client = UseClient();
            await Assert.ThrowsAsync<ErrorResponseException>(() => client.GetSignInUrlAsync(State, CodeCallenge, EmulatorUrl, FinalRedirect));
        }

        [Fact]
        public async Task GetSignInUrlAsync_ShouldReturnSignInUrl()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("test-url")
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

            var client = UseClient();

            var result = await client.GetSignInUrlAsync(State, CodeCallenge, EmulatorUrl, FinalRedirect);

            Assert.Equal("test-url", result);
        }

        [Fact]
        public async Task GetSignInResourceAsync_ShouldThrowOnNullState()
        {
            var client = UseClient();
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetSignInResourceAsync(null, CodeCallenge, EmulatorUrl, FinalRedirect));
        }

        [Fact]
        public async Task GetSignInResourceAsync_ShouldThrowOnError()
        {
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

            var client = UseClient();

            await Assert.ThrowsAsync<ErrorResponseException>(() => client.GetSignInResourceAsync(State, CodeCallenge, EmulatorUrl, FinalRedirect));
        }

        [Fact]
        public async Task GetSignInResourceAsync_ShouldReturnSignInResource()
        {
            var content = new SignInResource()
            {
                SignInLink = "test-link",
                TokenExchangeResource = new TokenExchangeResource { Id = "test-id" },
                TokenPostResource = new TokenPostResource { SasUrl = "test-url" }
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(content))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

            var client = UseClient();

            var result = await client.GetSignInResourceAsync(State, CodeCallenge, EmulatorUrl, FinalRedirect);

            Assert.Equal(content.SignInLink, result.SignInLink);
            Assert.Equal(content.TokenExchangeResource.Id, result.TokenExchangeResource.Id);
            Assert.Equal(content.TokenPostResource.SasUrl, result.TokenPostResource.SasUrl);
        }

        private static AgentSignInRestClient UseClient()
        {
            var transport = new Mock<IRestTransport>();
            transport.Setup(x => x.Endpoint)
                .Returns(Endpoint);
            transport.Setup(a => a.GetHttpClientAsync())
                .Returns(Task.FromResult(MockHttpClient.Object));

            return new AgentSignInRestClient(transport.Object);
        }
    }
}
