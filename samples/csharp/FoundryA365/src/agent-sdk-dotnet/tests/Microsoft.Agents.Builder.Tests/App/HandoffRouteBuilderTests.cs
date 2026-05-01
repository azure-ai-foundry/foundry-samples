// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class HandoffRouteBuilderTests
    {
        #region HandoffRouteBuilder.Create Tests

        [Fact]
        public void HandoffRouteBuilder_Create_ReturnsNewInstance()
        {
            // Act
            var builder = HandoffRouteBuilder.Create();

            // Assert
            Assert.NotNull(builder);
            Assert.IsType<HandoffRouteBuilder>(builder);
        }

        [Fact]
        public void HandoffRouteBuilder_Create_SetsInvokeFlagByDefault()
        {
            // Act
            var builder = HandoffRouteBuilder.Create();
            var route = builder
                .WithHandler((context, state, continuation, token) => Task.CompletedTask)
                .Build();

            // Assert
            Assert.True(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        #endregion

        #region WithHandler Tests

        [Fact]
        public void HandoffRouteBuilder_WithHandler_SetsHandler()
        {
            // Arrange
            var builder = HandoffRouteBuilder.Create();
            HandoffHandler handler = (context, state, continuation, token) => Task.CompletedTask;

            // Act
            var result = builder.WithHandler(handler);

            // Assert
            Assert.Same(builder, result);
            var route = result.Build();
            Assert.NotNull(route.Handler);
        }

        [Fact]
        public void HandoffRouteBuilder_WithHandler_NullHandler_ThrowsArgumentNullException()
        {
            // Arrange
            var builder = HandoffRouteBuilder.Create();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.WithHandler(null));
        }

        [Fact]
        public void HandoffRouteBuilder_WithHandler_ReturnsBuilderForMethodChaining()
        {
            // Arrange
            var builder = HandoffRouteBuilder.Create();
            HandoffHandler handler = (context, state, continuation, token) => Task.CompletedTask;

            // Act
            var result = builder
                .WithHandler(handler)
                .WithChannelId(Channels.Msteams);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task HandoffRouteBuilder_WithHandler_SetsCorrectSelector()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "handoff/action"
            });

            var builder = HandoffRouteBuilder.Create()
                .WithHandler((context, state, continuation, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task HandoffRouteBuilder_WithHandler_ExtractsContinuationToken()
        {
            // Arrange
            string capturedContinuation = null;
            var expectedContinuation = "test-continuation-token";

            var mockContext = new Mock<ITurnContext>();
            var mockActivity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "handoff/action",
                Value = new { Continuation = expectedContinuation }
            };
            mockContext.Setup(c => c.Activity).Returns(mockActivity);
            mockContext.Setup(c => c.SendActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse());

            HandoffHandler handler = (context, state, continuation, token) =>
            {
                capturedContinuation = continuation;
                return Task.CompletedTask;
            };

            var route = HandoffRouteBuilder.Create()
                .WithHandler(handler)
                .Build();

            // Act
            await route.Handler(mockContext.Object, null, CancellationToken.None);

            // Assert
            Assert.Equal(expectedContinuation, capturedContinuation);
        }

        [Fact]
        public async Task HandoffRouteBuilder_WithHandler_HandlesNullContinuationToken()
        {
            // Arrange
            string capturedContinuation = null;

            var mockContext = new Mock<ITurnContext>();
            var mockActivity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "handoff/action",
                Value = new { SomeOtherProperty = "value" }
            };
            mockContext.Setup(c => c.Activity).Returns(mockActivity);
            mockContext.Setup(c => c.SendActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse());

            HandoffHandler handler = (context, state, continuation, token) =>
            {
                capturedContinuation = continuation;
                return Task.CompletedTask;
            };

            var route = HandoffRouteBuilder.Create()
                .WithHandler(handler)
                .Build();

            // Act
            await route.Handler(mockContext.Object, null, CancellationToken.None);

            // Assert
            Assert.Equal("", capturedContinuation);
        }

        [Fact]
        public async Task HandoffRouteBuilder_WithHandler_HandlesNullValue()
        {
            // Arrange
            string capturedContinuation = null;

            var mockContext = new Mock<ITurnContext>();
            var mockActivity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "handoff/action",
                Value = null
            };
            mockContext.Setup(c => c.Activity).Returns(mockActivity);
            mockContext.Setup(c => c.SendActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse());

            HandoffHandler handler = (context, state, continuation, token) =>
            {
                capturedContinuation = continuation;
                return Task.CompletedTask;
            };

            var route = HandoffRouteBuilder.Create()
                .WithHandler(handler)
                .Build();

            // Act
            await route.Handler(mockContext.Object, null, CancellationToken.None);

            // Assert
            Assert.Equal("", capturedContinuation);
        }

        [Fact]
        public async Task HandoffRouteBuilder_WithHandler_SendsInvokeResponseActivity()
        {
            // Arrange
            IActivity sentActivity = null;

            var mockContext = new Mock<ITurnContext>();
            var mockActivity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "handoff/action",
                Value = new { Continuation = "test-token" }
            };
            mockContext.Setup(c => c.Activity).Returns(mockActivity);
            mockContext.Setup(c => c.SendActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .Callback<IActivity, CancellationToken>((activity, ct) => sentActivity = activity)
                .ReturnsAsync(new ResourceResponse());

            bool handlerExecuted = false;
            var route = HandoffRouteBuilder.Create()
                .WithHandler((context, state, continuation, token) =>
                {
                    handlerExecuted = true;
                    return Task.CompletedTask;
                })
                .Build();

            // Act
            await route.Handler(mockContext.Object, null, CancellationToken.None);

            // Assert
            Assert.NotNull(sentActivity);
            Assert.Equal(ActivityTypes.InvokeResponse, sentActivity.Type);
            Assert.True(handlerExecuted);
        }

        #endregion

        #region Selector Tests

        [Fact]
        public async Task HandoffRouteBuilder_Selector_MatchesInvokeActivityWithHandoffAction()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "handoff/action"
            });

            var route = HandoffRouteBuilder.Create()
                .WithHandler((context, state, continuation, token) => Task.CompletedTask)
                .Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task HandoffRouteBuilder_Selector_MatchesCaseInsensitive()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "HANDOFF/ACTION"
            });

            var route = HandoffRouteBuilder.Create()
                .WithHandler((context, state, continuation, token) => Task.CompletedTask)
                .Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task HandoffRouteBuilder_Selector_DoesNotMatchDifferentActivityType()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Message,
                Name = "handoff/action"
            });

            var route = HandoffRouteBuilder.Create()
                .WithHandler((context, state, continuation, token) => Task.CompletedTask)
                .Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task HandoffRouteBuilder_Selector_DoesNotMatchDifferentName()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "other/action"
            });

            var route = HandoffRouteBuilder.Create()
                .WithHandler((context, state, continuation, token) => Task.CompletedTask)
                .Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task HandoffRouteBuilder_Selector_DoesNotMatchNullName()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = null
            });

            var route = HandoffRouteBuilder.Create()
                .WithHandler((context, state, continuation, token) => Task.CompletedTask)
                .Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task HandoffRouteBuilder_Selector_RespectsChannelId()
        {
            // Arrange - Matching channel
            var matchingContext = new Mock<ITurnContext>();
            matchingContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "handoff/action",
                ChannelId = Channels.Msteams
            });

            // Arrange - Non-matching channel
            var nonMatchingContext = new Mock<ITurnContext>();
            nonMatchingContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "handoff/action",
                ChannelId = Channels.Directline
            });

            var route = HandoffRouteBuilder.Create()
                .WithHandler((context, state, continuation, token) => Task.CompletedTask)
                .WithChannelId(Channels.Msteams)
                .Build();

            // Act
            var matchingResult = await route.Selector(matchingContext.Object, CancellationToken.None);
            var nonMatchingResult = await route.Selector(nonMatchingContext.Object, CancellationToken.None);

            // Assert
            Assert.True(matchingResult);
            Assert.False(nonMatchingResult);
        }

        [Fact]
        public async Task HandoffRouteBuilder_Selector_RespectsAgenticFlag()
        {
            // Arrange - Agentic context
            var agenticContext = new Mock<ITurnContext>();
            agenticContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "handoff/action",
                Recipient = new ChannelAccount { Role = RoleTypes.AgenticUser }
            });

            // Arrange - Non-agentic context
            var nonAgenticContext = new Mock<ITurnContext>();
            nonAgenticContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "handoff/action",
                Recipient = new ChannelAccount()
            });

            var route = HandoffRouteBuilder.Create()
                .WithHandler((context, state, continuation, token) => Task.CompletedTask)
                .AsAgentic()
                .Build();

            // Act
            var agenticResult = await route.Selector(agenticContext.Object, CancellationToken.None);
            var nonAgenticResult = await route.Selector(nonAgenticContext.Object, CancellationToken.None);

            // Assert
            Assert.True(agenticResult);
            Assert.False(nonAgenticResult);
        }

        #endregion

        #region AsInvoke Tests

        [Fact]
        public void HandoffRouteBuilder_AsInvoke_AlwaysReturnsInvokeFlagSet()
        {
            // Arrange
            var builder = HandoffRouteBuilder.Create();

            // Act
            var result = builder.AsInvoke();

            // Assert
            var route = builder
                .WithHandler((context, state, continuation, token) => Task.CompletedTask)
                .Build();
            Assert.True(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        [Fact]
        public void HandoffRouteBuilder_AsInvoke_False_StillReturnsInvokeFlagSet()
        {
            // Arrange
            var builder = HandoffRouteBuilder.Create();

            // Act
            var result = builder.AsInvoke(false);

            // Assert
            var route = builder
                .WithHandler((context, state, continuation, token) => Task.CompletedTask)
                .Build();
            // Invoke flag should still be set because HandoffRouteBuilder overrides AsInvoke
            Assert.True(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        [Fact]
        public void HandoffRouteBuilder_AsInvoke_ReturnsBuilderForMethodChaining()
        {
            // Arrange
            var builder = HandoffRouteBuilder.Create();

            // Act
            var result = builder
                .AsInvoke()
                .WithHandler((context, state, continuation, token) => Task.CompletedTask);

            // Assert
            Assert.NotNull(result);
        }

        #endregion

        #region Build Tests

        [Fact]
        public void HandoffRouteBuilder_Build_WithHandler_ReturnsRoute()
        {
            // Arrange
            HandoffHandler handler = (context, state, continuation, token) => Task.CompletedTask;
            var builder = HandoffRouteBuilder.Create()
                .WithHandler(handler);

            // Act
            var route = builder.Build();

            // Assert
            Assert.NotNull(route);
            Assert.NotNull(route.Selector);
            Assert.NotNull(route.Handler);
            Assert.True(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        [Fact]
        public void HandoffRouteBuilder_Build_WithoutHandler_ThrowsArgumentNullException()
        {
            // Arrange
            var builder = HandoffRouteBuilder.Create();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.Build());
        }

        [Fact]
        public void HandoffRouteBuilder_Build_WithAllProperties_ReturnsRoute()
        {
            // Arrange
            HandoffHandler handler = (context, state, continuation, token) => Task.CompletedTask;
            var builder = HandoffRouteBuilder.Create()
                .WithHandler(handler)
                .WithChannelId(Channels.Msteams)
                .WithOrderRank(5)
                .AsAgentic()
                .WithOAuthHandlers("handler1,handler2");

            // Act
            var route = builder.Build();

            // Assert
            Assert.NotNull(route);
            Assert.NotNull(route.Selector);
            Assert.NotNull(route.Handler);
            Assert.Equal(Channels.Msteams, route.ChannelId);
            Assert.Equal((ushort)5, route.Rank);
            Assert.True(route.Flags.HasFlag(RouteFlags.Invoke));
            Assert.True(route.Flags.HasFlag(RouteFlags.Agentic));
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void HandoffRouteBuilder_FluentAPI_AllMethodsChainCorrectly()
        {
            // Arrange & Act
            var route = HandoffRouteBuilder.Create()
                .WithHandler((context, state, continuation, token) => Task.CompletedTask)
                .WithChannelId(Channels.Msteams)
                .WithOrderRank(10)
                .AsAgentic()
                .AsNonTerminal()
                .WithOAuthHandlers("handler1,handler2")
                .Build();

            // Assert
            Assert.NotNull(route);
            Assert.Equal(Channels.Msteams, route.ChannelId);
            Assert.Equal((ushort)10, route.Rank);
            Assert.True(route.Flags.HasFlag(RouteFlags.Invoke));
            Assert.True(route.Flags.HasFlag(RouteFlags.Agentic));
            Assert.True(route.Flags.HasFlag(RouteFlags.NonTerminal));
        }

        [Fact]
        public async Task HandoffRouteBuilder_CompleteScenario_ExecutesCorrectly()
        {
            // Arrange
            var handlerExecuted = false;
            string capturedContinuation = null;
            var expectedContinuation = "test-continuation";

            var mockContext = new Mock<ITurnContext>();
            var mockActivity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "handoff/action",
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount { Role = RoleTypes.AgenticUser },
                Value = new { Continuation = expectedContinuation }
            };
            mockContext.Setup(c => c.Activity).Returns(mockActivity);
            mockContext.Setup(c => c.SendActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse());

            var route = HandoffRouteBuilder.Create()
                .WithHandler((context, state, continuation, token) =>
                {
                    handlerExecuted = true;
                    capturedContinuation = continuation;
                    return Task.CompletedTask;
                })
                .WithChannelId(Channels.Msteams)
                .AsAgentic()
                .Build();

            // Act
            var selectorResult = await route.Selector(mockContext.Object, CancellationToken.None);
            if (selectorResult)
            {
                await route.Handler(mockContext.Object, null, CancellationToken.None);
            }

            // Assert
            Assert.True(selectorResult);
            Assert.True(handlerExecuted);
            Assert.Equal(expectedContinuation, capturedContinuation);
        }

        [Fact]
        public async Task HandoffRouteBuilder_WithCancellationToken_PassesThroughCorrectly()
        {
            // Arrange
            CancellationToken capturedToken = default;
            var expectedToken = new CancellationTokenSource().Token;

            var mockContext = new Mock<ITurnContext>();
            var mockActivity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "handoff/action",
                Value = new { Continuation = "test" }
            };
            mockContext.Setup(c => c.Activity).Returns(mockActivity);
            mockContext.Setup(c => c.SendActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse());

            var route = HandoffRouteBuilder.Create()
                .WithHandler((context, state, continuation, token) =>
                {
                    capturedToken = token;
                    return Task.CompletedTask;
                })
                .Build();

            // Act
            await route.Handler(mockContext.Object, null, expectedToken);

            // Assert
            Assert.Equal(expectedToken, capturedToken);
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void HandoffRouteBuilder_Constructor_SetsInvokeFlag()
        {
            // Arrange & Act
            var builder = new HandoffRouteBuilder();
            var route = builder
                .WithHandler((context, state, continuation, token) => Task.CompletedTask)
                .Build();

            // Assert
            Assert.True(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        #endregion
    }
}