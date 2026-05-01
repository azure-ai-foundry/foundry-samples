// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
#if !NETFRAMEWORK
using static System.Runtime.InteropServices.JavaScript.JSType;
#endif

namespace Microsoft.Agents.Client.Tests
{
    public class AgentResponseExtensionTests
    {
        private readonly IAgentHost _agentHost;
        private readonly Mock<IKeyedServiceProvider> _provider = new();
        private readonly IStorage _storage = new MemoryStorage();
        private readonly Mock<IConnections> _connections = new();
        private readonly Mock<IHttpClientFactory> _httpClientFactory = new();
        private readonly Mock<IAccessTokenProvider> _accessTokenProvider = new Mock<IAccessTokenProvider>();

        public AgentResponseExtensionTests() 
        {
            var agent1Name = "bot1";
            var agent2Name = "bot2";
            var botClientId = "123";
            var botTokenProvider = "ServiceConnection";
            var botEndpoint = "http://localhost/api/messages";
            var DefaultResponseEndpoint = "http://localhost/";
            var sections = new Dictionary<string, string>{
                {$"Agent:Host:Agents:{agent1Name}:ConnectionSettings:ClientId", botClientId},
                {$"Agent:Host:Agents:{agent1Name}:ConnectionSettings:TokenProvider", botTokenProvider},
                {$"Agent:Host:Agents:{agent1Name}:ConnectionSettings:Endpoint", botEndpoint},
                {$"Agent:Host:Agents:{agent2Name}:ConnectionSettings:ClientId", botClientId},
                {$"Agent:Host:Agents:{agent2Name}:ConnectionSettings:TokenProvider", botTokenProvider},
                {$"Agent:Host:Agents:{agent2Name}:ConnectionSettings:Endpoint", botEndpoint},
                {"Agent:Host:DefaultResponseEndpoint", DefaultResponseEndpoint},
                {"Agent:ClientId", botClientId},
            };
            var config = new ConfigurationBuilder()
            .AddInMemoryCollection(sections)
            .Build();
            _agentHost = new ConfigurationAgentHost(config, _provider.Object, _storage, _connections.Object, _httpClientFactory.Object);

            _accessTokenProvider
                .Setup(p => p.GetAccessTokenAsync(It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<bool>()))
                .Returns(Task.FromResult("token"));

            IAccessTokenProvider provider = _accessTokenProvider.Object;
            _connections
                .Setup(c => c.TryGetConnection(It.IsAny<string>(), out provider))
                .Returns(true);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DefaultEOCHandling(bool error)
        {
            int eocCount = 0;
            bool eocHandlerCalled = false;
            bool secondOnErrorCalled = false;

            _httpClientFactory
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(() => new HttpClient(new TestHttpMessageHandler()
                {
                    HttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK),
                    SendAssert = (activity, count) =>
                    {
                        // Each Agent should get an EOC sent to it.
                        Assert.Equal(ActivityTypes.EndOfConversation, activity.Type);
                        eocCount++;
                    },
                }));

            // Setup AgentApplication
            var app = new AgentApplication(new AgentApplicationOptions(_storage));
            app.RegisterExtension(new AgentResponses(app, _agentHost), (extension) =>
            {
                extension.OnAgentReply((turnContext, turnState, reference, agentActivity, cancellationToken) =>
                {
                    return Task.CompletedTask;
                });

                extension.AddDefaultEndOfConversationHandling((turnContext, turnState, cancellationToken) =>
                {
                    eocHandlerCalled = true;
                    return Task.CompletedTask;
                });
            });

            app.OnActivity(ActivityTypes.Message, async (turnContext, turnState, cancellationToken) =>
            {
                // Create two conversations
                await _agentHost.GetOrCreateConversationAsync(turnContext, "bot1");
                await _agentHost.GetOrCreateConversationAsync(turnContext, "bot2");
                Assert.Equal(2, (await _agentHost.GetConversations(turnContext, cancellationToken)).Count);

                if (error)
                {
                    throw new Exception("testException");
                }
            });

            // AddDefaultEndOfConversationHandling sets up one OnTurnError, but adding another that
            // that will get called after that to assert state.
            app.OnTurnError(async (turnContext, turnState, exception, cancellationToken) =>
            {
                // Conversations should have been cleared by the time the second handler is called.
                secondOnErrorCalled = true;
                Assert.Empty(await _agentHost.GetConversations(turnContext, CancellationToken.None));
            });

            // Act
            TurnContext turnContext = CreateTurnContext(new Activity()
            {
                Type = ActivityTypes.Message,
                ChannelId = "test",
                Conversation = new ConversationAccount() { Id = "1" },
                From = new ChannelAccount() { Id = "from" }
            });

            if (error)
            {
                // Call OnTurn directly.  It's going to throw because OnActivity did
                await Assert.ThrowsAsync<Exception>(() => app.OnTurnAsync(turnContext, CancellationToken.None));

                Assert.True(secondOnErrorCalled);
            }
            else
            {
                // This will create the two conversations
                turnContext = CreateTurnContext(new Activity()
                {
                    Type = ActivityTypes.Message,
                    ChannelId = "test",
                    Conversation = new ConversationAccount() { Id = "1" },
                    From = new ChannelAccount() { Id = "from" }
                });

                await app.OnTurnAsync(turnContext, CancellationToken.None);

                // Send eoc, which should end both
                turnContext = CreateTurnContext(new Activity()
                {
                    Type = ActivityTypes.EndOfConversation,
                    ChannelId = "test",
                    Conversation = new ConversationAccount() { Id = "1" },
                    From = new ChannelAccount() { Id = "from" }
                });

                await app.OnTurnAsync(turnContext, CancellationToken.None);
                Assert.Empty(await _agentHost.GetConversations(turnContext, CancellationToken.None));
            }

            // handler passed to AddDefaultEndOfConversationHandling should have been called
            Assert.True(eocHandlerCalled);

            // Two EOC's sent (once for each Agent)
            Assert.Equal(2, eocCount);
        }

        [Fact]
        public async Task ResponseHandler_NoConversationTest()
        {
            // Setup AgentApplication
            var app = new AgentApplication(new AgentApplicationOptions(_storage));
            var responseHandler = new AdapterChannelResponseHandler(new TestAdapter(), app, _agentHost, NullLogger<AdapterChannelResponseHandler>.Instance);

            // Act

            var agent2Activity = new Activity()
            {
                Type = ActivityTypes.Message,
                ChannelId = "test",
                Conversation = new ConversationAccount() { Id = "fakConversationId" },
                From = new ChannelAccount() { Id = "from" }
            };

            var agent2Identity = new ClaimsIdentity(
            [
                new(AuthenticationConstants.AudienceClaim, _agentHost.HostClientId),
                new(AuthenticationConstants.AppIdClaim, _agentHost.HostClientId),
            ]);

            await Assert.ThrowsAsync<KeyNotFoundException>(() => responseHandler.OnSendToConversationAsync(agent2Identity, "fakConversationId", agent2Activity));
        }

        [Fact]
        public async Task ResponseHandler_EventCreated()
        {
            bool eventReceived = false;

            // Setup
            TurnContext turnContext = CreateTurnContext(new Activity()
            {
                Type = ActivityTypes.Message,
                ChannelId = "webchat",
                ServiceUrl = "https://serviceUrl.com",
                Conversation = new ConversationAccount() { Id = "1" },
                From = new ChannelAccount() { Id = "from" }
            });

            var agent2ConversationId = await _agentHost.GetOrCreateConversationAsync(turnContext, "bot1");

            // Setup AgentApplication
            var adapter = new TestAdapter();
            var app = new AgentApplication(new AgentApplicationOptions(_storage));

            app.OnActivity(ActivityTypes.Event, (turnContext, turnState, cancellationToken) =>
            {
                if (turnContext.Activity.Type == ActivityTypes.Event && turnContext.Activity.Name == AdapterChannelResponseHandler.ChannelReplyEventName)
                {
                    Assert.NotNull(turnContext.Activity.Value);
                    var channelReplyValue = ProtocolJsonSerializer.ToObject<AdapterChannelResponseHandler.ChannelReply>(turnContext.Activity.Value);
                    Assert.Equal(agent2ConversationId, channelReplyValue.Activity.Conversation.Id);
                    Assert.Equal("1", channelReplyValue.ChannelConversationReference.ConversationReference.Conversation.Id);
                    Assert.Equal("webchat", channelReplyValue.ChannelConversationReference.ConversationReference.ChannelId);
                    Assert.Equal("https://serviceUrl.com", channelReplyValue.ChannelConversationReference.ConversationReference.ServiceUrl);
                    eventReceived = true;
                }
                return Task.CompletedTask;
            });

            var responseHandler = new AdapterChannelResponseHandler(adapter, app, _agentHost, NullLogger<AdapterChannelResponseHandler>.Instance);

            // Act

            var agent2Activity = new Activity()
            {
                Type = ActivityTypes.Message,
                ChannelId = "test",
                Conversation = new ConversationAccount() { Id = agent2ConversationId },
                From = new ChannelAccount() { Id = "from" }
            };

            var agent2Identity = new ClaimsIdentity(
            [
                new(AuthenticationConstants.AudienceClaim, _agentHost.HostClientId),
                new(AuthenticationConstants.AppIdClaim, _agentHost.HostClientId),
            ]);

            await responseHandler.OnSendToConversationAsync(agent2Identity, agent2ConversationId, agent2Activity);
            Assert.True(eventReceived);
        }

        private TurnContext CreateTurnContext(IActivity activity)
        {
            return new TurnContext(new TestAdapter(), activity)
            {
                Identity = new ClaimsIdentity(
                [
                    new(AuthenticationConstants.AudienceClaim, _agentHost.HostClientId),
                    new(AuthenticationConstants.AppIdClaim, _agentHost.HostClientId),
                ])
            };
        }
    }
}
