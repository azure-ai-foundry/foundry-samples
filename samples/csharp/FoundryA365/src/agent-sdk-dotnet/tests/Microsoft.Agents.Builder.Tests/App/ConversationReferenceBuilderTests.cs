// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Core.Models;
using System;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class ConversationReferenceBuilderTests
    {
        [Fact]
        public void Create_WithValidParameters_ReturnsReferenceBuilder()
        {
            // Arrange
            var channelId = new ChannelId(Channels.Msteams);
            var conversationId = "test-conversation-id";

            // Act
            var builder = ConversationReferenceBuilder.Create(channelId, conversationId);

            // Assert
            Assert.NotNull(builder);
            Assert.IsType<ConversationReferenceBuilder>(builder);
        }

        [Fact]
        public void Create_WithNullChannelId_ThrowsArgumentNullException()
        {
            // Arrange
            ChannelId channelId = null;
            var conversationId = "test-conversation-id";

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => ConversationReferenceBuilder.Create(channelId, conversationId));
        }

        [Fact]
        public void Create_WithNullConversationId_ThrowsArgumentNullException()
        {
            // Arrange
            var channelId = new ChannelId(Channels.Msteams);
            string conversationId = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => ConversationReferenceBuilder.Create(channelId, conversationId));
        }

        [Fact]
        public void Create_WithEmptyConversationId_ThrowsArgumentException()
        {
            // Arrange
            var channelId = new ChannelId(Channels.Msteams);
            var conversationId = string.Empty;

            // Act & Assert
            Assert.Throws<ArgumentException>(() => ConversationReferenceBuilder.Create(channelId, conversationId));
        }

        [Fact]
        public void WithUser_WithUserIdAndUserName_SetsUserCorrectly()
        {
            // Arrange
            var channelId = new ChannelId(Channels.Msteams);
            var conversationId = "test-conversation-id";
            var userId = "user-123";
            var userName = "Test User";

            // Act
            var reference = ConversationReferenceBuilder.Create(channelId, conversationId)
                .WithUser(userId, userName)
                .Build();

            // Assert
            Assert.NotNull(reference.User);
            Assert.Equal(userId, reference.User.Id);
            Assert.Equal(userName, reference.User.Name);
            Assert.Equal(RoleTypes.User, reference.User.Role);
        }

        [Fact]
        public void WithUser_WithUserIdOnly_SetsUserWithNullName()
        {
            // Arrange
            var channelId = new ChannelId(Channels.Msteams);
            var conversationId = "test-conversation-id";
            var userId = "user-123";

            // Act
            var reference = ConversationReferenceBuilder.Create(channelId, conversationId)
                .WithUser(userId)
                .Build();

            // Assert
            Assert.NotNull(reference.User);
            Assert.Equal(userId, reference.User.Id);
            Assert.Null(reference.User.Name);
            Assert.Equal(RoleTypes.User, reference.User.Role);
        }

        [Fact]
        public void WithUser_WithChannelAccount_SetsUserCorrectly()
        {
            // Arrange
            var channelId = new ChannelId(Channels.Msteams);
            var conversationId = "test-conversation-id";
            var user = new ChannelAccount("user-456", "Another User", RoleTypes.User);

            // Act
            var reference = ConversationReferenceBuilder.Create(channelId, conversationId)
                .WithUser(user)
                .Build();

            // Assert
            Assert.NotNull(reference.User);
            Assert.Equal(user.Id, reference.User.Id);
            Assert.Equal(user.Name, reference.User.Name);
            Assert.Equal(user.Role, reference.User.Role);
        }

        [Fact]
        public void WithAgent_WithAgentIdAndAgentName_SetsAgentCorrectly()
        {
            // Arrange
            var channelId = new ChannelId(Channels.Webchat);
            var conversationId = "test-conversation-id";
            var agentId = "agent-123";
            var agentName = "Test Agent";

            // Act
            var reference = ConversationReferenceBuilder.Create(channelId, conversationId)
                .WithAgent(agentId, agentName)
                .Build();

            // Assert
            Assert.NotNull(reference.Agent);
            Assert.Equal(agentId, reference.Agent.Id);
            Assert.Equal(agentName, reference.Agent.Name);
            Assert.Equal(RoleTypes.Agent, reference.Agent.Role);
        }

        [Fact]
        public void WithAgent_WithAgentIdOnly_SetsAgentWithNullName()
        {
            // Arrange
            var channelId = new ChannelId(Channels.Webchat);
            var conversationId = "test-conversation-id";
            var agentId = "agent-123";

            // Act
            var reference = ConversationReferenceBuilder.Create(channelId, conversationId)
                .WithAgent(agentId)
                .Build();

            // Assert
            Assert.NotNull(reference.Agent);
            Assert.Equal(agentId, reference.Agent.Id);
            Assert.Null(reference.Agent.Name);
            Assert.Equal(RoleTypes.Agent, reference.Agent.Role);
        }

        [Fact]
        public void WithAgent_WithChannelAccount_SetsAgentCorrectly()
        {
            // Arrange
            var channelId = new ChannelId(Channels.Msteams);
            var conversationId = "test-conversation-id";
            var agent = new ChannelAccount("agent-456", "Another Agent", RoleTypes.Agent);

            // Act
            var reference = ConversationReferenceBuilder.Create(channelId, conversationId)
                .WithAgent(agent)
                .Build();

            // Assert
            Assert.NotNull(reference.Agent);
            Assert.Equal(agent.Id, reference.Agent.Id);
            Assert.Equal(agent.Name, reference.Agent.Name);
            Assert.Equal(agent.Role, reference.Agent.Role);
        }

        [Fact]
        public void WithServiceUrl_SetsServiceUrlCorrectly()
        {
            // Arrange
            var channelId = new ChannelId(Channels.Msteams);
            var conversationId = "test-conversation-id";
            var serviceUrl = "https://custom.service.url/";

            // Act
            var reference = ConversationReferenceBuilder.Create(channelId, conversationId)
                .WithServiceUrl(serviceUrl)
                .Build();

            // Assert
            Assert.Equal(serviceUrl, reference.ServiceUrl);
        }

        [Fact]
        public void WithActivityId_SetsActivityIdCorrectly()
        {
            // Arrange
            var channelId = new ChannelId(Channels.Msteams);
            var conversationId = "test-conversation-id";
            var activityId = "activity-123";

            // Act
            var reference = ConversationReferenceBuilder.Create(channelId, conversationId)
                .WithActivityId(activityId)
                .Build();

            // Assert
            Assert.Equal(activityId, reference.ActivityId);
        }

        [Fact]
        public void WithLocale_SetsLocaleCorrectly()
        {
            // Arrange
            var channelId = new ChannelId(Channels.Msteams);
            var conversationId = "test-conversation-id";
            var locale = "en-US";

            // Act
            var reference = ConversationReferenceBuilder.Create(channelId, conversationId)
                .WithLocale(locale)
                .Build();

            // Assert
            Assert.Equal(locale, reference.Locale);
        }

        [Fact]
        public void Build_WithMsteamsChannel_SetsCorrectServiceUrl()
        {
            // Arrange
            var channelId = new ChannelId(Channels.Msteams);
            var conversationId = "test-conversation-id";

            // Act
            var reference = ConversationReferenceBuilder.Create(channelId, conversationId)
                .Build();

            // Assert
            Assert.Equal("https://smba.trafficmanager.net/teams/", reference.ServiceUrl);
        }

        [Fact]
        public void Build_WithWebchatChannel_SetsCorrectServiceUrl()
        {
            // Arrange
            var channelId = new ChannelId(Channels.Webchat);
            var conversationId = "test-conversation-id";

            // Act
            var reference = ConversationReferenceBuilder.Create(channelId, conversationId)
                .Build();

            // Assert
            Assert.Equal("https://webchat.botframework.com/", reference.ServiceUrl);
        }

        [Fact]
        public void Build_WithDirectlineChannel_SetsCorrectServiceUrl()
        {
            // Arrange
            var channelId = new ChannelId(Channels.Directline);
            var conversationId = "test-conversation-id";

            // Act
            var reference = ConversationReferenceBuilder.Create(channelId, conversationId)
                .Build();

            // Assert
            Assert.Equal("https://directline.botframework.com/", reference.ServiceUrl);
        }

        [Fact]
        public void Build_WithUnknownChannel_SetsNullServiceUrl()
        {
            // Arrange
            var channelId = new ChannelId("unknown-channel");
            var conversationId = "test-conversation-id";

            // Act
            var reference = ConversationReferenceBuilder.Create(channelId, conversationId)
                .Build();

            // Assert
            Assert.NotNull(reference.ServiceUrl);
        }

        [Fact]
        public void Build_WithoutSettingUser_CreatesDefaultUser()
        {
            // Arrange
            var channelId = new ChannelId(Channels.Msteams);
            var conversationId = "test-conversation-id";

            // Act
            var reference = ConversationReferenceBuilder.Create(channelId, conversationId)
                .Build();

            // Assert
            Assert.NotNull(reference.User);
            Assert.Equal(RoleTypes.User, reference.User.Role);
        }

        [Fact]
        public void Build_WithoutSettingAgent_CreatesDefaultAgent()
        {
            // Arrange
            var channelId = new ChannelId(Channels.Msteams);
            var conversationId = "test-conversation-id";

            // Act
            var reference = ConversationReferenceBuilder.Create(channelId, conversationId)
                .Build();

            // Assert
            Assert.NotNull(reference.Agent);
            Assert.Equal(RoleTypes.Agent, reference.Agent.Role);
        }

        [Fact]
        public void Build_WithCustomServiceUrl_DoesNotOverrideServiceUrl()
        {
            // Arrange
            var channelId = new ChannelId(Channels.Msteams);
            var conversationId = "test-conversation-id";
            var customServiceUrl = "https://custom.url/";

            // Act
            var reference = ConversationReferenceBuilder.Create(channelId, conversationId)
                .WithServiceUrl(customServiceUrl)
                .Build();

            // Assert
            Assert.Equal(customServiceUrl, reference.ServiceUrl);
        }

        [Fact]
        public void Build_SetsChannelIdCorrectly()
        {
            // Arrange
            var channelId = new ChannelId(Channels.Msteams);
            var conversationId = "test-conversation-id";

            // Act
            var reference = ConversationReferenceBuilder.Create(channelId, conversationId)
                .Build();

            // Assert
            Assert.Equal(channelId, reference.ChannelId);
        }

        [Fact]
        public void Build_SetsConversationCorrectly()
        {
            // Arrange
            var channelId = new ChannelId(Channels.Msteams);
            var conversationId = "test-conversation-id";

            // Act
            var reference = ConversationReferenceBuilder.Create(channelId, conversationId)
                .Build();

            // Assert
            Assert.NotNull(reference.Conversation);
            Assert.Equal(conversationId, reference.Conversation.Id);
        }

        [Fact]
        public void FluentInterface_AllMethodsChainCorrectly()
        {
            // Arrange
            var channelId = new ChannelId(Channels.Webchat);
            var conversationId = "test-conversation-id";
            var userId = "user-123";
            var userName = "Test User";
            var agentId = "agent-123";
            var agentName = "Test Agent";
            var serviceUrl = "https://custom.url/";
            var activityId = "activity-123";
            var locale = "en-US";

            // Act
            var reference = ConversationReferenceBuilder.Create(channelId, conversationId)
                .WithUser(userId, userName)
                .WithAgent(agentId, agentName)
                .WithServiceUrl(serviceUrl)
                .WithActivityId(activityId)
                .WithLocale(locale)
                .Build();

            // Assert
            Assert.NotNull(reference);
            Assert.Equal(channelId, reference.ChannelId);
            Assert.Equal(conversationId, reference.Conversation.Id);
            Assert.Equal(userId, reference.User.Id);
            Assert.Equal(userName, reference.User.Name);
            Assert.Equal(agentId, reference.Agent.Id);
            Assert.Equal(agentName, reference.Agent.Name);
            Assert.Equal(serviceUrl, reference.ServiceUrl);
            Assert.Equal(activityId, reference.ActivityId);
            Assert.Equal(locale, reference.Locale);
        }

        [Fact]
        public void Build_WithM365CopilotChannel_SetsCorrectServiceUrl()
        {
            // Arrange
            var channelId = new ChannelId(Channels.M365Copilot);
            var conversationId = "test-conversation-id";

            // Act
            var reference = ConversationReferenceBuilder.Create(channelId, conversationId)
                .Build();

            // Assert - M365Copilot is a subchannel of Msteams
            Assert.Equal("https://smba.trafficmanager.net/teams/", reference.ServiceUrl);
        }
    }
}