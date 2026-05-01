// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.CopilotStudio.Client.Discovery;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.CopilotStudio.Client.Tests
{
    public class CopilotClientTests
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<ILogger<CopilotClient>> _loggerMock;
        private readonly ConnectionSettings _settings;

        public CopilotClientTests()
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _loggerMock = new Mock<ILogger<CopilotClient>>();
            _settings = new ConnectionSettings(null)
            {
                EnvironmentId = "test-env",
                SchemaName = "test-bot"
            };
        }

        [Fact]
        public async Task StartConversationAsync_ShouldReturnActivities()
        {
            // Arrange
            var httpClient = new HttpClient(new FakeHttpMessageHandler());
            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
            var client = new CopilotClient(_settings, _httpClientFactoryMock.Object, _loggerMock.Object);

            // Act
            var activities = client.StartConversationAsync();

            // Assert
            await foreach (var activity in activities)
            {
                Assert.NotNull(activity);
            }
        }

        [Fact]
        public async Task AskQuestionAsync_ShouldReturnActivities()
        {
            // Arrange
            var httpClient = new HttpClient(new FakeHttpMessageHandler());
            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
            var client = new CopilotClient(_settings, _httpClientFactoryMock.Object, _loggerMock.Object);

            // Act
            var activities = client.AskQuestionAsync("test question");

            // Assert
            await foreach (var activity in activities)
            {
                Assert.NotNull(activity);
            }
        }

        [Theory]
        [InlineData(PowerPlatformCloud.Other, AgentType.Published, "Bot01" , "A47151CF-4F34-488F-B377-EBE84E17B478", "foo.api.com" , "" , "https://a47151cf4f34488fb377ebe84e17b47.8.environment.foo.api.com/copilotstudio/dataverse-backed/authenticated/bots/Bot01/conversations?api-version=2022-03-01-preview")]
        [InlineData(PowerPlatformCloud.Preprod, AgentType.Published, "Bot01", "A47151CF-4F34-488F-B377-EBE84E17B478", "", "", "https://a47151cf4f34488fb377ebe84e17b47.8.environment.api.preprod.powerplatform.com/copilotstudio/dataverse-backed/authenticated/bots/Bot01/conversations?api-version=2022-03-01-preview")]
        [InlineData(PowerPlatformCloud.Prod, AgentType.Published, "Bot01", "A47151CF-4F34-488F-B377-EBE84E17B478", "", "", "https://a47151cf4f34488fb377ebe84e17b4.78.environment.api.powerplatform.com/copilotstudio/dataverse-backed/authenticated/bots/Bot01/conversations?api-version=2022-03-01-preview")]
        [InlineData(PowerPlatformCloud.FirstRelease, AgentType.Published, "Bot01", "A47151CF-4F34-488F-B377-EBE84E17B478", "", "", "https://a47151cf4f34488fb377ebe84e17b4.78.environment.api.powerplatform.com/copilotstudio/dataverse-backed/authenticated/bots/Bot01/conversations?api-version=2022-03-01-preview")]
        [InlineData(PowerPlatformCloud.FirstRelease, AgentType.Published, "Bot01", "A47151CF-4F34-488F-B377-EBE84E17B478", "", "1234", "https://a47151cf4f34488fb377ebe84e17b4.78.environment.api.powerplatform.com/copilotstudio/dataverse-backed/authenticated/bots/Bot01/conversations/1234?api-version=2022-03-01-preview")]
        [InlineData(PowerPlatformCloud.Prod, AgentType.Prebuilt, "Bot01", "A47151CF-4F34-488F-B377-EBE84E17B478", "", "1234", "https://a47151cf4f34488fb377ebe84e17b4.78.environment.api.powerplatform.com/copilotstudio/prebuilt/authenticated/bots/Bot01/conversations/1234?api-version=2022-03-01-preview")]
        [InlineData(PowerPlatformCloud.Other, AgentType.Prebuilt, "Bot01", "A47151CF-4F34-488F-B377-EBE84E17B478", "Blah+1_ Blah", "1234", "https://a47151cf4f34488fb377ebe84e17b4.78.environment.api.powerplatform.com/copilotstudio/prebuilt/authenticated/bots/Bot01/conversations/1234?api-version=2022-03-01-preview", true)]
        public void VerifyConnectionUrl( PowerPlatformCloud cloud, AgentType botType, string botId, string envId, string customCloud, string conversationId , string expectedResult,bool shouldthrow = false )
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                                { "ConnectionTest:EnvironmentId", envId },
                                { "ConnectionTest:Cloud", cloud.ToString() },
                                { "ConnectionTest:CustomPowerPlatformCloud", customCloud },
                                { "ConnectionTest:SchemaName", botId },
                                { "ConnectionTest:CopilotAgentType", botType.ToString() }
                })
                .Build();
            ConnectionSettings settings = new ConnectionSettings(configuration.GetSection("ConnectionTest"));
            if (shouldthrow)
            {
                Assert.Throws<ArgumentException>(() => PowerPlatformEnvironment.GetCopilotStudioConnectionUrl(settings, conversationId));
                return;
            }
            else
            {
                var uri = PowerPlatformEnvironment.GetCopilotStudioConnectionUrl(settings, conversationId);
                Assert.True(uri.ToString() == expectedResult);
            }

        }

        [Theory]
        [InlineData(PowerPlatformCloud.Prod, "", "https://api.powerplatform.com/.default", false)]
        [InlineData(PowerPlatformCloud.Preprod, "", "https://api.preprod.powerplatform.com/.default", false)]
        [InlineData(PowerPlatformCloud.Mooncake, "", "https://api.powerplatform.partner.microsoftonline.cn/.default", false)]
        [InlineData(PowerPlatformCloud.FirstRelease, "", "https://api.powerplatform.com/.default", false)]
        [InlineData(PowerPlatformCloud.Other, "fido.com", "https://fido.com/.default", false)]
        [InlineData(PowerPlatformCloud.Unknown, "", "", true)]
        public void VerifyAgentScopeTest(PowerPlatformCloud cloud , string cloudBaseAddress, string expectedAuthority, bool shouldthrow = false)
        {
            ConnectionSettings settings = new ConnectionSettings(null)
            {
                EnvironmentId = "A47151CF-4F34-488F-B377-EBE84E17B478",
                Cloud = cloud,
                SchemaName = "Bot01", 
                CopilotAgentType = AgentType.Published,
                CustomPowerPlatformCloud = cloudBaseAddress
            };

            if (shouldthrow)
            {
                Assert.Throws<ArgumentException>(() => CopilotClient.ScopeFromSettings(settings)); 
            }
            else
            {
                var scope = CopilotClient.ScopeFromSettings(settings);
                Assert.True(scope == expectedAuthority);
            }
        }

        private class FakeHttpMessageHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var content = new StringContent("data: {\"type\": \"message\", \"conversation\": {\"id\": \"test-convo\"}}");
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream"); 
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = content
                };
                return Task.FromResult(response);
            }
        }
    }
}
