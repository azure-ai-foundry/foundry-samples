// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class ConversationBuilderTests
    {
        private const string TestAgentClientId = "test-agent-id";
        private const string TestChannelId = "test-channel";
        private const string TestServiceUrl = "https://test.service.url/";
        private const string TestRequestorId = "test-requestor-id";
        private const string TestUserId = "test-user-id";
        private const string TestUserName = "Test User";
        private const string TestConversationId = "test-conversation-id";

        #region Create Tests

        [Fact]
        public void Create_WithAgentClientIdAndChannelId_ReturnsBuilder()
        {
            // Act
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId);

            // Assert
            Assert.NotNull(builder);
            Assert.IsType<ConversationBuilder>(builder);
        }

        [Fact]
        public void Create_WithServiceUrl_SetsServiceUrl()
        {
            // Act
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId, TestServiceUrl);
            var conversation = builder.Build();

            // Assert
            Assert.Equal(TestServiceUrl, conversation.Reference.ServiceUrl);
        }

        [Fact]
        public void Create_WithoutServiceUrl_UsesDefaultServiceUrl()
        {
            // Act
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId);
            var conversation = builder.Build();

            // Assert
            Assert.NotNull(conversation.Reference.ServiceUrl);
            Assert.Equal($"https://{TestChannelId}.botframework.com/", conversation.Reference.ServiceUrl);
        }

        [Fact]
        public void Create_WithRequestorId_SetsAppIdClaim()
        {
            // Act
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId, null, TestRequestorId);
            var conversation = builder.Build();

            // Assert
            var identity = conversation.Identity;
            var appIdClaim = identity.Claims.FirstOrDefault(c => c.Type == "appid");
            Assert.NotNull(appIdClaim);
            Assert.Equal(TestRequestorId, appIdClaim.Value);
        }

        [Fact]
        public void Create_WithoutRequestorId_DoesNotSetAppIdClaim()
        {
            // Act
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId);
            var conversation = builder.Build();

            // Assert
            var identity = conversation.Identity;
            var appIdClaim = identity.Claims.FirstOrDefault(c => c.Type == "appid");
            Assert.Null(appIdClaim);
        }

        [Fact]
        public void Create_SetsAudienceClaim()
        {
            // Act
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId);
            var conversation = builder.Build();

            // Assert
            var identity = conversation.Identity;
            var audClaim = identity.Claims.FirstOrDefault(c => c.Type == "aud");
            Assert.NotNull(audClaim);
            Assert.Equal(TestAgentClientId, audClaim.Value);
        }

        [Fact]
        public void Create_WithNullAgentClientId_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => ConversationBuilder.Create((string)null, TestChannelId));
        }

        [Fact]
        public void Create_WithEmptyAgentClientId_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => ConversationBuilder.Create(string.Empty, TestChannelId));
        }

        [Fact]
        public void Create_WithWhitespaceAgentClientId_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => ConversationBuilder.Create("    ", TestChannelId));
        }

        [Fact]
        public void Create_WithNullChannelId_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => ConversationBuilder.Create(TestAgentClientId, null));
        }

        [Fact]
        public void Create_WithEmptyChannelId_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => ConversationBuilder.Create(TestAgentClientId, string.Empty));
        }

        [Fact]
        public void Create_WithClaimsIdentity_ReturnsBuilder()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("aud", TestAgentClientId),
                new Claim("appid", TestRequestorId)
            };
            var identity = new ClaimsIdentity(claims);

            // Act
            var builder = ConversationBuilder.Create(identity, TestChannelId);

            // Assert
            Assert.NotNull(builder);
            Assert.IsType<ConversationBuilder>(builder);
        }

        [Fact]
        public void Create_WithClaimsIdentity_SetsIdentity()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("aud", TestAgentClientId),
                new Claim("appid", TestRequestorId),
                new Claim("ver", "1.0"),
                new Claim("other", "value")
            };
            var identity = new ClaimsIdentity(claims);

            // Act
            var builder = ConversationBuilder.Create(identity, TestChannelId);
            var conversation = builder.Build();

            // Assert
            var resultIdentity = conversation.Identity;
            Assert.NotNull(resultIdentity.Claims.FirstOrDefault(c => c.Type == "aud"));
            Assert.NotNull(resultIdentity.Claims.FirstOrDefault(c => c.Type == "appid"));
            Assert.NotNull(resultIdentity.Claims.FirstOrDefault(c => c.Type == "ver"));
            Assert.Null(resultIdentity.Claims.FirstOrDefault(c => c.Type == "other"));
        }

        [Fact]
        public void Create_WithClaimsIdentityAndServiceUrl_SetsServiceUrl()
        {
            // Arrange
            var claims = new List<Claim> { new Claim("aud", TestAgentClientId) };
            var identity = new ClaimsIdentity(claims);

            // Act
            var builder = ConversationBuilder.Create(identity, TestChannelId, TestServiceUrl);
            var conversation = builder.Build();

            // Assert
            Assert.Equal(TestServiceUrl, conversation.Reference.ServiceUrl);
        }

        #endregion

        #region WithUser Tests

        [Fact]
        public void WithUser_WithUserIdAndName_SetsUser()
        {
            // Act
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId)
                .WithUser(TestUserId, TestUserName);
            var conversation = builder.Build();

            // Assert
            Assert.NotNull(conversation.Reference.User);
            Assert.Equal(TestUserId, conversation.Reference.User.Id);
            Assert.Equal(TestUserName, conversation.Reference.User.Name);
            Assert.Equal(RoleTypes.User, conversation.Reference.User.Role);
        }

        [Fact]
        public void WithUser_WithUserIdOnly_SetsUserWithoutName()
        {
            // Act
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId)
                .WithUser(TestUserId);
            var conversation = builder.Build();

            // Assert
            Assert.NotNull(conversation.Reference.User);
            Assert.Equal(TestUserId, conversation.Reference.User.Id);
            Assert.Equal(RoleTypes.User, conversation.Reference.User.Role);
            Assert.Null(conversation.Reference.User.Name);
        }

        [Fact]
        public void WithUser_WithNullUserId_ThrowsArgumentException()
        {
            // Arrange
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.WithUser(null, TestUserName));
        }

        [Fact]
        public void WithUser_WithEmptyUserId_ThrowsArgumentException()
        {
            // Arrange
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => builder.WithUser(string.Empty, TestUserName));
        }

        [Fact]
        public void WithUser_WithWhitespaceUserId_ThrowsArgumentException()
        {
            // Arrange
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => builder.WithUser("   ", TestUserName));
        }

        [Fact]
        public void WithUser_WithChannelAccount_SetsUser()
        {
            // Arrange
            var user = new ChannelAccount(TestUserId, TestUserName, RoleTypes.User);

            // Act
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId)
                .WithUser(user);
            var conversation = builder.Build();

            // Assert
            Assert.NotNull(conversation.Reference.User);
            Assert.Equal(TestUserId, conversation.Reference.User.Id);
            Assert.Equal(TestUserName, conversation.Reference.User.Name);
            Assert.Equal(RoleTypes.User, conversation.Reference.User.Role);
        }

        [Fact]
        public void WithUser_WithNullChannelAccount_ThrowsArgumentNullException()
        {
            // Arrange
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.WithUser((ChannelAccount)null));
        }

        [Fact]
        public void WithUser_MergeReferenceWithUser()
        {
            // Note: This tests the internal behavior when _conversation.Reference is null
            // We can't directly test this since Create() initializes the reference,
            // but this validates the merge logic works correctly
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId)
                .WithUser(TestUserId, TestUserName);
            var conversation = builder.Build();

            // Assert
            Assert.NotNull(conversation.Reference);
            Assert.Equal(TestChannelId, conversation.Reference.ChannelId);
            Assert.NotNull(conversation.Reference.Agent);
            Assert.Equal(TestAgentClientId, conversation.Reference.Agent.Id);
            Assert.NotNull(conversation.Reference.User);
            Assert.Equal(TestUserId, conversation.Reference.User.Id);
            Assert.NotEmpty(conversation.Reference.ServiceUrl);
        }

        [Fact]
        public void WithUser_ReturnsBuilderForChaining()
        {
            // Act
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId);
            var result = builder.WithUser(TestUserId);

            // Assert
            Assert.Same(builder, result);
        }

        #endregion

        #region WithConversation Tests

        [Fact]
        public void WithConversation_WithConversationId_SetsConversation()
        {
            // Act
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId)
                .WithConversation(TestConversationId);
            var conversation = builder.Build();

            // Assert
            Assert.NotNull(conversation.Reference.Conversation);
            Assert.Equal(TestConversationId, conversation.Reference.Conversation.Id);
        }

        [Fact]
        public void WithConversation_WithNullConversationId_ThrowsArgumentException()
        {
            // Arrange
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.WithConversation((string)null));
        }

        [Fact]
        public void WithConversation_WithEmptyConversationId_ThrowsArgumentException()
        {
            // Arrange
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => builder.WithConversation(string.Empty));
        }

        [Fact]
        public void WithConversation_WithWhitespaceConversationId_ThrowsArgumentException()
        {
            // Arrange
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => builder.WithConversation("   "));
        }

        [Fact]
        public void WithConversation_WithConversationAccount_SetsConversation()
        {
            // Arrange
            var conversationAccount = new ConversationAccount(id: TestConversationId, name: "Test Conversation");

            // Act
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId)
                .WithConversation(conversationAccount);
            var conversation = builder.Build();

            // Assert
            Assert.NotNull(conversation.Reference.Conversation);
            Assert.Equal(TestConversationId, conversation.Reference.Conversation.Id);
            Assert.Equal("Test Conversation", conversation.Reference.Conversation.Name);
        }

        [Fact]
        public void WithConversation_WithNullConversationAccount_ThrowsArgumentNullException()
        {
            // Arrange
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.WithConversation((ConversationAccount)null));
        }

        [Fact]
        public void WithConversation_ReturnsBuilderForChaining()
        {
            // Act
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId);
            var result = builder.WithConversation(TestConversationId);

            // Assert
            Assert.Same(builder, result);
        }

        #endregion

        #region Build Tests

        [Fact]
        public void Build_WithMinimalConfiguration_ReturnsValidatesWithoutConversion()
        {
            // Act
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId).WithUser(TestUserId);
            var conversation = builder.Build();

            // Assert
            Assert.NotNull(conversation);
            Assert.NotNull(conversation.Reference);
            Assert.NotNull(conversation.Identity);

            conversation.Validate(validateConversation: false);
        }

        [Fact]
        public void Build_WithMinimalConfiguration_ReturnsValidatesWithConversation()
        {
            // Act
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId)
                .WithUser(TestUserId)
                .WithConversation("conv-id");
            var conversation = builder.Build();

            // Assert
            Assert.NotNull(conversation);
            Assert.NotNull(conversation.Reference);
            Assert.NotNull(conversation.Identity);

            conversation.Validate(validateConversation: true);
        }

        [Fact]
        public void Build_WithMinimalConfiguration_FailsValidationWithWhitespaceConversationId()
        {
            // Act
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId);
            Assert.Throws<ArgumentException>(() => builder.WithConversation(" "));
        }

        [Fact]
        public void Build_WithMinimalConfiguration_FailsValidationWithNullConversationId()
        {
            // Act
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId);
            Assert.Throws<ArgumentNullException>(() => builder.WithConversation(new ConversationAccount()));
        }

        [Fact]
        public void Build_WithFullConfiguration_ReturnsValidConversation()
        {
            // Act
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId, TestServiceUrl, TestRequestorId)
                .WithUser(TestUserId, TestUserName)
                .WithConversation(TestConversationId);
            var conversation = builder.Build();

            // Assert
            Assert.NotNull(conversation);
            Assert.Equal(TestChannelId, conversation.Reference.ChannelId);
            Assert.Equal(TestServiceUrl, conversation.Reference.ServiceUrl);
            Assert.Equal(TestUserId, conversation.Reference.User.Id);
            Assert.Equal(TestUserName, conversation.Reference.User.Name);
            Assert.Equal(TestConversationId, conversation.Reference.Conversation.Id);
        }

        [Fact]
        public void Build_SetsDefaultServiceUrlWhenNotProvided()
        {
            // Act
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId);
            var conversation = builder.Build();

            // Assert
            Assert.NotNull(conversation.Reference.ServiceUrl);
            Assert.StartsWith("https://", conversation.Reference.ServiceUrl);
        }

        [Fact]
        public void Build_WithTeamsChannel_SetsTeamsServiceUrl()
        {
            // Act
            var builder = ConversationBuilder.Create(TestAgentClientId, Channels.Msteams);
            var conversation = builder.Build();

            // Assert
            Assert.Equal("https://smba.trafficmanager.net/teams/", conversation.Reference.ServiceUrl);
        }

        [Fact]
        public void Build_MultipleTimes_ReturnsConsistentResults()
        {
            // Arrange
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId)
                .WithUser(TestUserId, TestUserName);

            // Act
            var conversation1 = builder.Build();
            var conversation2 = builder.Build();

            // Assert
            Assert.Equal(conversation1.Reference.User.Id, conversation2.Reference.User.Id);
            Assert.Equal(conversation1.Reference.ChannelId, conversation2.Reference.ChannelId);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void BuilderChaining_WorksCorrectly()
        {
            // Act
            var conversation = ConversationBuilder.Create(TestAgentClientId, TestChannelId, TestServiceUrl, TestRequestorId)
                .WithUser(TestUserId, TestUserName)
                .WithConversation(TestConversationId)
                .Build();

            // Assert
            Assert.NotNull(conversation);
            Assert.Equal(TestUserId, conversation.Reference.User.Id);
            Assert.Equal(TestConversationId, conversation.Reference.Conversation.Id);
            Assert.Equal(TestServiceUrl, conversation.Reference.ServiceUrl);
        }

        [Fact]
        public void BuilderWithClaimsIdentity_PreservesClaims()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("aud", TestAgentClientId),
                new Claim("appid", TestRequestorId),
                new Claim("ver", "2.0"),
                new Claim("iss", "test-issuer"),
                new Claim("tid", "test-tenant-id")
            };
            var identity = new ClaimsIdentity(claims);

            // Act
            var conversation = ConversationBuilder.Create(identity, TestChannelId)
                .WithUser(TestUserId)
                .WithConversation(TestConversationId)
                .Build();

            // Assert
            var resultIdentity = conversation.Identity;
            Assert.Equal(5, resultIdentity.Claims.Count());
            Assert.NotNull(resultIdentity.Claims.FirstOrDefault(c => c.Type == "aud" && c.Value == TestAgentClientId));
            Assert.NotNull(resultIdentity.Claims.FirstOrDefault(c => c.Type == "appid" && c.Value == TestRequestorId));
            Assert.NotNull(resultIdentity.Claims.FirstOrDefault(c => c.Type == "ver" && c.Value == "2.0"));
            Assert.NotNull(resultIdentity.Claims.FirstOrDefault(c => c.Type == "iss" && c.Value == "test-issuer"));
            Assert.NotNull(resultIdentity.Claims.FirstOrDefault(c => c.Type == "tid" && c.Value == "test-tenant-id"));
        }

        [Fact]
        public void Builder_OverwritesUserWithMultipleCalls()
        {
            // Act
            var conversation = ConversationBuilder.Create(TestAgentClientId, TestChannelId)
                .WithUser("first-user-id", "First User")
                .WithUser(TestUserId, TestUserName)
                .Build();

            // Assert
            Assert.Equal(TestUserId, conversation.Reference.User.Id);
            Assert.Equal(TestUserName, conversation.Reference.User.Name);
        }

        [Fact]
        public void Builder_OverwritesConversationWithMultipleCalls()
        {
            // Act
            var conversation = ConversationBuilder.Create(TestAgentClientId, TestChannelId)
                .WithConversation("first-conversation-id")
                .WithConversation(TestConversationId)
                .Build();

            // Assert
            Assert.Equal(TestConversationId, conversation.Reference.Conversation.Id);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void Builder_WithEmptyServiceUrl_UsesDefault()
        {
            // Act
            var builder = ConversationBuilder.Create(TestAgentClientId, TestChannelId, string.Empty);
            var conversation = builder.Build();

            // Assert
            Assert.NotNull(conversation.Reference.ServiceUrl);
            Assert.NotEmpty(conversation.Reference.ServiceUrl);
        }

        [Fact]
        public void Builder_WithDifferentChannels_GeneratesCorrectServiceUrls()
        {
            // Test different channels
            var testCases = new[]
            {
                (Channels.Msteams, "https://smba.trafficmanager.net/teams/"),
                (Channels.Emulator, "https://emulator.botframework.com/"),
                (Channels.Directline, "https://directline.botframework.com/"),
                (Channels.Webchat, "https://webchat.botframework.com/")
            };

            foreach (var (channelId, expectedUrl) in testCases)
            {
                // Act
                var conversation = ConversationBuilder.Create(TestAgentClientId, channelId).Build();

                // Assert
                Assert.Equal(expectedUrl, conversation.Reference.ServiceUrl);
            }
        }

        #endregion
    }
}