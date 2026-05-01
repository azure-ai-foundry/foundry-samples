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
    public class RouteBuilderTests
    {
        #region RouteBuilder.Create Tests

        [Fact]
        public void RouteBuilder_Create_ReturnsNewInstance()
        {
            // Act
            var builder = RouteBuilder.Create();

            // Assert
            Assert.NotNull(builder);
            Assert.IsType<RouteBuilder>(builder);
        }

        #endregion

        #region WithHandler Tests

        [Fact]
        public void RouteBuilder_WithHandler_SetsHandler()
        {
            // Arrange
            var builder = RouteBuilder.Create();
            RouteHandler handler = (context, state, token) => Task.CompletedTask;

            // Act
            var result = builder.WithHandler(handler);

            // Assert
            Assert.Same(builder, result);
            var route = builder
                .WithSelector((context, token) => Task.FromResult(true))
                .Build();
            Assert.Same(handler, route.Handler);
        }

        [Fact]
        public void RouteBuilder_WithHandler_NullHandler_ThrowsArgumentNullException()
        {
            // Arrange
            var builder = RouteBuilder.Create();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.WithHandler(null));
        }

        [Fact]
        public void RouteBuilder_WithHandler_ReturnsBuilderForMethodChaining()
        {
            // Arrange
            var builder = RouteBuilder.Create();
            RouteHandler handler = (context, state, token) => Task.CompletedTask;

            // Act
            var result = builder
                .WithHandler(handler)
                .WithSelector((context, token) => Task.FromResult(true));

            // Assert
            Assert.NotNull(result);
        }

        #endregion

        #region WithSelector Tests

        [Fact]
        public void RouteBuilder_WithSelector_SetsSelector()
        {
            // Arrange
            var builder = RouteBuilder.Create();
            RouteSelector selector = (context, token) => Task.FromResult(true);

            // Act
            var result = builder.WithSelector(selector);

            // Assert
            Assert.Same(builder, result);
        }

        [Fact]
        public void RouteBuilder_WithSelector_NullSelector_ThrowsArgumentNullException()
        {
            // Arrange
            var builder = RouteBuilder.Create();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.WithSelector(null));
        }

        [Fact]
        public async Task RouteBuilder_WithSelector_WrapsWithIsContextMatch()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount { Id = "bot", Role = RoleTypes.AgenticUser }
            });

            var selectorCalled = false;
            RouteSelector selector = (context, token) =>
            {
                selectorCalled = true;
                return Task.FromResult(true);
            };

            var builder = RouteBuilder.Create()
                .WithSelector(selector)
                .WithHandler((context, state, token) => Task.CompletedTask)
                .AsAgentic();

            var route = builder.Build();

            // Act - Agentic request should pass
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.True(result);
            Assert.True(selectorCalled);
        }

        [Fact]
        public async Task RouteBuilder_WithSelector_BlocksNonAgenticRequest_WhenAgenticFlagSet()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount { Id = "bot" } // No agentic role
            });

            var selectorCalled = false;
            RouteSelector selector = (context, token) =>
            {
                selectorCalled = true;
                return Task.FromResult(true);
            };

            var builder = RouteBuilder.Create()
                .WithSelector(selector)
                .WithHandler((context, state, token) => Task.CompletedTask)
                .AsAgentic();

            var route = builder.Build();

            // Act - Non-agentic request should be blocked
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.False(result);
            Assert.False(selectorCalled); // Should not reach the selector
        }

        #endregion

        #region WithChannelId Tests

        [Fact]
        public void RouteBuilder_WithChannelId_SetsChannelId()
        {
            // Arrange
            var builder = RouteBuilder.Create();
            var channelId = Channels.Msteams;

            // Act
            var result = builder.WithChannelId(channelId);

            // Assert
            Assert.Same(builder, result);
            var route = builder
                .WithSelector((context, token) => Task.FromResult(true))
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();
            Assert.Equal(channelId, route.ChannelId);
        }

        [Fact]
        public void RouteBuilder_WithChannelId_ReturnsBuilderForMethodChaining()
        {
            // Arrange
            var builder = RouteBuilder.Create();

            // Act
            var result = builder
                .WithChannelId(Channels.Msteams)
                .WithSelector((context, token) => Task.FromResult(true));

            // Assert
            Assert.NotNull(result);
        }

        #endregion

        #region WithOAuthHandlers Tests

        [Fact]
        public void RouteBuilder_WithOAuthHandlers_String_ParsesDelimitedString()
        {
            // Arrange
            var builder = RouteBuilder.Create();
            var delimitedHandlers = "handler1,handler2;handler3 handler4";

            // Act
            var result = builder.WithOAuthHandlers(delimitedHandlers);

            // Assert
            Assert.Same(builder, result);
            var route = builder
                .WithSelector((context, token) => Task.FromResult(true))
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();

            var mockContext = new Mock<ITurnContext>();
            var handlers = route.OAuthHandlers(mockContext.Object);
            Assert.Equal(4, handlers.Length);
            Assert.Contains("handler1", handlers);
            Assert.Contains("handler2", handlers);
            Assert.Contains("handler3", handlers);
            Assert.Contains("handler4", handlers);
        }

        [Fact]
        public void RouteBuilder_WithOAuthHandlers_String_EmptyString_ReturnsEmptyArray()
        {
            // Arrange
            var builder = RouteBuilder.Create();

            // Act
            var result = builder.WithOAuthHandlers("");

            // Assert
            var route = builder
                .WithSelector((context, token) => Task.FromResult(true))
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();

            var mockContext = new Mock<ITurnContext>();
            var handlers = route.OAuthHandlers(mockContext.Object);
            Assert.Empty(handlers);
        }

        [Fact]
        public void RouteBuilder_WithOAuthHandlers_String_NullString_ReturnsEmptyArray()
        {
            // Arrange
            var builder = RouteBuilder.Create();

            // Act
            var result = builder.WithOAuthHandlers((string)null);

            // Assert
            var route = builder
                .WithSelector((context, token) => Task.FromResult(true))
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();

            var mockContext = new Mock<ITurnContext>();
            var handlers = route.OAuthHandlers(mockContext.Object);
            Assert.Empty(handlers);
        }

        [Fact]
        public void RouteBuilder_WithOAuthHandlers_Array_SetsHandlers()
        {
            // Arrange
            var builder = RouteBuilder.Create();
            var handlersArray = new[] { "handler1", "handler2" };

            // Act
            var result = builder.WithOAuthHandlers(handlersArray);

            // Assert
            Assert.Same(builder, result);
            var route = builder
                .WithSelector((context, token) => Task.FromResult(true))
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();

            var mockContext = new Mock<ITurnContext>();
            var handlers = route.OAuthHandlers(mockContext.Object);
            Assert.Equal(2, handlers.Length);
            Assert.Equal(handlersArray, handlers);
        }

        [Fact]
        public void RouteBuilder_WithOAuthHandlers_Array_NullArray_ReturnsEmptyArray()
        {
            // Arrange
            var builder = RouteBuilder.Create();

            // Act
            var result = builder.WithOAuthHandlers((string[])null);

            // Assert
            var route = builder
                .WithSelector((context, token) => Task.FromResult(true))
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();

            var mockContext = new Mock<ITurnContext>();
            var handlers = route.OAuthHandlers(mockContext.Object);
            Assert.Empty(handlers);
        }

        [Fact]
        public void RouteBuilder_WithOAuthHandlers_Function_SetsFunction()
        {
            // Arrange
            var builder = RouteBuilder.Create();
            Func<ITurnContext, string[]> handlerFunc = context => new[] { "dynamic1", "dynamic2" };

            // Act
            var result = builder.WithOAuthHandlers(handlerFunc);

            // Assert
            Assert.Same(builder, result);
            var route = builder
                .WithSelector((context, token) => Task.FromResult(true))
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();

            var mockContext = new Mock<ITurnContext>();
            var handlers = route.OAuthHandlers(mockContext.Object);
            Assert.Equal(2, handlers.Length);
            Assert.Contains("dynamic1", handlers);
            Assert.Contains("dynamic2", handlers);
        }

        [Fact]
        public void RouteBuilder_WithOAuthHandlers_Function_NullFunction_ReturnsEmptyArray()
        {
            // Arrange
            var builder = RouteBuilder.Create();

            // Act
            var result = builder.WithOAuthHandlers((Func<ITurnContext, string[]>)null);

            // Assert
            var route = builder
                .WithSelector((context, token) => Task.FromResult(true))
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();

            var mockContext = new Mock<ITurnContext>();
            var handlers = route.OAuthHandlers(mockContext.Object);
            Assert.Empty(handlers);
        }

        #endregion

        #region GetOAuthHandlers Tests

        [Fact]
        public void RouteBuilder_GetOAuthHandlers_ParsesCommaDelimitedString()
        {
            // Act
            var handlers = RouteBuilder.GetOAuthHandlers("handler1,handler2,handler3");

            // Assert
            Assert.Equal(3, handlers.Length);
            Assert.Equal("handler1", handlers[0]);
            Assert.Equal("handler2", handlers[1]);
            Assert.Equal("handler3", handlers[2]);
        }

        [Fact]
        public void RouteBuilder_GetOAuthHandlers_ParsesSemicolonDelimitedString()
        {
            // Act
            var handlers = RouteBuilder.GetOAuthHandlers("handler1;handler2;handler3");

            // Assert
            Assert.Equal(3, handlers.Length);
        }

        [Fact]
        public void RouteBuilder_GetOAuthHandlers_ParsesSpaceDelimitedString()
        {
            // Act
            var handlers = RouteBuilder.GetOAuthHandlers("handler1 handler2 handler3");

            // Assert
            Assert.Equal(3, handlers.Length);
        }

        [Fact]
        public void RouteBuilder_GetOAuthHandlers_ParsesMixedDelimiters()
        {
            // Act
            var handlers = RouteBuilder.GetOAuthHandlers("handler1, handler2; handler3");

            // Assert
            Assert.Equal(3, handlers.Length);
            Assert.Equal("handler1", handlers[0]);
            Assert.Equal("handler2", handlers[1]);
            Assert.Equal("handler3", handlers[2]);
        }

        [Fact]
        public void RouteBuilder_GetOAuthHandlers_EmptyString_ReturnsNull()
        {
            // Act
            var handlers = RouteBuilder.GetOAuthHandlers("");

            // Assert
            Assert.Null(handlers);
        }

        [Fact]
        public void RouteBuilder_GetOAuthHandlers_NullString_ReturnsNull()
        {
            // Act
            var handlers = RouteBuilder.GetOAuthHandlers(null);

            // Assert
            Assert.Null(handlers);
        }

        #endregion

        #region AsInvoke Tests

        [Fact]
        public void RouteBuilder_AsInvoke_SetsInvokeFlag()
        {
            // Arrange
            var builder = RouteBuilder.Create();

            // Act
            var result = builder.AsInvoke();

            // Assert
            Assert.Same(builder, result);
            var route = builder
                .WithSelector((context, token) => Task.FromResult(true))
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();
            Assert.True(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        [Fact]
        public void RouteBuilder_AsInvoke_False_ClearsInvokeFlag()
        {
            // Arrange
            var builder = RouteBuilder.Create()
                .AsInvoke();

            // Act
            var result = builder.AsInvoke(false);

            // Assert
            var route = builder
                .WithSelector((context, token) => Task.FromResult(true))
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();
            Assert.False(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        [Fact]
        public void RouteBuilder_AsInvoke_ReturnsBuilderForMethodChaining()
        {
            // Arrange
            var builder = RouteBuilder.Create();

            // Act
            var result = builder
                .AsInvoke()
                .WithSelector((context, token) => Task.FromResult(true));

            // Assert
            Assert.NotNull(result);
        }

        #endregion

        #region AsAgentic Tests

        [Fact]
        public void RouteBuilder_AsAgentic_SetsAgenticFlag()
        {
            // Arrange
            var builder = RouteBuilder.Create();

            // Act
            var result = builder.AsAgentic();

            // Assert
            Assert.Same(builder, result);
            var route = builder
                .WithSelector((context, token) => Task.FromResult(true))
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();
            Assert.True(route.Flags.HasFlag(RouteFlags.Agentic));
        }

        [Fact]
        public void RouteBuilder_AsAgentic_False_ClearsAgenticFlag()
        {
            // Arrange
            var builder = RouteBuilder.Create()
                .AsAgentic();

            // Act
            var result = builder.AsAgentic(false);

            // Assert
            var route = builder
                .WithSelector((context, token) => Task.FromResult(true))
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();
            Assert.False(route.Flags.HasFlag(RouteFlags.Agentic));
        }

        [Fact]
        public void RouteBuilder_AsAgentic_CombinesWithOtherFlags()
        {
            // Arrange
            var builder = RouteBuilder.Create()
                .AsAgentic()
                .AsInvoke();

            // Act
            var route = builder
                .WithSelector((context, token) => Task.FromResult(true))
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();

            // Assert
            Assert.True(route.Flags.HasFlag(RouteFlags.Agentic));
            Assert.True(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        #endregion

        #region AsNonTerminal Tests

        [Fact]
        public void RouteBuilder_AsNonTerminal_SetsNonTerminalFlag()
        {
            // Arrange
            var builder = RouteBuilder.Create();

            // Act
            var result = builder.AsNonTerminal();

            // Assert
            Assert.Same(builder, result);
            var route = builder
                .WithSelector((context, token) => Task.FromResult(true))
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();
            Assert.True(route.Flags.HasFlag(RouteFlags.NonTerminal));
        }

        [Fact]
        public void RouteBuilder_AsNonTerminal_CombinesWithOtherFlags()
        {
            // Arrange
            var builder = RouteBuilder.Create()
                .AsNonTerminal()
                .AsInvoke();

            // Act
            var route = builder
                .WithSelector((context, token) => Task.FromResult(true))
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();

            // Assert
            Assert.True(route.Flags.HasFlag(RouteFlags.NonTerminal));
            Assert.True(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        #endregion

        #region WithOrderRank Tests

        [Fact]
        public void RouteBuilder_WithOrderRank_SetsRank()
        {
            // Arrange
            var builder = RouteBuilder.Create();
            ushort rank = 5;

            // Act
            var result = builder.WithOrderRank(rank);

            // Assert
            Assert.Same(builder, result);
            var route = builder
                .WithSelector((context, token) => Task.FromResult(true))
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();
            Assert.Equal(rank, route.Rank);
        }

        [Fact]
        public void RouteBuilder_WithOrderRank_ZeroRank_SetsRank()
        {
            // Arrange
            var builder = RouteBuilder.Create();

            // Act
            var result = builder.WithOrderRank(0);

            // Assert
            var route = builder
                .WithSelector((context, token) => Task.FromResult(true))
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();
            Assert.Equal((ushort)0, route.Rank);
        }

        [Fact]
        public void RouteBuilder_WithOrderRank_MaxValue_SetsRank()
        {
            // Arrange
            var builder = RouteBuilder.Create();

            // Act
            var result = builder.WithOrderRank(ushort.MaxValue);

            // Assert
            var route = builder
                .WithSelector((context, token) => Task.FromResult(true))
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();
            Assert.Equal(ushort.MaxValue, route.Rank);
        }

        #endregion

        #region Build Tests

        [Fact]
        public void RouteBuilder_Build_WithAllProperties_ReturnsRoute()
        {
            // Arrange
            RouteSelector selector = (context, token) => Task.FromResult(true);
            RouteHandler handler = (context, state, token) => Task.CompletedTask;
            var builder = RouteBuilder.Create()
                .WithSelector(selector)
                .WithHandler(handler)
                .WithChannelId(Channels.Msteams)
                .WithOrderRank(5)
                .AsInvoke()
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

        [Fact]
        public void RouteBuilder_Build_WithoutSelector_ThrowsArgumentNullException()
        {
            // Arrange
            var builder = RouteBuilder.Create()
                .WithHandler((context, state, token) => Task.CompletedTask);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.Build());
        }

        [Fact]
        public void RouteBuilder_Build_WithoutHandler_ThrowsArgumentNullException()
        {
            // Arrange
            var builder = RouteBuilder.Create()
                .WithSelector((context, token) => Task.FromResult(true));

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.Build());
        }

        [Fact]
        public void RouteBuilder_Build_MinimalConfiguration_ReturnsRoute()
        {
            // Arrange
            var builder = RouteBuilder.Create()
                .WithSelector((context, token) => Task.FromResult(true))
                .WithHandler((context, state, token) => Task.CompletedTask);

            // Act
            var route = builder.Build();

            // Assert
            Assert.NotNull(route);
            Assert.NotNull(route.Selector);
            Assert.NotNull(route.Handler);
            Assert.Null(route.ChannelId);
            Assert.Equal(RouteRank.Unspecified, route.Rank);
            Assert.Equal(RouteFlags.None, route.Flags);
        }

        #endregion

        #region IsContextMatch Tests

        [Fact]
        public void RouteBuilder_IsContextMatch_AgenticRoute_AgenticContext_ReturnsTrue()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount { Role = RoleTypes.AgenticUser }
            });

            var route = new Route
            {
                Flags = RouteFlags.Agentic,
                ChannelId = null
            };

            // Act
            var result = RouteBuilderBase<RouteBuilder>.IsContextMatch(mockContext.Object, route);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void RouteBuilder_IsContextMatch_AgenticRoute_NonAgenticContext_ReturnsFalse()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount { Role = RoleTypes.Agent }
            });

            var route = new Route
            {
                Flags = RouteFlags.Agentic,
                ChannelId = null
            };

            // Act
            var result = RouteBuilderBase<RouteBuilder>.IsContextMatch(mockContext.Object, route);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void RouteBuilder_IsContextMatch_NonAgenticRoute_ReturnsTrue()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount()
            });

            var route = new Route
            {
                Flags = RouteFlags.None,
                ChannelId = null
            };

            // Act
            var result = RouteBuilderBase<RouteBuilder>.IsContextMatch(mockContext.Object, route);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void RouteBuilder_IsContextMatch_MatchingChannelId_ReturnsTrue()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount()
            });

            var route = new Route
            {
                Flags = RouteFlags.None,
                ChannelId = Channels.Msteams
            };

            // Act
            var result = RouteBuilderBase<RouteBuilder>.IsContextMatch(mockContext.Object, route);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void RouteBuilder_IsContextMatch_NonMatchingChannelId_ReturnsFalse()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                ChannelId = Channels.Directline,
                Recipient = new ChannelAccount()
            });

            var route = new Route
            {
                Flags = RouteFlags.None,
                ChannelId = Channels.Msteams
            };

            // Act
            var result = RouteBuilderBase<RouteBuilder>.IsContextMatch(mockContext.Object, route);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void RouteBuilder_FluentAPI_AllMethodsChainCorrectly()
        {
            // Arrange & Act
            var route = RouteBuilder.Create()
                .WithSelector((context, token) => Task.FromResult(true))
                .WithHandler((context, state, token) => Task.CompletedTask)
                .WithChannelId(Channels.Msteams)
                .WithOrderRank(10)
                .AsInvoke()
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
        public async Task RouteBuilder_CompleteScenario_ExecutesCorrectly()
        {
            // Arrange
            var handlerExecuted = false;
            var selectorExecuted = false;

            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount { Role = RoleTypes.AgenticUser }
            });

            var route = RouteBuilder.Create()
                .WithSelector((context, token) =>
                {
                    selectorExecuted = true;
                    return Task.FromResult(true);
                })
                .WithHandler((context, state, token) =>
                {
                    handlerExecuted = true;
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
            Assert.True(selectorExecuted);
            Assert.True(handlerExecuted);
        }

        #endregion
    }
}