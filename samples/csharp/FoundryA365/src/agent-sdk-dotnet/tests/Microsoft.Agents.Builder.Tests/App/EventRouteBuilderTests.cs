// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Moq;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class EventRouteBuilderTests
    {
        #region EventRouteBuilder.Create Tests

        [Fact]
        public void EventRouteBuilder_Create_ReturnsNewInstance()
        {
            // Act
            var builder = EventRouteBuilder.Create();

            // Assert
            Assert.NotNull(builder);
            Assert.IsType<EventRouteBuilder>(builder);
        }

        #endregion

        #region WithName(string) Tests

        [Fact]
        public void EventRouteBuilder_WithName_String_SetsSelector()
        {
            // Arrange
            var builder = EventRouteBuilder.Create();
            var name = "myEvent";

            // Act
            var result = builder.WithName(name);

            // Assert
            Assert.Same(builder, result);
            var route = builder
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();
            Assert.NotNull(route.Selector);
        }

        [Fact]
        public void EventRouteBuilder_WithName_String_NullName_ThrowsArgumentNullException()
        {
            // Arrange
            var builder = EventRouteBuilder.Create();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.WithName((string)null));
        }

        [Fact]
        public void EventRouteBuilder_WithName_String_EmptyName_ThrowsArgumentException()
        {
            // Arrange
            var builder = EventRouteBuilder.Create();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => builder.WithName(string.Empty));
        }

        [Fact]
        public void EventRouteBuilder_WithName_String_WhitespaceName_ThrowsArgumentException()
        {
            // Arrange
            var builder = EventRouteBuilder.Create();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => builder.WithName("   "));
        }

        [Fact]
        public async Task EventRouteBuilder_WithName_String_MatchesNameCaseInsensitive()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Event,
                Name = "MYEVENT"
            });

            var builder = EventRouteBuilder.Create()
                .WithName("myEvent")
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task EventRouteBuilder_WithName_String_DoesNotMatchDifferentName()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Event,
                Name = "differentEvent"
            });

            var builder = EventRouteBuilder.Create()
                .WithName("myEvent")
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task EventRouteBuilder_WithName_String_DoesNotMatchNonEventActivity()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Message,
                Name = "myEvent"
            });

            var builder = EventRouteBuilder.Create()
                .WithName("myEvent")
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task EventRouteBuilder_WithName_String_HandlesNullActivityName()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Event,
                Name = null
            });

            var builder = EventRouteBuilder.Create()
                .WithName("myEvent")
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region WithName(Regex) Tests

        [Fact]
        public void EventRouteBuilder_WithName_Regex_SetsSelector()
        {
            // Arrange
            var builder = EventRouteBuilder.Create();
            var pattern = new Regex("myEvent");

            // Act
            var result = builder.WithName(pattern);

            // Assert
            Assert.Same(builder, result);
            var route = builder
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();
            Assert.NotNull(route.Selector);
        }

        [Fact]
        public void EventRouteBuilder_WithName_Regex_NullPattern_ThrowsArgumentNullException()
        {
            // Arrange
            var builder = EventRouteBuilder.Create();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.WithName((Regex)null));
        }

        [Fact]
        public async Task EventRouteBuilder_WithName_Regex_MatchesPattern()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Event,
                Name = "tokens/response"
            });

            var builder = EventRouteBuilder.Create()
                .WithName(new Regex(@"tokens/.+"))
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task EventRouteBuilder_WithName_Regex_DoesNotMatchNonPattern()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Event,
                Name = "otherEvent"
            });

            var builder = EventRouteBuilder.Create()
                .WithName(new Regex(@"tokens/.+"))
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task EventRouteBuilder_WithName_Regex_DoesNotMatchNonEventActivity()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "tokens/response"
            });

            var builder = EventRouteBuilder.Create()
                .WithName(new Regex("tokens/.+"))
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void EventRouteBuilder_WithName_Regex_ThrowsWhenNameAlreadyDefined()
        {
            Assert.Throws<InvalidOperationException>(() => EventRouteBuilder.Create()
                .WithName("name")
                .WithName(new Regex("^second.*")));
        }

        [Fact]
        public void EventRouteBuilder_WithName_Name_ThrowsWhenRegexAlreadyDefined()
        {
            Assert.Throws<InvalidOperationException>(() => EventRouteBuilder.Create()
                .WithName(new Regex("^second.*"))
                .WithName("name"));
        }

        [Fact]
        public async Task EventRouteBuilder_WithName_Regex_HandlesNullActivityName()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Event,
                Name = null
            });

            var builder = EventRouteBuilder.Create()
                .WithName(new Regex("myEvent"))
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act & Assert - Should not throw
            var result = await route.Selector(mockContext.Object, CancellationToken.None);
            Assert.False(result);
        }

        #endregion

        #region WithSelector Tests

        [Fact]
        public void EventRouteBuilder_WithSelector_SetsSelector()
        {
            // Arrange
            var builder = EventRouteBuilder.Create();
            RouteSelector selector = (context, token) => Task.FromResult(true);

            // Act
            var result = builder.WithSelector(selector);

            // Assert
            Assert.Same(builder, result);
        }

        [Fact]
        public void EventRouteBuilder_WithSelector_NullSelector_ThrowsArgumentNullException()
        {
            // Arrange
            var builder = EventRouteBuilder.Create();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.WithSelector(null));
        }

        [Fact]
        public async Task EventRouteBuilder_WithSelector_WrapsWithEventTypeCheck()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Event,
                Name = "customEvent"
            });

            var selectorCalled = false;
            RouteSelector selector = (context, token) =>
            {
                selectorCalled = true;
                return Task.FromResult(true);
            };

            var builder = EventRouteBuilder.Create()
                .WithSelector(selector)
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.True(result);
            Assert.True(selectorCalled);
        }

        [Fact]
        public async Task EventRouteBuilder_WithSelector_BlocksNonEventActivity()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Message
            });

            var selectorCalled = false;
            RouteSelector selector = (context, token) =>
            {
                selectorCalled = true;
                return Task.FromResult(true);
            };

            var builder = EventRouteBuilder.Create()
                .WithSelector(selector)
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.False(result);
            Assert.False(selectorCalled); // Selector should not be called
        }

        [Fact]
        public void EventRouteBuilder_WithSelector_ThrowsWhenSelectorAlreadyDefined()
        {
            // Arrange
            var builder = EventRouteBuilder.Create()
                .WithSelector((context, token) => Task.FromResult(true));

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => builder.WithSelector((context, token) => Task.FromResult(false)));
        }

        [Fact]
        public async Task EventRouteBuilder_WithSelector_CustomLogic_ExecutesCorrectly()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Event,
                Name = "tokens/response",
                Value = new { token = "abc123" }
            });

            RouteSelector selector = (context, token) =>
            {
                var hasValue = context.Activity.Value != null;
                return Task.FromResult(hasValue);
            };

            var builder = EventRouteBuilder.Create()
                .WithSelector(selector)
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.True(result);
        }

        #endregion

        #region WithHandler Tests

        [Fact]
        public void EventRouteBuilder_WithHandler_SetsHandler()
        {
            // Arrange
            var builder = EventRouteBuilder.Create();
            RouteHandler handler = (context, state, token) => Task.CompletedTask;

            // Act
            var result = builder.WithHandler(handler);

            // Assert
            Assert.Same(builder, result);
            var route = builder
                .WithName("test")
                .Build();
            Assert.Same(handler, route.Handler);
        }

        [Fact]
        public void EventRouteBuilder_WithHandler_NullHandler_ThrowsArgumentNullException()
        {
            // Arrange
            var builder = EventRouteBuilder.Create();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.WithHandler(null));
        }

        #endregion

        #region AsInvoke Tests

        [Fact]
        public void EventRouteBuilder_AsInvoke_DoesNotSetInvokeFlag()
        {
            // Arrange
            var builder = EventRouteBuilder.Create();

            // Act
            var result = builder.AsInvoke();

            // Assert
            Assert.Same(builder, result);
            var route = builder
                .WithName("test")
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();

            // AsInvoke is ignored for event routes
            Assert.False(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        [Fact]
        public void EventRouteBuilder_AsInvoke_False_DoesNothing()
        {
            // Arrange
            var builder = EventRouteBuilder.Create()
                .AsInvoke();

            // Act
            var result = builder.AsInvoke(false);

            // Assert
            var route = builder
                .WithName("test")
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();
            Assert.False(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        [Fact]
        public void EventRouteBuilder_AsInvoke_True_StillDoesNotSetFlag()
        {
            // Arrange
            var builder = EventRouteBuilder.Create()
                .AsInvoke(true);

            // Act
            var route = builder
                .WithName("test")
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();

            // Assert
            Assert.False(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        #endregion

        #region Agentic and ChannelId Tests

        [Fact]
        public async Task EventRouteBuilder_WithName_RespectsAgenticFlag()
        {
            // Arrange - Agentic context
            var agenticContext = new Mock<ITurnContext>();
            agenticContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Event,
                Name = "myEvent",
                Recipient = new ChannelAccount { Role = RoleTypes.AgenticUser }
            });

            // Arrange - Non-agentic context
            var nonAgenticContext = new Mock<ITurnContext>();
            nonAgenticContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Event,
                Name = "myEvent"
            });

            var builder = EventRouteBuilder.Create()
                .WithName("myEvent")
                .AsAgentic()
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var agenticResult = await route.Selector(agenticContext.Object, CancellationToken.None);
            var nonAgenticResult = await route.Selector(nonAgenticContext.Object, CancellationToken.None);

            // Assert
            Assert.True(agenticResult);
            Assert.False(nonAgenticResult);
        }

        [Fact]
        public async Task EventRouteBuilder_WithName_RespectsChannelId()
        {
            // Arrange - Matching channel
            var matchingContext = new Mock<ITurnContext>();
            matchingContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Event,
                Name = "myEvent",
                ChannelId = Channels.Msteams
            });

            // Arrange - Non-matching channel
            var nonMatchingContext = new Mock<ITurnContext>();
            nonMatchingContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Event,
                Name = "myEvent",
                ChannelId = Channels.Directline
            });

            var builder = EventRouteBuilder.Create()
                .WithName("myEvent")
                .WithChannelId(Channels.Msteams)
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var matchingResult = await route.Selector(matchingContext.Object, CancellationToken.None);
            var nonMatchingResult = await route.Selector(nonMatchingContext.Object, CancellationToken.None);

            // Assert
            Assert.True(matchingResult);
            Assert.False(nonMatchingResult);
        }

        [Fact]
        public async Task EventRouteBuilder_WithSelector_RespectsAgenticFlag()
        {
            // Arrange - Agentic context
            var agenticContext = new Mock<ITurnContext>();
            agenticContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Event,
                Name = "myEvent",
                Recipient = new ChannelAccount { Role = RoleTypes.AgenticUser }
            });

            // Arrange - Non-agentic context
            var nonAgenticContext = new Mock<ITurnContext>();
            nonAgenticContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Event,
                Name = "myEvent"
            });

            var builder = EventRouteBuilder.Create()
                .WithSelector((ctx, ct) => Task.FromResult(true))
                .AsAgentic()
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var agenticResult = await route.Selector(agenticContext.Object, CancellationToken.None);
            var nonAgenticResult = await route.Selector(nonAgenticContext.Object, CancellationToken.None);

            // Assert
            Assert.True(agenticResult);
            Assert.False(nonAgenticResult);
        }

        [Fact]
        public async Task EventRouteBuilder_WithSelector_RespectsChannelId()
        {
            // Arrange - Matching channel
            var matchingContext = new Mock<ITurnContext>();
            matchingContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Event,
                Name = "myEvent",
                ChannelId = Channels.Msteams
            });

            // Arrange - Non-matching channel
            var nonMatchingContext = new Mock<ITurnContext>();
            nonMatchingContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Event,
                Name = "myEvent",
                ChannelId = Channels.Directline
            });

            var builder = EventRouteBuilder.Create()
                .WithSelector((ctx, ct) => Task.FromResult(true))
                .WithChannelId(Channels.Msteams)
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var matchingResult = await route.Selector(matchingContext.Object, CancellationToken.None);
            var nonMatchingResult = await route.Selector(nonMatchingContext.Object, CancellationToken.None);

            // Assert
            Assert.True(matchingResult);
            Assert.False(nonMatchingResult);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void EventRouteBuilder_FluentAPI_AllMethodsChainCorrectly()
        {
            // Arrange & Act
            var route = EventRouteBuilder.Create()
                .WithName("myEvent")
                .WithHandler((context, state, token) => Task.CompletedTask)
                .WithChannelId(Channels.Msteams)
                .WithOrderRank(10)
                .AsAgentic()
                .AsNonTerminal()
                .WithOAuthHandlers("handler1,handler2")
                .AsInvoke() // Should be ignored
                .Build();

            // Assert
            Assert.NotNull(route);
            Assert.Equal(Channels.Msteams, route.ChannelId);
            Assert.Equal((ushort)10, route.Rank);
            Assert.True(route.Flags.HasFlag(RouteFlags.Agentic));
            Assert.True(route.Flags.HasFlag(RouteFlags.NonTerminal));
            Assert.False(route.Flags.HasFlag(RouteFlags.Invoke)); // Should not be set for events
        }

        [Fact]
        public async Task EventRouteBuilder_CompleteScenario_StringName_ExecutesCorrectly()
        {
            // Arrange
            var handlerExecuted = false;

            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Event,
                Name = "tokens/response",
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount { Role = RoleTypes.AgenticUser }
            });

            var route = EventRouteBuilder.Create()
                .WithName("tokens/response")
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
            Assert.True(selectorResult);
            Assert.True(handlerExecuted);
        }

        [Fact]
        public async Task EventRouteBuilder_CompleteScenario_RegexName_ExecutesCorrectly()
        {
            // Arrange
            var handlerExecuted = false;

            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Event,
                Name = "tokens/response",
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount { Role = RoleTypes.AgenticUser }
            });

            var route = EventRouteBuilder.Create()
                .WithName(new Regex(@"tokens/.+"))
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
            Assert.True(selectorResult);
            Assert.True(handlerExecuted);
        }

        [Fact]
        public async Task EventRouteBuilder_CompleteScenario_CustomSelector_ExecutesCorrectly()
        {
            // Arrange
            var handlerExecuted = false;

            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Event,
                Name = "tokens/response",
                ChannelId = Channels.Msteams,
                Value = new { token = "abc123" }
            });

            var route = EventRouteBuilder.Create()
                .WithSelector((context, token) =>
                {
                    var hasTokenValue = context.Activity.Name == "tokens/response" && context.Activity.Value != null;
                    return Task.FromResult(hasTokenValue);
                })
                .WithHandler((context, state, token) =>
                {
                    handlerExecuted = true;
                    return Task.CompletedTask;
                })
                .WithChannelId(Channels.Msteams)
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
        }

        [Fact]
        public void EventRouteBuilder_Build_WithoutSelector_ThrowsInvalidOperationException()
        {
            // Arrange
            var builder = EventRouteBuilder.Create()
                .WithHandler((context, state, token) => Task.CompletedTask);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Fact]
        public void EventRouteBuilder_Build_WithoutHandler_ThrowsArgumentNullException()
        {
            // Arrange
            var builder = EventRouteBuilder.Create()
                .WithName("myEvent");

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.Build());
        }

        [Fact]
        public void EventRouteBuilder_Build_MinimalConfiguration_ReturnsRoute()
        {
            // Arrange
            var builder = EventRouteBuilder.Create()
                .WithName("myEvent")
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

        #region Edge Case Tests

        [Fact]
        public async Task EventRouteBuilder_WithName_String_TrimsAndMatches()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Event,
                Name = "  myEvent  " // Activity name with spaces (unlikely but possible)
            });

            var builder = EventRouteBuilder.Create()
                .WithName("myEvent")
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert - Case insensitive comparison should not match trimmed strings
            Assert.False(result);
        }

        [Fact]
        public async Task EventRouteBuilder_WithName_Regex_ComplexPattern()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Event,
                Name = "continueConversation/123-abc"
            });

            var builder = EventRouteBuilder.Create()
                .WithName(new Regex(@"^continueConversation/[\w\-]+$"))
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task EventRouteBuilder_MultipleConditions_AllMustMatch()
        {
            // Arrange - All conditions match
            var matchingContext = new Mock<ITurnContext>();
            matchingContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Event,
                Name = "myEvent",
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount { Role = RoleTypes.AgenticUser }
            });

            // Arrange - Wrong name
            var wrongNameContext = new Mock<ITurnContext>();
            wrongNameContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Event,
                Name = "wrongEvent",
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount { Role = RoleTypes.AgenticUser }
            });

            var route = EventRouteBuilder.Create()
                .WithName("myEvent")
                .WithChannelId(Channels.Msteams)
                .AsAgentic()
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();

            // Act
            var matchingResult = await route.Selector(matchingContext.Object, CancellationToken.None);
            var wrongNameResult = await route.Selector(wrongNameContext.Object, CancellationToken.None);

            // Assert
            Assert.True(matchingResult);
            Assert.False(wrongNameResult);
        }

        #endregion
    }
}