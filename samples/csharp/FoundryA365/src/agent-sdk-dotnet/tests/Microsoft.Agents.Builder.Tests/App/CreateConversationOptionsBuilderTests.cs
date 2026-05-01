// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class CreateConversationOptionsBuilderTests
    {
        private const string TestAgentClientId = "test-agent-id";
        private const string TestServiceUrl = "https://test.service.url/";
        private const string TestUserId = "test-user-id";
        private const string TestUserName = "Test User";
        private const string TestScope = "test-scope";
        private const string TestTopicName = "Test Topic";
        private const string TestTenantId = "test-tenant-id";

        #region Create with AgentClientId Tests

        [Fact]
        public void Create_WithAgentClientId_ShouldReturnBuilder()
        {
            // Act
            var builder = CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams);

            // Assert
            Assert.NotNull(builder);
        }

        [Fact]
        public void Create_WithAgentClientId_ShouldThrowOnNullAgentClientId()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => CreateConversationOptionsBuilder.Create((string) null, Channels.Msteams));
        }

        [Fact]
        public void Create_WithAgentClientId_ShouldThrowOnEmptyAgentClientId()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => CreateConversationOptionsBuilder.Create(string.Empty, Channels.Msteams));
        }

        [Fact]
        public void Create_WithAgentClientId_ShouldThrowOnWhitespaceAgentClientId()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => CreateConversationOptionsBuilder.Create("   ", Channels.Msteams));
        }

        [Fact]
        public void Create_WithAgentClientId_ShouldThrowOnNullChannelId()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => CreateConversationOptionsBuilder.Create(TestAgentClientId, (ChannelId)null));
        }

        [Fact]
        public void Create_WithAgentClientId_ShouldInitializeWithDefaultParameters()
        {
            // Act
            var result = CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams)
                .WithUser(TestUserId, TestUserName)
                .Build();

            // Assert
            Assert.NotNull(result.Parameters);
            Assert.NotNull(result.Parameters.Agent);
            Assert.Equal(TestAgentClientId, result.Parameters.Agent.Id);
        }

        [Fact]
        public void Create_WithAgentClientId_AndParameters_ShouldUseProvidedParameters()
        {
            // Arrange
            var parameters = new ConversationParameters
            {
                IsGroup = true,
                TopicName = "Custom Topic"
            };

            // Act
            var result = CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams, parameters: parameters)
                .WithUser(TestUserId, TestUserName)
                .Build();

            // Assert
            Assert.True(result.Parameters.IsGroup);
            Assert.Equal("Custom Topic", result.Parameters.TopicName);
        }

        [Fact]
        public void Create_WithAgentClientId_ShouldSetAgentIfNotProvidedInParameters()
        {
            // Arrange
            var parameters = new ConversationParameters();

            // Act
            var result = CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams, parameters: parameters)
                .WithUser(TestUserId, TestUserName)
                .Build();

            // Assert
            Assert.NotNull(result.Parameters.Agent);
            Assert.Equal(TestAgentClientId, result.Parameters.Agent.Id);
        }

        [Fact]
        public void Create_WithAgentClientId_ShouldNotOverrideAgentIfProvidedInParameters()
        {
            // Arrange
            var customAgent = new ChannelAccount("custom-agent-id", "Custom Agent");
            var parameters = new ConversationParameters
            {
                Agent = customAgent
            };

            // Act
            var result = CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams, parameters: parameters)
                .WithUser(TestUserId, TestUserName)
                .Build();

            // Assert
            Assert.Equal("custom-agent-id", result.Parameters.Agent.Id);
            Assert.Equal("Custom Agent", result.Parameters.Agent.Name);
        }

        #endregion

        #region Create with Claims Tests

        [Fact]
        public void Create_WithClaims_ShouldReturnBuilder()
        {
            // Arrange
            var claims = new Dictionary<string, string>
            {
                { "aud", TestAgentClientId }
            };

            // Act
            var builder = CreateConversationOptionsBuilder.Create(claims, Channels.Msteams);

            // Assert
            Assert.NotNull(builder);
        }

        [Fact]
        public void Create_WithClaims_ShouldThrowOnNullClaims()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => CreateConversationOptionsBuilder.Create((IDictionary<string, string>)null, Channels.Msteams));
        }

        [Fact]
        public void Create_WithClaims_ShouldThrowOnNullChannelId()
        {
            // Arrange
            var claims = new Dictionary<string, string>
            {
                { "aud", TestAgentClientId }
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => CreateConversationOptionsBuilder.Create(claims, (ChannelId)null));
        }

        [Fact]
        public void Create_WithClaims_ShouldThrowOnMissingAudClaim()
        {
            // Arrange
            var claims = new Dictionary<string, string>
            {
                { "other", "value" }
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => CreateConversationOptionsBuilder.Create(claims, Channels.Msteams));
            Assert.Contains("aud", ex.Message);
        }

        [Fact]
        public void Create_WithClaims_ShouldThrowOnEmptyAudClaim()
        {
            // Arrange
            var claims = new Dictionary<string, string>
            {
                { "aud", string.Empty }
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => CreateConversationOptionsBuilder.Create(claims, Channels.Msteams));
        }

        [Fact]
        public void Create_WithClaims_ShouldInitializeWithDefaultParameters()
        {
            // Arrange
            var claims = new Dictionary<string, string>
            {
                { "aud", TestAgentClientId }
            };

            // Act
            var result = CreateConversationOptionsBuilder.Create(claims, Channels.Msteams)
                .WithUser(TestUserId, TestUserName)
                .Build();

            // Assert
            Assert.NotNull(result.Parameters);
            Assert.NotNull(result.Parameters.Agent);
            Assert.Equal(TestAgentClientId, result.Parameters.Agent.Id);
        }

        [Fact]
        public void Create_WithClaims_AndServiceUrl_ShouldUseProvidedServiceUrl()
        {
            // Arrange
            var claims = new Dictionary<string, string>
            {
                { "aud", TestAgentClientId }
            };

            // Act
            var result = CreateConversationOptionsBuilder.Create(claims, Channels.Msteams, TestServiceUrl)
                .WithUser(TestUserId, TestUserName)
                .Build();

            // Assert
            Assert.Equal(TestServiceUrl, result.ServiceUrl);
        }

        #endregion

        #region WithUser Tests

        [Fact]
        public void WithUser_WithUserIdAndName_ShouldSetUser()
        {
            // Act
            var result = CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams)
                .WithUser(TestUserId, TestUserName)
                .Build();

            // Assert
            Assert.NotNull(result.Parameters.Members);
            Assert.Equal(TestUserId, result.Parameters.Members[0].Id);
            Assert.Equal(TestUserName, result.Parameters.Members[0].Name);
            Assert.Single(result.Parameters.Members);
            Assert.Equal(TestUserId, result.Parameters.Members[0].Id);
        }

        [Fact]
        public void WithUser_WithUserIdOnly_ShouldSetUser()
        {
            // Act
            var result = CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams)
                .WithUser(TestUserId)
                .Build();

            // Assert
            Assert.NotNull(result.Parameters.Members[0]);
            Assert.Equal(TestUserId, result.Parameters.Members[0].Id);
            Assert.Null(result.Parameters.Members[0].Name);
        }

        [Fact]
        public void WithUser_ShouldThrowOnNullUserId()
        {
            // Arrange
            var builder = CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => builder.WithUser((string)null));
        }

        [Fact]
        public void WithUser_ShouldThrowOnEmptyUserId()
        {
            // Arrange
            var builder = CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => builder.WithUser(string.Empty));
        }

        [Fact]
        public void WithUser_WithChannelAccount_ShouldSetUser()
        {
            // Arrange
            var user = new ChannelAccount(TestUserId, TestUserName);

            // Act
            var result = CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams)
                .WithUser(user)
                .Build();

            // Assert
            Assert.Single(result.Parameters.Members);
            Assert.Equal(TestUserId, result.Parameters.Members[0].Id);
        }

        [Fact]
        public void WithUser_WithNullChannelAccount_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams).WithUser((ChannelAccount)null));
        }

        [Fact]
        public void WithUser_WithNullChannelAccountId_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams).WithUser(new ChannelAccount(null)));
        }

        [Fact]
        public void WithUser_WithWhitespaceChannelAccountId_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams).WithUser(new ChannelAccount("    ")));
        }

        #endregion

        #region WithScope Tests

        [Fact]
        public void WithScope_ShouldSetScope()
        {
            // Act
            var result = CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams)
                .WithUser(TestUserId, TestUserName)
                .WithScope(TestScope)
                .Build();

            // Assert
            Assert.Equal(TestScope, result.Scope);
        }

        #endregion

        #region WithActivity Tests

        [Fact]
        public void WithActivity_ShouldSetActivity()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "Test message"
            };

            // Act
            var result = CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams)
                .WithUser(TestUserId, TestUserName)
                .WithActivity(activity)
                .Build();

            // Assert
            Assert.NotNull(result.Parameters.Activity);
            Assert.Equal("Test message", result.Parameters.Activity.Text);
            Assert.Equal(ActivityTypes.Message, result.Parameters.Activity.Type);
        }

        [Fact]
        public void WithActivity_WithoutType_ShouldSetTypeToMessageInBuild()
        {
            // Arrange
            var activity = new Activity
            {
                Text = "Test message"
            };

            // Act
            var result = CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams)
                .WithUser(TestUserId, TestUserName)
                .WithActivity(activity)
                .Build();

            // Assert
            Assert.Equal(ActivityTypes.Message, result.Parameters.Activity.Type);
        }

        #endregion

        #region WithChannelData Tests

        [Fact]
        public void WithChannelData_ShouldSetChannelData()
        {
            // Arrange
            var channelData = new { customProperty = "customValue" };

            // Act
            var result = CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams)
                .WithUser(TestUserId, TestUserName)
                .WithChannelData(channelData)
                .Build();

            // Assert
            Assert.NotNull(result.Parameters.ChannelData);
            Assert.Equal(channelData, result.Parameters.ChannelData);
        }

        [Fact]
        public void WithChannelData_WithNull_ShouldSetNullChannelData()
        {
            // Act
            var result = CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams)
                .WithUser(TestUserId, TestUserName)
                .WithChannelData(null)
                .Build();

            // Assert
            Assert.Null(result.Parameters.ChannelData);
        }

        #endregion

        #region IsGroup Tests

        [Fact]
        public void IsGroup_ShouldSetIsGroupToTrue()
        {
            // Act
            var result = CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams)
                .WithUser(TestUserId, TestUserName)
                .IsGroup(true)
                .Build();

            // Assert
            Assert.True(result.Parameters.IsGroup);
        }

        [Fact]
        public void IsGroup_ShouldSetIsGroupToFalse()
        {
            // Act
            var result = CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams)
                .WithUser(TestUserId, TestUserName)
                .IsGroup(false)
                .Build();

            // Assert
            Assert.False(result.Parameters.IsGroup);
        }

        #endregion

        #region WithTopicName Tests

        [Fact]
        public void WithTopicName_ShouldSetTopicName()
        {
            // Act
            var result = CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams)
                .WithUser(TestUserId, TestUserName)
                .WithTopicName(TestTopicName)
                .Build();

            // Assert
            Assert.Equal(TestTopicName, result.Parameters.TopicName);
        }

        #endregion

        #region WithTenantId Tests

        [Fact]
        public void WithTenantId_ShouldSetTenantId()
        {
            // Act
            var result = CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams)
                .WithUser(TestUserId, TestUserName)
                .WithTenantId(TestTenantId)
                .Build();

            // Assert
            Assert.Equal(TestTenantId, result.Parameters.TenantId);
        }

        #endregion

        #region Teams Tests
        [Fact]
        public void Create_ForMsteams_ShouldMergeTeamsTenantAndChannelId()
        {
            // Act
            var result = CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams)
                .WithTenantId("teams-tenant-id")
                .WithTeamsChannelId("teams-channel-id")
                .WithUser(TestUserId, TestUserName)
                .Build();

            var expectedJson = "{\"tenant\":{\"id\":\"teams-tenant-id\"},\"channel\":{\"id\":\"teams-channel-id\"}}";

            // Assert
            Assert.NotNull(result.Parameters.ChannelData);
            Assert.Equal(expectedJson, System.Text.Json.JsonSerializer.Serialize(result.Parameters.ChannelData));
        }

        [Fact]
        public void Create_ForMsteams_ShouldMergeWithChannelData()
        {
            // Act
            var result = CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams)
                .WithTeamsChannelId("teams-channel-id")
                .WithChannelData(new { tenant = new { id = "teams-tenant-id" } })
                .WithUser(TestUserId, TestUserName)
                .Build();

            var expectedJson = "{\"channel\":{\"id\":\"teams-channel-id\"},\"tenant\":{\"id\":\"teams-tenant-id\"}}";

            // Assert
            Assert.NotNull(result.Parameters.ChannelData);
            Assert.Equal(expectedJson, System.Text.Json.JsonSerializer.Serialize(result.Parameters.ChannelData));
        }

        #endregion

        #region Build Tests

        [Fact]
        public void Build_ShouldThrowWhenMembersNotSet()
        {
            // Arrange
            var builder = CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => builder.Build());
        }

        [Fact]
        public void Build_ShouldSetDefaultScope_WhenNotSpecified()
        {
            // Act
            var result = CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams)
                .WithUser(TestUserId, TestUserName)
                .Build();

            // Assert
            Assert.Equal(CreateConversationOptions.AzureBotScope, result.Scope);
        }

        [Fact]
        public void Build_ShouldNotOverrideScope_WhenSpecified()
        {
            // Act
            var result = CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams)
                .WithUser(TestUserId, TestUserName)
                .WithScope(TestScope)
                .Build();

            // Assert
            Assert.Equal(TestScope, result.Scope);
        }

        [Fact]
        public void Build_ShouldReturnCompleteCreateConversation()
        {
            // Act
            var result = CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams)
                .WithUser(TestUserId, TestUserName)
                .Build();

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.ChannelId);
            Assert.NotNull(result.ServiceUrl);
            Assert.Equal(ConversationReferenceBuilder.ServiceUrlForChannel(Channels.Msteams), result.ServiceUrl);
            Assert.NotNull(result.Parameters);
            Assert.NotNull(result.Scope);
        }

        #endregion

        #region Fluent Interface Tests

        [Fact]
        public void FluentInterface_ShouldAllowMethodChaining()
        {
            // Arrange
            var activity = new Activity { Text = "Test" };
            var channelData = new { data = "value" };

            // Act
            var result = CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Webchat)
                .WithUser(TestUserId, TestUserName)
                .WithScope(TestScope)
                .WithActivity(activity)
                .WithChannelData(channelData)
                .IsGroup(true)
                .WithTopicName(TestTopicName)
                .WithTenantId(TestTenantId)
                .Build();

            // Assert
            Assert.Single(result.Parameters.Members);
            Assert.Equal(TestUserId, result.Parameters.Members[0].Id);
            Assert.Equal(TestScope, result.Scope);
            Assert.Equal("Test", result.Parameters.Activity.Text);
            Assert.Equal(channelData, result.Parameters.ChannelData);
            Assert.True(result.Parameters.IsGroup);
            Assert.Equal(TestTopicName, result.Parameters.TopicName);
            Assert.Equal(TestTenantId, result.Parameters.TenantId);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void Integration_ShouldCreateCompleteConversationForMsteams()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "Hello Teams!"
            };
            var teamsChannelData = new { teamsChannelId = "19:123@thread.skype" };

            // Act
            var result = CreateConversationOptionsBuilder.Create(TestAgentClientId, Channels.Msteams, TestServiceUrl)
                .WithUser(TestUserId, TestUserName)
                .WithActivity(activity)
                .WithChannelData(teamsChannelData)
                .IsGroup(true)
                .WithTopicName("Teams Discussion")
                .WithTenantId(TestTenantId)
                .Build();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(Channels.Msteams, result.ChannelId);
            Assert.Equal(TestServiceUrl, result.ServiceUrl);
            Assert.Single(result.Parameters.Members);
            Assert.Equal(TestUserId, result.Parameters.Members[0].Id);
            Assert.Equal("Hello Teams!", result.Parameters.Activity.Text);
            Assert.True(result.Parameters.IsGroup);
            Assert.Equal("Teams Discussion", result.Parameters.TopicName);
            Assert.Equal(TestTenantId, result.Parameters.TenantId);
        }

        [Fact]
        public void Integration_ShouldCreateCompleteConversationWithClaims()
        {
            // Arrange
            var claims = new Dictionary<string, string>
            {
                { "aud", TestAgentClientId },
                { "appid", "requestor-id" }
            };

            // Act
            var result = CreateConversationOptionsBuilder.Create(claims, Channels.Directline)
                .WithUser(TestUserId, TestUserName)
                .WithTopicName("DirectLine Chat")
                .Build();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(Channels.Directline, result.ChannelId);
            Assert.Single(result.Parameters.Members);
            Assert.Equal(TestUserId, result.Parameters.Members[0].Id);
            Assert.Equal("DirectLine Chat", result.Parameters.TopicName);
        }

        #endregion
    }
}