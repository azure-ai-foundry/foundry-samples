// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Agents.TestSupport;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class ContinueConversationRouteTests
    {
        private readonly ITestOutputHelper _output;

        public ContinueConversationRouteTests(ITestOutputHelper output)
        {
            _output = output;
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidDelegateNameAndNoTokenHandlers_ShouldCreateInstance()
        {
            // Arrange & Act
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", string.Empty);

            // Assert
            Assert.NotNull(route);
            Assert.Null(route.TokenHandlers);
        }

        [Fact]
        public void Constructor_WithValidDelegateNameAndSingleTokenHandler_ShouldCreateInstance()
        {
            // Arrange & Act
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", "handler1");

            // Assert
            Assert.NotNull(route);
            Assert.NotNull(route.TokenHandlers);
            Assert.Single(route.TokenHandlers);
            Assert.Equal("handler1", route.TokenHandlers[0]);
        }

        [Fact]
        public void Constructor_WithValidDelegateNameAndMultipleTokenHandlers_ShouldParseDelimitedString()
        {
            // Arrange & Act
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", "handler1,handler2,handler3");

            // Assert
            Assert.NotNull(route);
            Assert.NotNull(route.TokenHandlers);
            Assert.Equal(3, route.TokenHandlers.Length);
            Assert.Equal("handler1", route.TokenHandlers[0]);
            Assert.Equal("handler2", route.TokenHandlers[1]);
            Assert.Equal("handler3", route.TokenHandlers[2]);
        }

        [Fact]
        public void Constructor_WithDelimitedTokenHandlersUsingSemicolon_ShouldParseCorrectly()
        {
            // Arrange & Act
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", "handler1;handler2;handler3");

            // Assert
            Assert.NotNull(route);
            Assert.NotNull(route.TokenHandlers);
            Assert.Equal(3, route.TokenHandlers.Length);
            Assert.Equal("handler1", route.TokenHandlers[0]);
            Assert.Equal("handler2", route.TokenHandlers[1]);
            Assert.Equal("handler3", route.TokenHandlers[2]);
        }

        [Fact]
        public void Constructor_WithDelimitedTokenHandlersUsingSpaces_ShouldParseCorrectly()
        {
            // Arrange & Act
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", "handler1 handler2 handler3");

            // Assert
            Assert.NotNull(route);
            Assert.NotNull(route.TokenHandlers);
            Assert.Equal(3, route.TokenHandlers.Length);
            Assert.Equal("handler1", route.TokenHandlers[0]);
            Assert.Equal("handler2", route.TokenHandlers[1]);
            Assert.Equal("handler3", route.TokenHandlers[2]);
        }

        [Fact]
        public void Constructor_WithDelimitedTokenHandlersWithMixedDelimiters_ShouldParseCorrectly()
        {
            // Arrange & Act
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", "handler1, handler2; handler3");

            // Assert
            Assert.NotNull(route);
            Assert.NotNull(route.TokenHandlers);
            Assert.Equal(3, route.TokenHandlers.Length);
            Assert.Equal("handler1", route.TokenHandlers[0]);
            Assert.Equal("handler2", route.TokenHandlers[1]);
            Assert.Equal("handler3", route.TokenHandlers[2]);
        }

        [Fact]
        public void Constructor_WithEmptyDelimitedTokenHandlers_ShouldReturnNull()
        {
            // Arrange & Act
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", "");

            // Assert
            Assert.NotNull(route);
            Assert.Null(route.TokenHandlers);
        }

        [Fact]
        public void Constructor_WithNullDelimitedTokenHandlers_ShouldReturnNull()
        {
            // Arrange & Act
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", (string)null);

            // Assert
            Assert.NotNull(route);
            Assert.Null(route.TokenHandlers);
        }

        [Fact]
        public void Constructor_WithTokenHandlersArray_ShouldSetTokenHandlers()
        {
            // Arrange
            var tokenHandlers = new[] { "handler1", "handler2", "handler3" };

            // Act
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", tokenHandlers);

            // Assert
            Assert.NotNull(route);
            Assert.NotNull(route.TokenHandlers);
            Assert.Equal(3, route.TokenHandlers.Length);
            Assert.Equal("handler1", route.TokenHandlers[0]);
            Assert.Equal("handler2", route.TokenHandlers[1]);
            Assert.Equal("handler3", route.TokenHandlers[2]);
        }

        [Fact]
        public void Constructor_WithNullTokenHandlersArray_ShouldSetTokenHandlersToNull()
        {
            // Arrange & Act
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", (string[])null);

            // Assert
            Assert.NotNull(route);
            Assert.Null(route.TokenHandlers);
        }

        [Fact]
        public void Constructor_WithInvalidDelegateName_ShouldThrowInvalidOperationException()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                new ContinueConversationRoute<TestAgentApplication>("NonExistentMethod", string.Empty));

            Assert.Contains("NonExistentMethod", exception.Message);
            Assert.Contains("was not found", exception.Message);
            Assert.Contains(typeof(TestAgentApplication).FullName, exception.Message);
        }

        [Fact]
        public void Constructor_WithPublicMethod_ShouldSucceed()
        {
            // Arrange & Act
            var route = new ContinueConversationRoute<TestAgentApplication>("PublicRouteHandler", string.Empty);

            // Assert
            Assert.NotNull(route);
        }

        [Fact]
        public void Constructor_WithPrivateMethod_ShouldSucceed()
        {
            // Arrange & Act
            var route = new ContinueConversationRoute<TestAgentApplication>("PrivateRouteHandler", string.Empty);

            // Assert
            Assert.NotNull(route);
        }

        [Fact]
        public void Constructor_WithProtectedMethod_ShouldSucceed()
        {
            // Arrange & Act
            var route = new ContinueConversationRoute<TestAgentApplication>("ProtectedRouteHandler", string.Empty);

            // Assert
            Assert.NotNull(route);
        }

        #endregion

        #region RouteHandler Tests

        [Fact]
        public void RouteHandler_ShouldReturnValidDelegate()
        {
            // Arrange
            var agent = new TestAgentApplication(new AgentApplicationOptions(new MemoryStorage()));
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", string.Empty);

            // Act
            var handler = route.RouteHandler(agent);

            // Assert
            Assert.NotNull(handler);
        }

        [Fact]
        public async Task RouteHandler_ShouldInvokeCorrectMethod()
        {
            // Arrange
            var agent = new TestAgentApplication(new AgentApplicationOptions(new MemoryStorage()));
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", string.Empty);
            var handler = route.RouteHandler(agent);

            var turnContext = CreateEmptyContext();
            var turnState = new TurnState();

            // Act
            await handler(turnContext, turnState, CancellationToken.None);

            // Assert
            Assert.True(agent.TestRouteHandlerCalled);
        }

        [Fact]
        public async Task RouteHandler_WithPublicMethod_ShouldInvokeCorrectly()
        {
            // Arrange
            var agent = new TestAgentApplication(new AgentApplicationOptions(new MemoryStorage()));
            var route = new ContinueConversationRoute<TestAgentApplication>("PublicRouteHandler", string.Empty);
            var handler = route.RouteHandler(agent);

            var turnContext = CreateEmptyContext();
            var turnState = new TurnState();

            // Act
            await handler(turnContext, turnState, CancellationToken.None);

            // Assert
            Assert.True(agent.PublicRouteHandlerCalled);
        }

        [Fact]
        public async Task RouteHandler_WithPrivateMethod_ShouldInvokeCorrectly()
        {
            // Arrange
            var agent = new TestAgentApplication(new AgentApplicationOptions(new MemoryStorage()));
            var route = new ContinueConversationRoute<TestAgentApplication>("PrivateRouteHandler", string.Empty);
            var handler = route.RouteHandler(agent);

            var turnContext = CreateEmptyContext();
            var turnState = new TurnState();

            // Act
            await handler(turnContext, turnState, CancellationToken.None);

            // Assert
            Assert.True(agent.PrivateRouteHandlerCalled);
        }

        [Fact]
        public async Task RouteHandler_WithProtectedMethod_ShouldInvokeCorrectly()
        {
            // Arrange
            var agent = new TestAgentApplication(new AgentApplicationOptions(new MemoryStorage()));
            var route = new ContinueConversationRoute<TestAgentApplication>("ProtectedRouteHandler", string.Empty);
            var handler = route.RouteHandler(agent);

            var turnContext = CreateEmptyContext();
            var turnState = new TurnState();

            // Act
            await handler(turnContext, turnState, CancellationToken.None);

            // Assert
            Assert.True(agent.ProtectedRouteHandlerCalled);
        }

        [Fact]
        public void RouteHandler_WithInvalidMethodSignature_ShouldThrowArgumentException()
        {
            // Arrange
            var agent = new TestAgentApplication(new AgentApplicationOptions(new MemoryStorage()));
            var route = new ContinueConversationRoute<TestAgentApplication>("InvalidSignatureMethod", string.Empty);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => route.RouteHandler(agent));
            ExceptionTester.IsException<ArgumentException>(exception, -50004, _output);
        }

        [Fact]
        public void RouteHandler_WithNonAsyncMethod_ShouldThrowArgumentException()
        {
            // Arrange
            var agent = new TestAgentApplication(new AgentApplicationOptions(new MemoryStorage()));
            var route = new ContinueConversationRoute<TestAgentApplication>("NonAsyncMethod", string.Empty);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => route.RouteHandler(agent));
            ExceptionTester.IsException<ArgumentException>(exception, -50004, _output);
        }

        #endregion

        #region TokenHandlers Property Tests

        [Fact]
        public void TokenHandlers_WithNoHandlers_ShouldBeNull()
        {
            // Arrange & Act
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", string.Empty);

            // Assert
            Assert.Null(route.TokenHandlers);
        }

        [Fact]
        public void TokenHandlers_WithHandlers_ShouldReturnArray()
        {
            // Arrange
            var tokenHandlers = new[] { "handler1", "handler2" };

            // Act
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", tokenHandlers);

            // Assert
            Assert.NotNull(route.TokenHandlers);
            Assert.Equal(2, route.TokenHandlers.Length);
        }

        #endregion

        #region Test Helper Class

        public class TestAgentApplication : AgentApplication
        {
            public TestAgentApplication(AgentApplicationOptions options) : base(options)
            {
            }

            public bool TestRouteHandlerCalled { get; private set; }
            public bool PublicRouteHandlerCalled { get; private set; }
            public bool PrivateRouteHandlerCalled { get; private set; }
            public bool ProtectedRouteHandlerCalled { get; private set; }

            public Task TestRouteHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                TestRouteHandlerCalled = true;
                return Task.CompletedTask;
            }

            public Task PublicRouteHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                PublicRouteHandlerCalled = true;
                return Task.CompletedTask;
            }

            private Task PrivateRouteHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                PrivateRouteHandlerCalled = true;
                return Task.CompletedTask;
            }

            protected Task ProtectedRouteHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                ProtectedRouteHandlerCalled = true;
                return Task.CompletedTask;
            }

            // Invalid signature - missing ITurnState parameter
            public Task InvalidSignatureMethod(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            // Non-async method
            public void NonAsyncMethod(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
            }
        }

        private static TurnContext CreateEmptyContext()
        {
            var b = new TestAdapter();
            var a = new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = "EmptyContext",
                From = new ChannelAccount
                {
                    Id = "empty@empty.context.org",
                },

                Conversation = new ConversationAccount()
                {
                    Id = "213123123123",
                },
            };
            var bc = new TurnContext(b, a);

            return bc;
        }
        #endregion
    }
}