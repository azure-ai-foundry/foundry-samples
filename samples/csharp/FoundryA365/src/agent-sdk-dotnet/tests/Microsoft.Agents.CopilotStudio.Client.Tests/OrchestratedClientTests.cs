// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.CopilotStudio.Client.Discovery;
using Microsoft.Agents.CopilotStudio.Client.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.CopilotStudio.Client.Tests
{
    public class OrchestratedClientTests
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<ILogger<OrchestratedClient>> _loggerMock;
        private readonly ConnectionSettings _settings;

        public OrchestratedClientTests()
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _loggerMock = new Mock<ILogger<OrchestratedClient>>();
            _settings = new ConnectionSettings(null)
            {
                EnvironmentId = "test-env-id",
                SchemaName = "test-bot",
                CdsBotId = "A47151CF-4F34-488F-B377-EBE84E17B478"
            };
        }

        #region StartConversationAsync Tests

        [Fact]
        public async Task StartConversationAsync_WithSseActivityResponse_ReturnsActivityResponses()
        {
            // Arrange
            var handler = new FakeSseHttpMessageHandler("event: activity\ndata: {\"type\": \"message\", \"text\": \"Hello\", \"conversation\": {\"id\": \"conv-1\"}}\n\n");
            SetupHttpClient(handler);
            var client = CreateClient();

            // Act
            var responses = new List<OrchestratedResponse>();
            await foreach (var response in client.StartConversationAsync("conv-1"))
            {
                responses.Add(response);
            }

            // Assert
            Assert.Single(responses);
            var activityResponse = Assert.IsType<OrchestratedActivityResponse>(responses[0]);
            Assert.Equal("message", activityResponse.Activity.Type);
            Assert.Equal("Hello", activityResponse.Activity.Text);
        }

        [Fact]
        public async Task StartConversationAsync_WithSseStateResponse_ReturnsStateResponse()
        {
            // Arrange
            var handler = new FakeSseHttpMessageHandler("event: state\ndata: {\"status\": \"Completed\", \"enabledToolSchemaNames\": [\"tool1\"]}\n\n");
            SetupHttpClient(handler);
            var client = CreateClient();

            // Act
            var responses = new List<OrchestratedResponse>();
            await foreach (var response in client.StartConversationAsync("conv-1"))
            {
                responses.Add(response);
            }

            // Assert
            Assert.Single(responses);
            var stateResponse = Assert.IsType<OrchestratedStateResponse>(responses[0]);
            Assert.Equal("Completed", stateResponse.AgentState.Status.Value);
            Assert.Contains("tool1", stateResponse.AgentState.EnabledToolSchemaNames);
        }

        [Fact]
        public async Task StartConversationAsync_WithSseErrorResponse_ReturnsErrorResponse()
        {
            // Arrange
            var handler = new FakeSseHttpMessageHandler("event: error\ndata: {\"error\": {\"code\": \"BadRequest\", \"message\": \"Something went wrong\"}}\n\n");
            SetupHttpClient(handler);
            var client = CreateClient();

            // Act
            var responses = new List<OrchestratedResponse>();
            await foreach (var response in client.StartConversationAsync("conv-1"))
            {
                responses.Add(response);
            }

            // Assert
            Assert.Single(responses);
            var errorResponse = Assert.IsType<OrchestratedErrorResponse>(responses[0]);
            Assert.Equal("BadRequest", errorResponse.Error.Code);
            Assert.Equal("Something went wrong", errorResponse.Error.Message);
            Assert.Equal(OrchestratedErrorCode.Unknown, errorResponse.Error.ErrorCode);
        }

        [Fact]
        public async Task StartConversationAsync_WithKnownErrorCode_MapsToEnum()
        {
            // Arrange
            var handler = new FakeSseHttpMessageHandler("event: error\ndata: {\"error\": {\"code\": \"ConversationNotFound\", \"message\": \"Conversation not found\"}}\n\n");
            SetupHttpClient(handler);
            var client = CreateClient();

            // Act
            var responses = new List<OrchestratedResponse>();
            await foreach (var response in client.StartConversationAsync("conv-1"))
            {
                responses.Add(response);
            }

            // Assert
            Assert.Single(responses);
            var errorResponse = Assert.IsType<OrchestratedErrorResponse>(responses[0]);
            Assert.Equal("ConversationNotFound", errorResponse.Error.Code);
            Assert.Equal(OrchestratedErrorCode.ConversationNotFound, errorResponse.Error.ErrorCode);
        }

        [Theory]
        [InlineData("InvalidOperation", OrchestratedErrorCode.InvalidOperation)]
        [InlineData("MissingRequiredField", OrchestratedErrorCode.MissingRequiredField)]
        [InlineData("InvalidOperationForState", OrchestratedErrorCode.InvalidOperationForState)]
        [InlineData("Unauthorized", OrchestratedErrorCode.Unauthorized)]
        [InlineData("Throttled", OrchestratedErrorCode.Throttled)]
        [InlineData("BotContentNotFound", OrchestratedErrorCode.BotContentNotFound)]
        [InlineData("TopicNotFound", OrchestratedErrorCode.TopicNotFound)]
        [InlineData("InternalServerError", OrchestratedErrorCode.InternalServerError)]
        [InlineData("ConversationNotFound", OrchestratedErrorCode.ConversationNotFound)]
        [InlineData("SomeFutureError", OrchestratedErrorCode.Unknown)]
        [InlineData("", OrchestratedErrorCode.Unknown)]
        [InlineData(null, OrchestratedErrorCode.Unknown)]
        public void ErrorCode_ParsesCorrectly(string code, OrchestratedErrorCode expected)
        {
            Assert.Equal(expected, code.ToOrchestratedErrorCode());
        }

        [Fact]
        public async Task StartConversationAsync_WithMultipleSseEvents_ReturnsAllResponses()
        {
            // Arrange
            var sseData = new StringBuilder();
            sseData.AppendLine("event: activity");
            sseData.AppendLine("data: {\"type\": \"message\", \"text\": \"Hello\", \"conversation\": {\"id\": \"conv-1\"}}");
            sseData.AppendLine();
            sseData.AppendLine("event: state");
            sseData.AppendLine("data: {\"status\": \"WaitingForUserInput\", \"enabledToolSchemaNames\": []}");
            sseData.AppendLine();

            var handler = new FakeSseHttpMessageHandler(sseData.ToString());
            SetupHttpClient(handler);
            var client = CreateClient();

            // Act
            var responses = new List<OrchestratedResponse>();
            await foreach (var response in client.StartConversationAsync("conv-1"))
            {
                responses.Add(response);
            }

            // Assert
            Assert.Equal(2, responses.Count);
            Assert.IsType<OrchestratedActivityResponse>(responses[0]);
            Assert.IsType<OrchestratedStateResponse>(responses[1]);
        }

        [Fact]
        public async Task StartConversationAsync_WithNullConversationId_Throws()
        {
            var client = CreateClient();
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await foreach (var _ in client.StartConversationAsync(null!)) { }
            });
        }

        #endregion

        #region InvokeToolAsync Tests

        [Fact]
        public async Task InvokeToolAsync_WithSseResponse_ReturnsActivityResponses()
        {
            // Arrange
            var handler = new FakeSseHttpMessageHandler("event: activity\ndata: {\"type\": \"message\", \"text\": \"Tool result\", \"conversation\": {\"id\": \"conv-1\"}}\n\n");
            SetupHttpClient(handler);
            var client = CreateClient();
            var toolInput = new ToolInvocationInput { ToolSchemaName = "myTool", Parameters = new { key = "value" } };

            // Act
            var responses = new List<OrchestratedResponse>();
            await foreach (var response in client.InvokeToolAsync("conv-1", toolInput))
            {
                responses.Add(response);
            }

            // Assert
            Assert.Single(responses);
            var activityResponse = Assert.IsType<OrchestratedActivityResponse>(responses[0]);
            Assert.Equal("Tool result", activityResponse.Activity.Text);
        }

        [Fact]
        public async Task InvokeToolAsync_WithNullToolInputs_Throws()
        {
            var client = CreateClient();
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await foreach (var _ in client.InvokeToolAsync("conv-1", null!)) { }
            });
        }

        [Fact]
        public async Task InvokeToolAsync_WithNullConversationId_Throws()
        {
            var client = CreateClient();
            var toolInput = new ToolInvocationInput { ToolSchemaName = "myTool" };
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await foreach (var _ in client.InvokeToolAsync(null!, toolInput)) { }
            });
        }

        #endregion

        #region HandleUserResponseAsync Tests

        [Fact]
        public async Task HandleUserResponseAsync_WithSseResponse_ReturnsResponses()
        {
            // Arrange
            var sseData = new StringBuilder();
            sseData.AppendLine("event: activity");
            sseData.AppendLine("data: {\"type\": \"message\", \"text\": \"Response to user\", \"conversation\": {\"id\": \"conv-1\"}}");
            sseData.AppendLine();
            sseData.AppendLine("event: state");
            sseData.AppendLine("data: {\"status\": \"Completed\", \"enabledToolSchemaNames\": []}");
            sseData.AppendLine();

            var handler = new FakeSseHttpMessageHandler(sseData.ToString());
            SetupHttpClient(handler);
            var client = CreateClient();
            var activity = new Activity { Type = "message", Text = "User says hello" };

            // Act
            var responses = new List<OrchestratedResponse>();
            await foreach (var response in client.HandleUserResponseAsync("conv-1", activity))
            {
                responses.Add(response);
            }

            // Assert
            Assert.Equal(2, responses.Count);
            Assert.IsType<OrchestratedActivityResponse>(responses[0]);
            Assert.IsType<OrchestratedStateResponse>(responses[1]);
        }

        [Fact]
        public async Task HandleUserResponseAsync_WithNullActivity_Throws()
        {
            var client = CreateClient();
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await foreach (var _ in client.HandleUserResponseAsync("conv-1", null!)) { }
            });
        }

        #endregion

        #region ConversationUpdateAsync Tests

        [Fact]
        public async Task ConversationUpdateAsync_WithSseResponse_ReturnsResponses()
        {
            // Arrange
            var handler = new FakeSseHttpMessageHandler("event: state\ndata: {\"status\": \"Completed\", \"enabledToolSchemaNames\": []}\n\n");
            SetupHttpClient(handler);
            var client = CreateClient();

            // Act
            var responses = new List<OrchestratedResponse>();
            await foreach (var response in client.ConversationUpdateAsync("conv-1"))
            {
                responses.Add(response);
            }

            // Assert
            Assert.Single(responses);
            Assert.IsType<OrchestratedStateResponse>(responses[0]);
        }

        [Fact]
        public async Task ConversationUpdateAsync_WithNullConversationId_Throws()
        {
            var client = CreateClient();
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await foreach (var _ in client.ConversationUpdateAsync(null!)) { }
            });
        }

        #endregion

        #region JSON Fallback Tests

        [Fact]
        public async Task ExecuteTurnAsync_WithJsonFallback_ReturnsActivityAndState()
        {
            // Arrange
            var json = "{\"activities\": [{\"type\": \"message\", \"text\": \"From JSON\"}], \"agentState\": {\"status\": \"Completed\", \"enabledToolSchemaNames\": [\"t1\"]}}";
            var handler = new FakeJsonHttpMessageHandler(json);
            SetupHttpClient(handler);
            var client = CreateClient();

            var request = new OrchestratedTurnRequest
            {
                Orchestration = new OrchestrationRequest { Operation = OrchestrationOperation.StartConversation }
            };

            // Act
            var responses = new List<OrchestratedResponse>();
            await foreach (var response in client.ExecuteTurnAsync("conv-1", request))
            {
                responses.Add(response);
            }

            // Assert
            Assert.Equal(2, responses.Count);
            var activityResp = Assert.IsType<OrchestratedActivityResponse>(responses[0]);
            Assert.Equal("From JSON", activityResp.Activity.Text);
            var stateResp = Assert.IsType<OrchestratedStateResponse>(responses[1]);
            Assert.Equal("Completed", stateResp.AgentState.Status.Value);
        }

        [Fact]
        public async Task ExecuteTurnAsync_WithNullConversationId_Throws()
        {
            var client = CreateClient();
            var request = new OrchestratedTurnRequest
            {
                Orchestration = new OrchestrationRequest { Operation = OrchestrationOperation.StartConversation }
            };
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await foreach (var _ in client.ExecuteTurnAsync(null!, request)) { }
            });
        }

        [Fact]
        public async Task ExecuteTurnAsync_WithNullRequest_Throws()
        {
            var client = CreateClient();
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await foreach (var _ in client.ExecuteTurnAsync("conv-1", null!)) { }
            });
        }

        #endregion

        #region End Event Tests

        [Fact]
        public async Task ExecuteTurnAsync_WithSseEndEventWithData_CapturesData()
        {
            // Arrange
            var handler = new FakeSseHttpMessageHandler("event: end\ndata: end\n\n");
            SetupHttpClient(handler);
            var client = CreateClient();

            var request = new OrchestratedTurnRequest
            {
                Orchestration = new OrchestrationRequest { Operation = OrchestrationOperation.StartConversation }
            };

            // Act
            var responses = new List<OrchestratedResponse>();
            await foreach (var response in client.ExecuteTurnAsync("conv-1", request))
            {
                responses.Add(response);
            }

            // Assert
            Assert.Single(responses);
            var endResponse = Assert.IsType<OrchestratedEndResponse>(responses[0]);
            Assert.Equal("end", endResponse.Data);
        }

        [Fact]
        public async Task ExecuteTurnAsync_WithActivityThenEnd_ReturnsBothResponses()
        {
            // Arrange
            var sseData = new StringBuilder();
            sseData.AppendLine("event: activity");
            sseData.AppendLine("data: {\"type\": \"message\", \"text\": \"Hello\", \"conversation\": {\"id\": \"conv-1\"}}");
            sseData.AppendLine();
            sseData.AppendLine("event: state");
            sseData.AppendLine("data: {\"status\": \"Completed\", \"enabledToolSchemaNames\": []}");
            sseData.AppendLine();
            sseData.AppendLine("event: end");
            sseData.AppendLine("data: end");
            sseData.AppendLine();

            var handler = new FakeSseHttpMessageHandler(sseData.ToString());
            SetupHttpClient(handler);
            var client = CreateClient();

            var request = new OrchestratedTurnRequest
            {
                Orchestration = new OrchestrationRequest { Operation = OrchestrationOperation.StartConversation }
            };

            // Act
            var responses = new List<OrchestratedResponse>();
            await foreach (var response in client.ExecuteTurnAsync("conv-1", request))
            {
                responses.Add(response);
            }

            // Assert
            Assert.Equal(3, responses.Count);
            Assert.IsType<OrchestratedActivityResponse>(responses[0]);
            Assert.IsType<OrchestratedStateResponse>(responses[1]);
            Assert.IsType<OrchestratedEndResponse>(responses[2]);
        }

        #endregion

        #region HTTP Error Tests

        [Fact]
        public async Task ExecuteTurnAsync_WithHttpError_Throws()
        {
            // Arrange
            var handler = new FakeErrorHttpMessageHandler(HttpStatusCode.InternalServerError, "Server error");
            SetupHttpClient(handler);
            var client = CreateClient();

            var request = new OrchestratedTurnRequest
            {
                Orchestration = new OrchestrationRequest { Operation = OrchestrationOperation.StartConversation }
            };

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                await foreach (var _ in client.ExecuteTurnAsync("conv-1", request)) { }
            });
        }

        #endregion

        #region URL Generation Tests

        [Theory]
        [InlineData(PowerPlatformCloud.Prod, "A47151CF-4F34-488F-B377-EBE84E17B478", "bot-guid-123", "conv-1", "https://a47151cf4f34488fb377ebe84e17b4.78.environment.api.powerplatform.com/powervirtualagents/orchestrated/bot-guid-123/conversations/conv-1?api-version=2022-03-01-preview")]
        [InlineData(PowerPlatformCloud.Preprod, "A47151CF-4F34-488F-B377-EBE84E17B478", "bot-guid-456", "conv-2", "https://a47151cf4f34488fb377ebe84e17b47.8.environment.api.preprod.powerplatform.com/powervirtualagents/orchestrated/bot-guid-456/conversations/conv-2?api-version=2022-03-01-preview")]
        public void VerifyOrchestratedConnectionUrl(PowerPlatformCloud cloud, string envId, string cdsBotId, string conversationId, string expectedResult)
        {
            var settings = new ConnectionSettings(null)
            {
                EnvironmentId = envId,
                SchemaName = "ignored-for-orchestrated",
                CdsBotId = cdsBotId,
                Cloud = cloud
            };

            var uri = PowerPlatformEnvironment.GetOrchestratedConnectionUrl(settings, conversationId);
            Assert.Equal(expectedResult, uri.ToString());
        }

        [Fact]
        public void VerifyOrchestratedConnectionUrl_MissingCdsBotId_Throws()
        {
            var settings = new ConnectionSettings(null)
            {
                EnvironmentId = "test-env",
                SchemaName = "test-bot",
                CdsBotId = null
            };

            Assert.Throws<ArgumentException>(() => PowerPlatformEnvironment.GetOrchestratedConnectionUrl(settings, "conv-1"));
        }

        [Fact]
        public void VerifyOrchestratedConnectionUrl_MissingConversationId_Throws()
        {
            var settings = new ConnectionSettings(null)
            {
                EnvironmentId = "test-env",
                SchemaName = "test-bot",
                CdsBotId = "bot-id"
            };

            Assert.Throws<ArgumentException>(() => PowerPlatformEnvironment.GetOrchestratedConnectionUrl(settings, ""));
        }

        #endregion

        #region Header Tests

        [Fact]
        public async Task ExecuteTurnAsync_SendsConversationIdAndClientRequestIdHeaders()
        {
            // Arrange
            var handler = new CapturingHttpMessageHandler("event: end\ndata: done\n\n");
            SetupHttpClient(handler);
            var client = CreateClient();

            var request = new OrchestratedTurnRequest
            {
                Orchestration = new OrchestrationRequest { Operation = OrchestrationOperation.StartConversation }
            };

            // Act
            await foreach (var _ in client.ExecuteTurnAsync("conv-header-test", request)) { }

            // Assert
            Assert.NotNull(handler.CapturedRequest);

            // Verify x-ms-conversation-id header is present and matches the conversation ID
            Assert.True(handler.CapturedRequest.Headers.Contains(CopilotStudioHeaderNames.ConversationId));
            var conversationIdValues = handler.CapturedRequest.Headers.GetValues(CopilotStudioHeaderNames.ConversationId).ToList();
            Assert.Single(conversationIdValues);
            Assert.Equal("conv-header-test", conversationIdValues[0]);

            // Verify x-ms-client-request-id header is present and is a valid GUID
            Assert.True(handler.CapturedRequest.Headers.Contains(CopilotStudioHeaderNames.ClientRequestId));
            var clientRequestIdValues = handler.CapturedRequest.Headers.GetValues(CopilotStudioHeaderNames.ClientRequestId).ToList();
            Assert.Single(clientRequestIdValues);
            Assert.True(Guid.TryParse(clientRequestIdValues[0], out _), $"Expected a valid GUID but got: {clientRequestIdValues[0]}");
        }

        [Fact]
        public async Task ExecuteTurnAsync_WithRequestProperties_SendsAdditionalHeaders()
        {
            // Arrange
            var handler = new CapturingHttpMessageHandler("event: end\ndata: done\n\n");
            SetupHttpClient(handler);
            var client = CreateClient();

            var request = new OrchestratedTurnRequest
            {
                Orchestration = new OrchestrationRequest { Operation = OrchestrationOperation.StartConversation }
            };
            var properties = new RequestProperties
            {
                CorrelationId = "test-correlation-id",
                AgentVersion = "1.0.0",
                AcceptLanguage = "en-US"
            };

            // Act
            await foreach (var _ in client.ExecuteTurnAsync("conv-1", request, properties)) { }

            // Assert
            Assert.NotNull(handler.CapturedRequest);

            Assert.True(handler.CapturedRequest.Headers.Contains(CopilotStudioHeaderNames.CorrelationId));
            Assert.Equal("test-correlation-id", handler.CapturedRequest.Headers.GetValues(CopilotStudioHeaderNames.CorrelationId).Single());

            Assert.True(handler.CapturedRequest.Headers.Contains(CopilotStudioHeaderNames.AgentVersion));
            Assert.Equal("1.0.0", handler.CapturedRequest.Headers.GetValues(CopilotStudioHeaderNames.AgentVersion).Single());

            Assert.True(handler.CapturedRequest.Headers.Contains(CopilotStudioHeaderNames.AcceptLanguage));
            Assert.Equal("en-US", handler.CapturedRequest.Headers.GetValues(CopilotStudioHeaderNames.AcceptLanguage).Single());
        }

        [Fact]
        public async Task ExecuteTurnAsync_WithNullRequestProperties_DoesNotSendAdditionalHeaders()
        {
            // Arrange
            var handler = new CapturingHttpMessageHandler("event: end\ndata: done\n\n");
            SetupHttpClient(handler);
            var client = CreateClient();

            var request = new OrchestratedTurnRequest
            {
                Orchestration = new OrchestrationRequest { Operation = OrchestrationOperation.StartConversation }
            };

            // Act
            await foreach (var _ in client.ExecuteTurnAsync("conv-1", request, null)) { }

            // Assert
            Assert.NotNull(handler.CapturedRequest);
            Assert.False(handler.CapturedRequest.Headers.Contains(CopilotStudioHeaderNames.CorrelationId));
            Assert.False(handler.CapturedRequest.Headers.Contains(CopilotStudioHeaderNames.AgentVersion));
            Assert.False(handler.CapturedRequest.Headers.Contains(CopilotStudioHeaderNames.AcceptLanguage));
        }

        [Fact]
        public async Task ExecuteTurnAsync_WithPartialRequestProperties_SendsOnlyNonNullHeaders()
        {
            // Arrange
            var handler = new CapturingHttpMessageHandler("event: end\ndata: done\n\n");
            SetupHttpClient(handler);
            var client = CreateClient();

            var request = new OrchestratedTurnRequest
            {
                Orchestration = new OrchestrationRequest { Operation = OrchestrationOperation.StartConversation }
            };
            var properties = new RequestProperties
            {
                CorrelationId = "partial-correlation-id"
                // AgentVersion and AcceptLanguage intentionally omitted
            };

            // Act
            await foreach (var _ in client.ExecuteTurnAsync("conv-1", request, properties)) { }

            // Assert
            Assert.NotNull(handler.CapturedRequest);
            Assert.True(handler.CapturedRequest.Headers.Contains(CopilotStudioHeaderNames.CorrelationId));
            Assert.Equal("partial-correlation-id", handler.CapturedRequest.Headers.GetValues(CopilotStudioHeaderNames.CorrelationId).Single());
            Assert.False(handler.CapturedRequest.Headers.Contains(CopilotStudioHeaderNames.AgentVersion));
            Assert.False(handler.CapturedRequest.Headers.Contains(CopilotStudioHeaderNames.AcceptLanguage));
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullSettings_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new OrchestratedClient(null!, _httpClientFactoryMock.Object, _loggerMock.Object));
        }

        [Fact]
        public void Constructor_WithNullHttpClientFactory_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new OrchestratedClient(_settings, null!, _loggerMock.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new OrchestratedClient(_settings, _httpClientFactoryMock.Object, null!));
        }

        #endregion

        #region Helpers

        private OrchestratedClient CreateClient()
        {
            return new OrchestratedClient(_settings, _httpClientFactoryMock.Object, _loggerMock.Object);
        }

        private void SetupHttpClient(HttpMessageHandler handler)
        {
            var httpClient = new HttpClient(handler);
            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
        }

        /// <summary>
        /// Returns SSE (text/event-stream) responses.
        /// </summary>
        private class FakeSseHttpMessageHandler(string sseContent) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var content = new StringContent(sseContent, Encoding.UTF8);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
                return Task.FromResult(response);
            }
        }

        /// <summary>
        /// Returns JSON (application/json) responses.
        /// </summary>
        private class FakeJsonHttpMessageHandler(string jsonContent) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
                return Task.FromResult(response);
            }
        }

        /// <summary>
        /// Returns error HTTP responses.
        /// </summary>
        private class FakeErrorHttpMessageHandler(HttpStatusCode statusCode, string errorContent) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var content = new StringContent(errorContent, Encoding.UTF8, "application/json");
                var response = new HttpResponseMessage(statusCode) { Content = content };
                return Task.FromResult(response);
            }
        }

        /// <summary>
        /// Returns SSE responses and captures the outgoing HttpRequestMessage for assertion.
        /// </summary>
        private class CapturingHttpMessageHandler(string sseContent) : HttpMessageHandler
        {
            public HttpRequestMessage CapturedRequest { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CapturedRequest = request;
                var content = new StringContent(sseContent, Encoding.UTF8);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
                return Task.FromResult(response);
            }
        }

        #endregion
    }
}
