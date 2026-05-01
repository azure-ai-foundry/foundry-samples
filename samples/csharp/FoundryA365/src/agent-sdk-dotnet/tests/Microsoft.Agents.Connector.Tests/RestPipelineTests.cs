// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using Microsoft.Agents.Connector.RestClients;
using Microsoft.Agents.Core.Models;
using Moq;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Connector.Tests
{
    public class RestPipelineTests
    {
        private static readonly Uri Endpoint = new Uri("http://localhost/");

        // Helper: creates a mock IRestTransport whose HttpClient uses a fake handler
        private static (Mock<IRestTransport> transport, FakeHttpHandler handler) CreateTransport(
            HttpStatusCode status, string? jsonBody = null)
        {
            var handler = new FakeHttpHandler(status, jsonBody);
            var client = new HttpClient(handler);
            var mock = new Mock<IRestTransport>();
            mock.Setup(t => t.Endpoint).Returns(Endpoint);
            mock.Setup(t => t.GetHttpClientAsync()).ReturnsAsync(client);
            return (mock, handler);
        }

        [Fact]
        public async Task SendRawAsync_SendsToCorrectUri()
        {
            var (transport, handler) = CreateTransport(HttpStatusCode.OK, "{}");
            var request = RestRequest.Get("v3/conversations");

            using var _ = await RestPipeline.SendRawAsync(transport.Object, request, CancellationToken.None);

            Assert.Equal("http://localhost/v3/conversations", handler.LastRequestUri?.ToString());
        }

        [Fact]
        public async Task SendRawAsync_ReturnsResponse()
        {
            var (transport, _) = CreateTransport(HttpStatusCode.NotFound, null);
            var request = RestRequest.Get("v3/conversations");

            using var response = await RestPipeline.SendRawAsync(transport.Object, request, CancellationToken.None);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task ReadContentAsync_DeserializesJson()
        {
            var activity = new Activity { Id = "test-id", Type = "message" };
            var json = JsonSerializer.Serialize(activity);
            using var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            var result = await RestPipeline.ReadContentAsync<Activity>(response, CancellationToken.None);

            Assert.Equal("test-id", result?.Id);
        }

        [Fact]
        public async Task ReadAsStringAsync_ReturnsContent()
        {
            using var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("hello-url", Encoding.UTF8, "text/plain")
            };

            var result = await RestPipeline.ReadAsStringAsync(response, CancellationToken.None);

            Assert.Equal("hello-url", result);
        }

        // Fake handler that captures the last request URI
        public class FakeHttpHandler(HttpStatusCode status, string? body) : HttpMessageHandler
        {
            public Uri? LastRequestUri { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequestUri = request.RequestUri;
                var response = new HttpResponseMessage(status);
                if (body != null)
                    response.Content = new StringContent(body, Encoding.UTF8, "application/json");
                return Task.FromResult(response);
            }
        }
    }
}
