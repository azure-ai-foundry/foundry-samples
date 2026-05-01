// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Client.Tests
{
    public class ConfigurationAgentHostTests
    {
        private readonly Mock<IKeyedServiceProvider> _provider = new();
        private readonly Mock<IConnections> _connections = new();
        private readonly IConfigurationRoot _config = new ConfigurationBuilder().Build();
        private readonly Mock<IHttpClientFactory> _httpClientFactory = new();
        private readonly IStorage _storage = new MemoryStorage();

        [Fact]
        public void Constructor_ShouldThrowOnEmptyConfigSection()
        {
            Assert.Throws<ArgumentException>(() => new ConfigurationAgentHost(_config, _provider.Object, _storage, _connections.Object, _httpClientFactory.Object, ""));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullServiceProvider()
        {
            Assert.Throws<ArgumentNullException>(() => new ConfigurationAgentHost(_config, null, _storage, _connections.Object, _httpClientFactory.Object));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullConnections()
        {
            Assert.Throws<ArgumentNullException>(() => new ConfigurationAgentHost(_config, _provider.Object, _storage, null, _httpClientFactory.Object));
        }

        [Fact]
        public void Constructor_ShouldSetProperties()
        {
            var botName = "bot1";
            var botClientId = "123";
            var botTokenProvider = "ServiceConnection";
            var botEndpoint = "http://localhost/api/messages";
            var DefaultResponseEndpoint = "http://localhost/";
            var sections = new Dictionary<string, string>{
                {$"Agent:Host:Agents:{botName}:ConnectionSettings:ClientId", botClientId},
                {$"Agent:Host:Agents:{botName}:ConnectionSettings:TokenProvider", botTokenProvider},
                {$"Agent:Host:Agents:{botName}:ConnectionSettings:Endpoint", botEndpoint},
                {"Agent:Host:DefaultResponseEndpoint", DefaultResponseEndpoint},
                {"Agent:ClientId", botClientId},
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(sections)
                .Build();

            var host = new ConfigurationAgentHost(config, _provider.Object, _storage, _connections.Object, _httpClientFactory.Object);

            Assert.Single(host._agents);
            Assert.Equal(botClientId, host._agents[botName].ConnectionSettings.ClientId);
            Assert.Equal(DefaultResponseEndpoint, host.DefaultResponseEndpoint.ToString());
            Assert.Equal(botClientId, host.HostClientId);
        }

        [Fact]
        public void GetChannel_ShouldThrowOnEmptyName()
        {
            var sections = new Dictionary<string, string>{
                {"Agent:Host:DefaultResponseEndpoint", "http://localhost"},
                {"Agent:ClientId", "hostClientId"},
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(sections)
                .Build();

            var host = new ConfigurationAgentHost(config, _provider.Object, _storage, _connections.Object, _httpClientFactory.Object);

            Assert.Throws<ArgumentException>(() => host.GetClient(string.Empty));
        }

        [Fact]
        public void GetChannel_ShouldThrowOnUnknownChannel()
        {
            var botName = "botName";
            var botAlias = "bot1";
            var botClientId = "123";
            var botEndpoint = "http://localhost/api/messages";
            var botTokenProvider = "ServiceConnection";
            var DefaultResponseEndpoint = "http://localhost/";
            var sections = new Dictionary<string, string>{
                {$"Agent:Host:Agents:{botName}:Alias", botAlias},
                {$"Agent:Host:Agents:{botName}:ConnectionSettings:ClientId", botClientId},
                {$"Agent:Host:Agents:{botName}:ConnectionSettings:Endpoint", botEndpoint},
                {$"Agent:Host:Agents:{botName}:ConnectionSettings:TokenProvider", botTokenProvider},
                {"Agent:Host:DefaultResponseEndpoint", DefaultResponseEndpoint},
                {"Agent:ClientId", botClientId},
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(sections)
                .Build();

            var host = new ConfigurationAgentHost(config, _provider.Object, _storage, _connections.Object, _httpClientFactory.Object);

            Assert.Throws<ArgumentException>(() => host.GetClient("random"));
        }

        [Fact]
        public void GetChannel_ShouldThrowOnEmptyChannelTokenProvider()
        {
            var botName = "botName";
            var botAlias = "bot1";
            var botClientId = "123";
            var botEndpoint = "http://localhost/api/messages";
            var botTokenProvider = "";
            var DefaultResponseEndpoint = "http://localhost/";
            var sections = new Dictionary<string, string>{
                {$"Agent:Host:Agents:{botName}:Alias", botAlias},
                {$"Agent:Host:Agents:{botName}:ConnectionSettings:ClientId", botClientId},
                {$"Agent:Host:Agents:{botName}:ConnectionSettings:Endpoint", botEndpoint},
                {$"Agent:Host:Agents:{botName}:ConnectionSettings:TokenProvider", botTokenProvider},
                {"Agent:Host:DefaultResponseEndpoint", DefaultResponseEndpoint},
                {"Agent:ClientId", botClientId},
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(sections)
                .Build();

            Assert.Throws<ArgumentException>(() => new ConfigurationAgentHost(config, _provider.Object, _storage, _connections.Object, _httpClientFactory.Object));
        }

        [Fact]
        public void GetChannel_ShouldThrowOnNullConnection()
        {
            Assert.Throws<ArgumentNullException>(() => new ConfigurationAgentHost(_config, _provider.Object, _storage, null, _httpClientFactory.Object));
        }

        [Fact]
        public async Task Conversation_CreateDelete()
        {
            // arrange
            var botName = "botName";
            var botAlias = "bot1";
            var botClientId = "123";
            var botTokenProvider = "ServiceConnection";
            var botEndpoint = "http://localhost/api/messages";
            var DefaultResponseEndpoint = "http://localhost/";
            var hostId = "hostId";
            var sections = new Dictionary<string, string>{
                {$"Agent:Host:Agents:{botName}:Alias", botAlias},
                {$"Agent:Host:Agents:{botName}:ConnectionSettings:ClientId", botClientId},
                {$"Agent:Host:Agents:{botName}:ConnectionSettings:TokenProvider", botTokenProvider},
                {$"Agent:Host:Agents:{botName}:ConnectionSettings:Endpoint", botEndpoint},
                {"Agent:Host:DefaultResponseEndpoint", DefaultResponseEndpoint},
                {"Agent:ClientId", hostId},
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(sections)
                .Build();

            var host = new ConfigurationAgentHost(config, _provider.Object, _storage, _connections.Object, _httpClientFactory.Object);


            // act
            var activity = new Activity()
            {
                Type = ActivityTypes.Message,
                Id = "1234",
                ChannelId = "webchat",
                Conversation = new ConversationAccount()
                {
                    Id = "1"
                },
                From = new ChannelAccount()
                {
                    Id = "me@from.com"
                }
            };
            var turnContext = new TurnContext(new TestAdapter(), activity)
            {
                Identity = new ClaimsIdentity(
                [
                    new(AuthenticationConstants.AudienceClaim, host.HostClientId),
                    new(AuthenticationConstants.AppIdClaim, host.HostClientId),
                ])
            };
            
            var turnState = new TurnState(_storage);
            await turnState.LoadStateAsync(turnContext);

            // should be no conversation for bot
            Assert.Null(await host.GetConversation(turnContext, botName));

            // create a new conversation
            var conversationId = await host.GetOrCreateConversationAsync(turnContext, botName);
            Assert.NotNull(conversationId);
            Assert.Equal(conversationId, await host.GetConversation(turnContext, botName));

            // single conversation per agent
            var secondCreateConversationId = await host.GetOrCreateConversationAsync(turnContext, botName);
            Assert.Equal(conversationId, secondCreateConversationId);

            // Verify ConversationIdFactory stored the reference
            var idState = await _storage.ReadAsync([conversationId], CancellationToken.None);
            Assert.Single(idState);

            // delete conversation
            await host.DeleteConversationAsync(turnContext, conversationId);
            Assert.Null(await host.GetConversation(turnContext, botName));

            // Verify ConversationIdFactory deleted the reference
            idState = await _storage.ReadAsync([conversationId, $"{turnContext.Activity.Conversation.Id}/agentconversations"], CancellationToken.None);
            Assert.Empty(idState);
        }

        [Fact]
        public async Task Conversation_MultiChannel()
        {
            // arrange
            var agent1Name = "bot1Name";
            var agent2Name = "bot2Name";
            var sections = new Dictionary<string, string>{
                {$"Agent:Host:Agents:{agent1Name}:ConnectionSettings:ClientId", "123"},
                {$"Agent:Host:Agents:{agent1Name}:ConnectionSettings:TokenProvider", "ServiceConnection"},
                {$"Agent:Host:Agents:{agent1Name}:ConnectionSettings:Endpoint", "http://localhost/api/messages"},
                {$"Agent:Host:Agents:{agent2Name}:ConnectionSettings:ClientId", "456"},
                {$"Agent:Host:Agents:{agent2Name}:ConnectionSettings:TokenProvider", "ServiceConnection"},
                {$"Agent:Host:Agents:{agent2Name}:ConnectionSettings:Endpoint", "http://localhost/api/messages"},
                {"Agent:Host:DefaultResponseEndpoint", "http://localhost/"},
                {"Agent:ClientId", "hostId"},
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(sections)
                .Build();

            var host = new ConfigurationAgentHost(config, _provider.Object, _storage, _connections.Object, _httpClientFactory.Object);


            // act
            var activity = new Activity()
            {
                Type = ActivityTypes.Message,
                Id = "1234",
                ChannelId = "webchat",
                Conversation = new ConversationAccount()
                {
                    Id = "1"
                },
                From = new ChannelAccount()
                {
                    Id = "me@from.com"
                }
            };
            var turnContext = new TurnContext(new TestAdapter(), activity)
            {
                Identity = new ClaimsIdentity(
                [
                    new(AuthenticationConstants.AudienceClaim, host.HostClientId),
                    new(AuthenticationConstants.AppIdClaim, host.HostClientId),
                ])
            };

            var turnState = new TurnState(_storage);
            await turnState.LoadStateAsync(turnContext);

            // create a new conversation for agent1
            var conversationId1 = await host.GetOrCreateConversationAsync(turnContext, agent1Name);
            Assert.NotNull(conversationId1);
            Assert.Equal(conversationId1, await host.GetConversation(turnContext, agent1Name));

            // create a new conversation for agent2
            var conversationId2 = await host.GetOrCreateConversationAsync(turnContext, agent2Name);
            Assert.NotNull(conversationId2);
            Assert.Equal(conversationId2, await host.GetConversation(turnContext, agent2Name));

            // Should have two existing conversations
            var conversations = await host.GetConversations(turnContext);
            Assert.Equal(2, conversations.Count);
            Assert.Equal(conversationId1, conversations.Where(c => c.AgentName == agent1Name).First().AgentConversationId);
            Assert.Equal(conversationId2, conversations.Where(c => c.AgentName == agent2Name).First().AgentConversationId);

            // delete conversation
            await host.DeleteConversationAsync(turnContext, conversationId1);
            await host.DeleteConversationAsync(turnContext, conversationId2);
            Assert.Null(await host.GetConversation(turnContext, agent1Name));
            Assert.Null(await host.GetConversation(turnContext, agent2Name));
            conversations = await host.GetConversations(turnContext);
            Assert.Empty(conversations);
        }
    }
}