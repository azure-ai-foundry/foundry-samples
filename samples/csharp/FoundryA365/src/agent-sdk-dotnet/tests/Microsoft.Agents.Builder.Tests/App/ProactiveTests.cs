// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class ProactiveTests
    {
        private readonly Mock<IChannelAdapter> _mockAdapter;
        private readonly MemoryStorage _storage;
        private readonly AgentApplication _app;
        private readonly Proactive _proactive;
        private readonly ConversationReference _conversationRef;
        private readonly ITurnContext _turnContext;
        private readonly IDictionary<string, string> _claims;

        public ProactiveTests()
        {
            _mockAdapter = new Mock<IChannelAdapter>();
            _storage = new MemoryStorage();
            var options = new AgentApplicationOptions(_storage)
            {
                Proactive = new ProactiveOptions(_storage)
            };
            _app = new AgentApplication(options);
            _proactive = new Proactive(_app);
            _conversationRef = new ConversationReference
            {
                Conversation = new ConversationAccount { Id = "test-conversation-id" },
                ServiceUrl = "https://test.com",
                User = new ChannelAccount("user1", "User1"),
                Agent = new ChannelAccount("bot", "Bot"),
                ChannelId = "test-channel"
            };
            _turnContext = new TurnContext(_mockAdapter.Object, new Activity
            {
                Conversation = _conversationRef.Conversation,
                ServiceUrl = _conversationRef.ServiceUrl,
                ChannelId = _conversationRef.ChannelId
            }, AgentClaims.CreateIdentity("bot"));
            _claims = new Dictionary<string, string> { { "aud", _conversationRef.Agent.Id } };
        }

        #region StoreConversationAsync Tests

        [Fact]
        public async Task StoreConversationAsync_WithTurnContext_ShouldStoreConversation()
        {
            // Act
            var conversationId = await _proactive.StoreConversationAsync(_turnContext);

            // Assert
            Assert.NotNull(conversationId);
            Assert.Equal(_conversationRef.Conversation.Id, conversationId);

            var retrieved = await _proactive.GetConversationAsync(conversationId);
            Assert.NotNull(retrieved);
            Assert.Equal(_conversationRef.Conversation.Id, retrieved.Reference.Conversation.Id);
            Assert.NotNull(retrieved.Identity);
            Assert.Equal("bot", retrieved.Identity.Claims.FirstOrDefault(c => c.Type == "aud").Value);
        }

        [Fact]
        public async Task StoreConversationAsync_WithTurnContext_NullContext_ShouldThrow()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _proactive.StoreConversationAsync((ITurnContext)null));
        }

        [Fact]
        public async Task StoreConversationAsync_WithConversationReference_ShouldStoreConversation()
        {
            // Arrange
            var conversation = new Conversation(new Dictionary<string, string> { { "aud", _conversationRef.Agent.Id } }, _conversationRef);

            // Act
            var conversationId = await _proactive.StoreConversationAsync(conversation);

            // Assert
            Assert.NotNull(conversationId);
            Assert.Equal(_conversationRef.Conversation.Id, conversationId);

            var retrieved = await _proactive.GetConversationAsync(conversationId);
            Assert.NotNull(retrieved);
            Assert.Equal(_conversationRef.Conversation.Id, retrieved.Reference.Conversation.Id);
            Assert.Equal(_conversationRef.Agent.Id, retrieved.Identity.Claims.FirstOrDefault(c => c.Type == "aud").Value);
        }

        [Fact]
        public async Task StoreConversationAsync_WithConversation_NullConversation_ShouldThrow()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _proactive.StoreConversationAsync((Conversation)null));
        }

        [Fact]
        public async Task StoreConversationAsync_ShouldOverwriteExisting()
        {
            // Arrange
            var conversation1 = new Conversation(_claims, _conversationRef);
            var conversation2 = new Conversation(new Dictionary<string, string> { { "aud", _conversationRef.Agent.Id }, { "iss", "issuer" } }, _conversationRef);

            // Act
            await _proactive.StoreConversationAsync(conversation1);
            await _proactive.StoreConversationAsync(conversation2);

            // Assert
            var retrieved = await _proactive.GetConversationAsync(_conversationRef.Conversation.Id);
            Assert.NotNull(retrieved);
            Assert.Equal("issuer", retrieved.Identity.Claims.FirstOrDefault(c => c.Type == "iss").Value);
        }

        #endregion

        #region GetConversationAsync Tests

        [Fact]
        public async Task GetConversationAsync_WithInvalidId_ShouldReturnNull()
        {
            // Act
            var result = await _proactive.GetConversationAsync("non-existent-id");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetConversationAsync_NullConversationId_ShouldThrow()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _proactive.GetConversationAsync(null));
        }

        [Fact]
        public async Task GetConversationAsync_EmptyConversationId_ShouldThrow()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _proactive.GetConversationAsync(string.Empty));
        }

        [Fact]
        public async Task GetConversationAsync_WhitespaceConversationId_ShouldThrow()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _proactive.GetConversationAsync("    "));
        }

        #endregion

        #region DeleteConversationAsync Tests

        [Fact]
        public async Task DeleteConversationAsync_ShouldRemoveConversation()
        {
            // Arrange
            var conversation = new Conversation(_claims, _conversationRef);
            await _proactive.StoreConversationAsync(conversation);

            // Act
            await _proactive.DeleteConversationAsync(_conversationRef.Conversation.Id);

            // Assert
            var result = await _proactive.GetConversationAsync(_conversationRef.Conversation.Id);
            Assert.Null(result);
        }

        [Fact]
        public async Task DeleteConversationAsync_NonExistentId_ShouldNotThrow()
        {
            // Act & Assert
            await _proactive.DeleteConversationAsync("non-existent-id");
        }

        [Fact]
        public async Task DeleteConversationAsync_NullConversationId_ShouldThrow()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _proactive.DeleteConversationAsync(null));
        }

        [Fact]
        public async Task DeleteConversationAsync_EmptyConversationId_ShouldThrow()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _proactive.DeleteConversationAsync(string.Empty));
        }

        #endregion

        #region SendActivityAsync Tests

        [Fact]
        public async Task SendActivityAsync_WithStoredConversation_ShouldSendActivity()
        {
            // Arrange
            var conversation = new Conversation(_claims, _conversationRef);
            await _proactive.StoreConversationAsync(conversation);

            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "Test message"
            };

            _mockAdapter
                .Setup(a => a.ContinueConversationAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<ConversationReference>(),
                    It.IsAny<AgentCallbackHandler>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback<ClaimsIdentity, ConversationReference, AgentCallbackHandler, CancellationToken>(
                    async (identity, reference, callback, ct) =>
                    {
                        var turnContext = new TurnContext(_mockAdapter.Object, activity);
                        await callback(turnContext, ct);
                    });

            // Act
            var result = await _proactive.SendActivityAsync(
                _mockAdapter.Object,
                _conversationRef.Conversation.Id,
                activity);

            // Assert
            Assert.NotNull(result);
            _mockAdapter.Verify(a => a.ContinueConversationAsync(
                It.IsAny<ClaimsIdentity>(),
                It.IsAny<ConversationReference>(),
                It.IsAny<AgentCallbackHandler>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SendActivityAsync_WithNonExistentConversation_ShouldThrow()
        {
            // Arrange
            var activity = new Activity { Text = "Test" };

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _proactive.SendActivityAsync(_mockAdapter.Object, "non-existent-id", activity));
        }

        [Fact]
        public async Task SendActivityAsync_NullConversationId_ShouldThrow()
        {
            // Arrange
            var activity = new Activity { Text = "Test" };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _proactive.SendActivityAsync(_mockAdapter.Object, null, activity));
        }

        [Fact]
        public async Task SendActivityAsync_NullActivity_ShouldThrow()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _proactive.SendActivityAsync(_mockAdapter.Object, "test-id", null));
        }

        [Fact]
        public async Task SendActivityAsync_ActivityWithoutType_ShouldDefaultToMessage()
        {
            // Arrange
            var conversation = new Conversation(_claims, _conversationRef);
            await _proactive.StoreConversationAsync(conversation);

            var activity = new Activity { Text = "Test" };
            Assert.Null(activity.Type);

            _mockAdapter
                .Setup(a => a.ContinueConversationAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<ConversationReference>(),
                    It.IsAny<AgentCallbackHandler>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _proactive.SendActivityAsync(_mockAdapter.Object, _conversationRef.Conversation.Id, activity);

            // Assert
            Assert.Equal(ActivityTypes.Message, activity.Type);
        }

        [Fact]
        public async Task SendActivityAsync_StaticMethod_ShouldSendActivity()
        {
            // Arrange
            var conversation = new Conversation(_claims, _conversationRef);
            var activity = new Activity { Text = "Test" };

            _mockAdapter
                .Setup(a => a.ContinueConversationAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<ConversationReference>(),
                    It.IsAny<AgentCallbackHandler>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback<ClaimsIdentity, ConversationReference, AgentCallbackHandler, CancellationToken>(
                    async (identity, reference, callback, ct) =>
                    {
                        var turnContext = new TurnContext(_mockAdapter.Object, activity, conversation.Identity);
                        await callback(turnContext, ct);
                    });

            _mockAdapter
                .Setup(a => a.SendActivitiesAsync(
                    It.IsAny<ITurnContext>(),
                    It.IsAny<IActivity[]>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new ResourceResponse[] { new ResourceResponse("sentId") }));

            // Act
            var result = await Proactive.SendActivityAsync(_mockAdapter.Object, conversation, activity);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("sentId", result.Id);
            _mockAdapter.Verify(a => a.ContinueConversationAsync(
                It.IsAny<ClaimsIdentity>(),
                It.IsAny<ConversationReference>(),
                It.IsAny<AgentCallbackHandler>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SendActivityAsync_StaticMethod_NullAdapter_ShouldThrow()
        {
            // Arrange
            var conversation = new Conversation(_claims, _conversationRef);
            var activity = new Activity { Text = "Test" };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                Proactive.SendActivityAsync(null, conversation, activity));
        }

        [Fact]
        public async Task SendActivityAsync_StaticMethod_NullConversation_ShouldThrow()
        {
            // Arrange
            var activity = new Activity { Text = "Test" };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                Proactive.SendActivityAsync(_mockAdapter.Object, null, activity));
        }

        [Fact]
        public async Task SendActivityAsync_StaticMethod_NullActivity_ShouldThrow()
        {
            // Arrange
            var conversation = new Conversation(_claims, _conversationRef);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                Proactive.SendActivityAsync(_mockAdapter.Object, conversation, null));
        }

        #endregion

        #region ContinueConversationAsync Tests

        [Fact]
        public async Task ContinueConversationAsync_WithStoredConversation_ShouldContinue()
        {
            // Arrange
            var conversation = new Conversation(_claims, _conversationRef);
            await _proactive.StoreConversationAsync(conversation);

            var handlerCalled = false;
            RouteHandler handler = (ITurnContext tc, ITurnState ts, CancellationToken ct) =>
            {
                handlerCalled = true;
                return Task.CompletedTask;
            };

            _mockAdapter
                .Setup(a => a.ProcessProactiveAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<IActivity>(),
                    It.IsAny<string>(),
                    It.IsAny<AgentCallbackHandler>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback<ClaimsIdentity, IActivity, string, AgentCallbackHandler, CancellationToken>(
                    async (identity, activity, audience, callback, ct) =>
                    {
                        var turnContext = new TurnContext(_mockAdapter.Object, activity);
                        await callback(turnContext, ct);
                    });

            // Act
            await _proactive.ContinueConversationAsync(
                _mockAdapter.Object,
                _conversationRef.Conversation.Id,
                handler);

            // Assert
            Assert.True(handlerCalled);
        }

        [Fact]
        public async Task ContinueConversationAsync_WithNonExistentConversation_ShouldThrow()
        {
            // Arrange
            RouteHandler handler = (ITurnContext tc, ITurnState ts, CancellationToken ct) => Task.CompletedTask;

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _proactive.ContinueConversationAsync(_mockAdapter.Object, "non-existent-id", handler));
        }

        [Fact]
        public async Task ContinueConversationAsync_NullConversationId_ShouldThrow()
        {
            // Arrange
            RouteHandler handler = (ITurnContext tc, ITurnState ts, CancellationToken ct) => Task.CompletedTask;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _proactive.ContinueConversationAsync(_mockAdapter.Object, (string)null, handler));
        }

        [Fact]
        public async Task ContinueConversationAsync_WithConversationObject_ShouldContinue()
        {
            // Arrange
            var conversation = new Conversation(_claims, _conversationRef);
            var handlerCalled = false;
            RouteHandler handler = (ITurnContext tc, ITurnState ts, CancellationToken ct) =>
            {
                handlerCalled = true;
                return Task.CompletedTask;
            };

            _mockAdapter
                .Setup(a => a.ProcessProactiveAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<IActivity>(),
                    It.IsAny<string>(),
                    It.IsAny<AgentCallbackHandler>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback<ClaimsIdentity, IActivity, string, AgentCallbackHandler, CancellationToken>(
                    async (identity, activity, audience, callback, ct) => await callback(new TurnContext(_mockAdapter.Object, activity, identity), ct));

            // Act
            await _proactive.ContinueConversationAsync(_mockAdapter.Object, conversation, handler);

            // Assert
            Assert.True(handlerCalled);
        }

        [Fact]
        public async Task ContinueConversationAsync_NullAdapter_ShouldThrow()
        {
            // Arrange
            var conversation = new Conversation(_claims, _conversationRef);
            RouteHandler handler = (ITurnContext tc, ITurnState ts, CancellationToken ct) => Task.CompletedTask;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _proactive.ContinueConversationAsync(null, conversation, handler));
        }

        [Fact]
        public async Task ContinueConversationAsync_NullConversation_ShouldThrow()
        {
            // Arrange
            RouteHandler handler = (ITurnContext tc, ITurnState ts, CancellationToken ct) => Task.CompletedTask;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _proactive.ContinueConversationAsync(_mockAdapter.Object, (Conversation)null, handler));
        }

        [Fact]
        public async Task ContinueConversationAsync_NullHandler_ShouldThrow()
        {
            // Arrange
            var conversation = new Conversation(_claims, _conversationRef);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _proactive.ContinueConversationAsync(_mockAdapter.Object, conversation, null));
        }

        [Fact]
        public async Task ContinueConversationAsync_WithCustomActivity_ShouldUseCustomActivity()
        {
            // Arrange
            var conversation = new Conversation(_claims, _conversationRef);
            var customActivity = new Activity
            {
                Type = ActivityTypes.Event,
                Name = "CustomEvent"
            };

            IActivity capturedActivity = null;
            _mockAdapter
                .Setup(a => a.ProcessProactiveAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<IActivity>(),
                    It.IsAny<string>(),
                    It.IsAny<AgentCallbackHandler>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback<ClaimsIdentity, IActivity, string, AgentCallbackHandler, CancellationToken>(
                    (identity, activity, audience, callback, ct) => capturedActivity = activity);

            RouteHandler handler = (ITurnContext tc, ITurnState ts, CancellationToken ct) => Task.CompletedTask;

            // Act
            await _proactive.ContinueConversationAsync(_mockAdapter.Object, conversation, handler, continuationActivity: customActivity);

            // Assert
            Assert.NotNull(capturedActivity);
            Assert.Equal(ActivityTypes.Event, capturedActivity.Type);
            Assert.Equal("CustomEvent", capturedActivity.Name);
        }

        #endregion

        #region CreateConversationAsync Tests

        [Fact]
        public async Task CreateConversationAsync_ShouldCreateNewConversation()
        {
            // Arrange
            var createInfo = CreateConversationOptionsBuilder.Create("bot", "channel", "serviceUrl").WithUser("user1", "User 1")
                .Build();
            
            var newReference = new ConversationReference
            {
                Conversation = new ConversationAccount { Id = "new-conversation-id" },
                ServiceUrl = "https://test.com",
                ChannelId = "test-channel"
            };

            _mockAdapter
                .Setup(a => a.CreateConversationAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<ConversationParameters>(),
                    It.IsAny<AgentCallbackHandler>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(newReference);

            // Act
            var newConversation = await _proactive.CreateConversationAsync(_mockAdapter.Object, createInfo);

            // Assert
            Assert.NotNull(newConversation);
            Assert.Equal("new-conversation-id", newConversation.Reference.Conversation.Id);
        }

        [Fact]
        public async Task CreateConversationAsync_NullAdapter_ShouldThrow()
        {
            // Arrange
            var createInfo = CreateConversationOptionsBuilder.Create("bot", "channel", "serviceUrl").WithUser("user1", "User 1")
                .Build();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _proactive.CreateConversationAsync(null, createInfo));
        }

        [Fact]
        public async Task CreateConversationAsync_NullCreateInfo_ShouldThrow()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _proactive.CreateConversationAsync(_mockAdapter.Object, null));
        }

        [Fact]
        public async Task CreateConversationAsync_WithContinuation_ShouldCallHandler()
        {
            // Arrange
            var createInfo = CreateConversationOptionsBuilder.Create("bot", "test-channel", "serviceUrl")
                .WithUser("user1", "User 1")
                .Build();

            var newReference = new ConversationReference
            {
                Conversation = new ConversationAccount { Id = "new-conversation-id" },
                ServiceUrl = createInfo.ServiceUrl,
                ChannelId = createInfo.ChannelId,
                User = createInfo.Parameters.Members[0],
                Agent = createInfo.Parameters.Agent
            };

            var handlerCalled = false;
            RouteHandler handler = (ITurnContext tc, ITurnState ts, CancellationToken ct) =>
            {
                handlerCalled = true;
                Assert.Equal("test-channel", tc.Activity.ChannelId);
                Assert.Equal("new-conversation-id", tc.Activity.Conversation.Id);
                Assert.Equal("user1", tc.Activity.From.Id);
                return Task.CompletedTask;
            };

            _mockAdapter
                .Setup(a => a.CreateConversationAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<ConversationParameters>(),
                    It.IsAny<AgentCallbackHandler>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(newReference);

            _mockAdapter
                .Setup(a => a.ProcessProactiveAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<IActivity>(),
                    It.IsAny<string>(),
                    It.IsAny<AgentCallbackHandler>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback<ClaimsIdentity, IActivity, string, AgentCallbackHandler, CancellationToken>(
                    async (identity, activity, audience, callback, ct) => await callback(new TurnContext(_mockAdapter.Object, activity, identity), ct));

            // Act
            var newConversation = await _proactive.CreateConversationAsync(_mockAdapter.Object, createInfo, handler, continuationActivityFactory: (reference) => newReference.GetCreateContinuationActivity());

            // Assert
            Assert.True(handlerCalled);
            Assert.NotNull(newConversation);
            Assert.Equal("new-conversation-id", newConversation.Reference.Conversation.Id);
        }

        #endregion

        #region ContinueConversationValueType Tests

        [Fact]
        public void ContinueConversationValueType_ShouldHaveCorrectValue()
        {
            // Assert
            Assert.Equal("application/vnd.microsoft.activity.continueconversation+json",
                Proactive.ContinueConversationValueType);
        }

        #endregion
    }
}