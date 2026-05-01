// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Moq;
using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class FeedbackRouteBuilderTests
    {
        #region Constructor Tests

        [Fact]
        public void FeedbackRouteBuilder_Constructor_SetsInvokeFlag()
        {
            // Act
            var builder = new FeedbackRouteBuilder();
            var route = builder
                .WithHandler((context, state, data, ct) => Task.CompletedTask)
                .Build();

            // Assert
            Assert.True(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        #endregion

        #region FeedbackRouteBuilder.Create Tests

        [Fact]
        public void FeedbackRouteBuilder_Create_ReturnsNewInstance()
        {
            // Act
            var builder = FeedbackRouteBuilder.Create();

            // Assert
            Assert.NotNull(builder);
            Assert.IsType<FeedbackRouteBuilder>(builder);
        }

        [Fact]
        public void FeedbackRouteBuilder_Create_SetsInvokeFlagByDefault()
        {
            // Act
            var builder = FeedbackRouteBuilder.Create();
            var route = builder
                .WithHandler((context, state, data, ct) => Task.CompletedTask)
                .Build();

            // Assert
            Assert.True(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        #endregion

        #region WithHandler Tests

        [Fact]
        public void FeedbackRouteBuilder_WithHandler_SetsHandler()
        {
            // Arrange
            var builder = FeedbackRouteBuilder.Create();
            FeedbackLoopHandler handler = (context, state, data, token) => Task.CompletedTask;

            // Act
            var result = builder.WithHandler(handler);

            // Assert
            Assert.Same(builder, result);
        }

        [Fact]
        public void FeedbackRouteBuilder_WithHandler_NullHandler_ThrowsArgumentNullException()
        {
            // Arrange
            var builder = FeedbackRouteBuilder.Create();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.WithHandler(null));
        }

        [Fact]
        public void FeedbackRouteBuilder_WithHandler_ReturnsBuilderForMethodChaining()
        {
            // Arrange
            var builder = FeedbackRouteBuilder.Create();
            FeedbackLoopHandler handler = (context, state, data, token) => Task.CompletedTask;

            // Act
            var result = builder
                .WithHandler(handler)
                .WithOrderRank(5);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task FeedbackRouteBuilder_WithHandler_SetsSelectorCorrectly()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            var feedbackValue = new JsonObject
            {
                ["actionName"] = "feedback",
                ["actionValue"] = new JsonObject
                {
                    ["reaction"] = "like",
                    ["feedback"] = "Great response!"
                }
            };
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "message/submitAction",
                Value = feedbackValue,
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount { Id = "bot" }
            });

            var builder = FeedbackRouteBuilder.Create();
            var handlerCalled = false;

            // Act
            var route = builder
                .WithHandler((context, state, data, ct) =>
                {
                    handlerCalled = true;
                    return Task.CompletedTask;
                })
                .Build();

            var selectorResult = await route.Selector(mockContext.Object, CancellationToken.None);
            await route.Handler(mockContext.Object, Mock.Of<ITurnState>(), CancellationToken.None);

            // Assert
            Assert.True(selectorResult);
            Assert.True(handlerCalled);
        }

        [Fact]
        public async Task FeedbackRouteBuilder_WithHandler_SelectorReturnsFalse_WhenActivityTypeIsNotInvoke()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            var feedbackValue = new JsonObject
            {
                ["actionName"] = "feedback"
            };
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Message, // Not Invoke
                Name = "message/submitAction",
                Value = feedbackValue,
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount { Id = "bot" }
            });

            var builder = FeedbackRouteBuilder.Create();
            var route = builder
                .WithHandler((context, state, data, ct) => Task.CompletedTask)
                .Build();

            // Act
            var selectorResult = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.False(selectorResult);
        }

        [Fact]
        public async Task FeedbackRouteBuilder_WithHandler_SelectorReturnsFalse_WhenActivityNameIsNotSubmitAction()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            var feedbackValue = new JsonObject
            {
                ["actionName"] = "feedback"
            };
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "message/someOtherAction", // Not submitAction
                Value = feedbackValue,
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount { Id = "bot" }
            });

            var builder = FeedbackRouteBuilder.Create();
            var route = builder
                .WithHandler((context, state, data, ct) => Task.CompletedTask)
                .Build();

            // Act
            var selectorResult = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.False(selectorResult);
        }

        [Fact]
        public async Task FeedbackRouteBuilder_WithHandler_SelectorReturnsFalse_WhenActionNameIsNotFeedback()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            var feedbackValue = new JsonObject
            {
                ["actionName"] = "someOtherAction" // Not feedback
            };
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "message/submitAction",
                Value = feedbackValue,
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount { Id = "bot" }
            });

            var builder = FeedbackRouteBuilder.Create();
            var route = builder
                .WithHandler((context, state, data, ct) => Task.CompletedTask)
                .Build();

            // Act
            var selectorResult = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.False(selectorResult);
        }

        [Fact]
        public async Task FeedbackRouteBuilder_WithHandler_SelectorReturnsFalse_WhenActionNameIsMissing()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            var feedbackValue = new JsonObject
            {
                ["someOtherProperty"] = "value" // No actionName
            };
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "message/submitAction",
                Value = feedbackValue,
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount { Id = "bot" }
            });

            var builder = FeedbackRouteBuilder.Create();
            var route = builder
                .WithHandler((context, state, data, ct) => Task.CompletedTask)
                .Build();

            // Act
            var selectorResult = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.False(selectorResult);
        }

        [Fact]
        public async Task FeedbackRouteBuilder_WithHandler_SelectorReturnsFalse_WhenValueIsNull()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "message/submitAction",
                Value = null, // Null value
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount { Id = "bot" }
            });

            var builder = FeedbackRouteBuilder.Create();
            var route = builder
                .WithHandler((context, state, data, ct) => Task.CompletedTask)
                .Build();

            // Act
            var selectorResult = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.False(selectorResult);
        }

        [Fact]
        public async Task FeedbackRouteBuilder_WithHandler_InvokesHandlerWithCorrectFeedbackData()
        {
            // Arrange
            IActivity sentActivity = null;
            var mockContext = new Mock<ITurnContext>();

            var feedbackValue = new JsonObject
            {
                ["actionName"] = "feedback",
                ["actionValue"] = new JsonObject
                {
                    ["reaction"] = "like",
                    ["feedback"] = "Great response!"
                },
                ["replyToId"] = "activity123"
            };

            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "message/submitAction",
                Value = feedbackValue,
                ReplyToId = "activity123",
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount { Id = "bot" }
            });
            mockContext.Setup(c => c.SendActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .Callback<IActivity, CancellationToken>((activity, ct) => sentActivity = activity)
                .ReturnsAsync(new ResourceResponse());

            FeedbackData capturedData = null;
            var builder = FeedbackRouteBuilder.Create();
            var route = builder
                .WithHandler((context, state, data, ct) =>
                {
                    capturedData = data;
                    return Task.CompletedTask;
                })
                .Build();

            // Act
            await route.Handler(mockContext.Object, null, CancellationToken.None);

            // Assert
            Assert.NotNull(sentActivity);
            Assert.Equal(ActivityTypes.InvokeResponse, sentActivity.Type);
            Assert.NotNull(capturedData);
            Assert.Equal("feedback", capturedData.ActionName);
            Assert.Equal("activity123", capturedData.ReplyToId);
            Assert.NotNull(capturedData.ActionValue);
        }

        [Fact]
        public async Task FeedbackRouteBuilder_WithHandler_SendsInvokeResponseWhenNotAlreadySent()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();

            var feedbackValue = new JsonObject
            {
                ["actionName"] = "feedback",
                ["actionValue"] = new JsonObject()
            };

            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "message/submitAction",
                Value = feedbackValue,
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount { Id = "bot" }
            });

            mockContext.Setup(c => c.SendActivityAsync(
                It.IsAny<IActivity>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse());

            var builder = FeedbackRouteBuilder.Create();
            var route = builder
                .WithHandler((context, state, data, ct) => Task.CompletedTask)
                .Build();

            // Act
            await route.Handler(mockContext.Object, null, CancellationToken.None);

            // Assert
            mockContext.Verify(c => c.SendActivityAsync(
                It.Is<IActivity>(a => a.Type == ActivityTypes.InvokeResponse),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task FeedbackRouteBuilder_WithHandler_SetsReplyToIdFromActivity()
        {
            // Arrange
            IActivity sentActivity = null;
            var mockContext = new Mock<ITurnContext>();

            var feedbackValue = new JsonObject
            {
                ["actionName"] = "feedback",
                ["actionValue"] = new JsonObject()
            };

            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "message/submitAction",
                Value = feedbackValue,
                ReplyToId = "specificReplyToId",
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount { Id = "bot" }
            });

            mockContext.Setup(c => c.SendActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .Callback<IActivity, CancellationToken>((activity, ct) => sentActivity = activity)
                .ReturnsAsync(new ResourceResponse());

            FeedbackData capturedData = null;
            var builder = FeedbackRouteBuilder.Create();
            var route = builder
                .WithHandler((context, state, data, ct) =>
                {
                    capturedData = data;
                    return Task.CompletedTask;
                })
                .Build();

            // Act
            await route.Handler(mockContext.Object, null, CancellationToken.None);

            // Assert
            Assert.NotNull(sentActivity);
            Assert.Equal(ActivityTypes.InvokeResponse, sentActivity.Type);
            Assert.NotNull(capturedData);
            Assert.Equal("specificReplyToId", capturedData.ReplyToId);
        }

        [Fact]
        public async Task FeedbackRouteBuilder_WithHandler_RespectsCancellationToken()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();

            var feedbackValue = new JsonObject
            {
                ["actionName"] = "feedback",
                ["actionValue"] = new JsonObject()
            };

            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "message/submitAction",
                Value = feedbackValue,
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount { Id = "bot" }
            });

            var cts = new CancellationTokenSource();
            cts.Cancel();

            var builder = FeedbackRouteBuilder.Create();
            var route = builder
                .WithHandler((context, state, data, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    return Task.CompletedTask;
                })
                .Build();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await route.Handler(mockContext.Object, null, cts.Token));
        }

        #endregion

        #region AsInvoke Tests

        [Fact]
        public void FeedbackRouteBuilder_AsInvoke_WithTrue_ReturnsBuilderInstance()
        {
            // Arrange
            var builder = FeedbackRouteBuilder.Create();

            // Act
            var result = builder.AsInvoke(true);

            // Assert
            Assert.Same(builder, result);
        }

        [Fact]
        public void FeedbackRouteBuilder_AsInvoke_WithFalse_ReturnsBuilderInstance()
        {
            // Arrange
            var builder = FeedbackRouteBuilder.Create();

            // Act
            var result = builder.AsInvoke(false);

            // Assert
            Assert.Same(builder, result);
        }

        [Fact]
        public void FeedbackRouteBuilder_AsInvoke_MaintainsInvokeFlagRegardlessOfParameter()
        {
            // Arrange
            var builder = FeedbackRouteBuilder.Create();

            // Act
            builder.AsInvoke(false);
            var route = builder
                .WithHandler((context, state, data, ct) => Task.CompletedTask)
                .Build();

            // Assert - Invoke flag should remain set because FeedbackRouteBuilder overrides AsInvoke
            Assert.True(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        [Fact]
        public void FeedbackRouteBuilder_AsInvoke_DefaultParameter_ReturnsBuilderInstance()
        {
            // Arrange
            var builder = FeedbackRouteBuilder.Create();

            // Act
            var result = builder.AsInvoke();

            // Assert
            Assert.Same(builder, result);
        }

        #endregion

        #region Integration Tests with Inherited Methods

        [Fact]
        public void FeedbackRouteBuilder_WithChannelId_ReturnsBuilderForMethodChaining()
        {
            // Arrange
            var builder = FeedbackRouteBuilder.Create();

            // Act
            var result = builder
                .WithChannelId(Channels.Msteams)
                .WithHandler((context, state, data, ct) => Task.CompletedTask);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task FeedbackRouteBuilder_WithChannelId_FiltersByChannel()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            var feedbackValue = new JsonObject
            {
                ["actionName"] = "feedback"
            };

            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "message/submitAction",
                Value = feedbackValue,
                ChannelId = Channels.Emulator, // Different channel
                Recipient = new ChannelAccount { Id = "bot" }
            });

            var builder = FeedbackRouteBuilder.Create();
            var route = builder
                .WithChannelId(Channels.Msteams) // Expecting Teams
                .WithHandler((context, state, data, ct) => Task.CompletedTask)
                .Build();

            // Act
            var selectorResult = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.False(selectorResult);
        }

        [Fact]
        public void FeedbackRouteBuilder_AsAgentic_ReturnsBuilderForMethodChaining()
        {
            // Arrange
            var builder = FeedbackRouteBuilder.Create();

            // Act
            var result = builder
                .AsAgentic()
                .WithHandler((context, state, data, ct) => Task.CompletedTask);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public void FeedbackRouteBuilder_AsNonTerminal_ReturnsBuilderForMethodChaining()
        {
            // Arrange
            var builder = FeedbackRouteBuilder.Create();

            // Act
            var result = builder
                .AsNonTerminal()
                .WithHandler((context, state, data, ct) => Task.CompletedTask);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public void FeedbackRouteBuilder_WithOAuthHandlers_String_ReturnsBuilderForMethodChaining()
        {
            // Arrange
            var builder = FeedbackRouteBuilder.Create();

            // Act
            var result = builder
                .WithOAuthHandlers("handler1,handler2")
                .WithHandler((context, state, data, ct) => Task.CompletedTask);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public void FeedbackRouteBuilder_WithOAuthHandlers_Array_ReturnsBuilderForMethodChaining()
        {
            // Arrange
            var builder = FeedbackRouteBuilder.Create();

            // Act
            var result = builder
                .WithOAuthHandlers(new[] { "handler1", "handler2" })
                .WithHandler((context, state, data, ct) => Task.CompletedTask);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public void FeedbackRouteBuilder_WithOAuthHandlers_Func_ReturnsBuilderForMethodChaining()
        {
            // Arrange
            var builder = FeedbackRouteBuilder.Create();

            // Act
            var result = builder
                .WithOAuthHandlers(ctx => new[] { "handler1" })
                .WithHandler((context, state, data, ct) => Task.CompletedTask);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public void FeedbackRouteBuilder_Build_ThrowsWhenHandlerNotSet()
        {
            // Arrange
            var builder = FeedbackRouteBuilder.Create();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.Build());
        }

        [Fact]
        public void FeedbackRouteBuilder_ComplexChaining_BuildsSuccessfully()
        {
            // Arrange
            var builder = FeedbackRouteBuilder.Create();

            // Act
            var route = builder
                .WithChannelId(Channels.Msteams)
                .WithOrderRank(10)
                .AsNonTerminal()
                .WithOAuthHandlers("oauth1,oauth2")
                .WithHandler((context, state, data, ct) => Task.CompletedTask)
                .Build();

            // Assert
            Assert.NotNull(route);
            Assert.True(route.Flags.HasFlag(RouteFlags.Invoke));
            Assert.True(route.Flags.HasFlag(RouteFlags.NonTerminal));
            Assert.Equal((ushort)10, route.Rank);
        }

        #endregion
    }
}