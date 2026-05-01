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
    public class TypeRouteBuilderTests
    {
        #region TypeRouteBuilder.Create Tests

        [Fact]
        public void TypeRouteBuilder_Create_ReturnsNewInstance()
        {
            // Act
            var builder = TypeRouteBuilder.Create();

            // Assert
            Assert.NotNull(builder);
            Assert.IsType<TypeRouteBuilder>(builder);
        }

        #endregion

        #region WithType(string) Tests

        [Fact]
        public void TypeRouteBuilder_WithType_String_SetsSelector()
        {
            // Arrange
            var builder = TypeRouteBuilder.Create();
            var type = "myType";

            // Act
            var result = builder.WithType(type);

            // Assert
            Assert.Same(builder, result);
            var route = builder
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();
            Assert.NotNull(route.Selector);
        }

        [Fact]
        public void TypeRouteBuilder_WithType_String_NullType_ThrowsArgumentNullException()
        {
            // Arrange
            var builder = TypeRouteBuilder.Create();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.WithType((string)null));
        }

        [Fact]
        public void TypeRouteBuilder_WithType_String_EmptyType_ThrowsArgumentException()
        {
            // Arrange
            var builder = TypeRouteBuilder.Create();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => builder.WithType(string.Empty));
        }

        [Fact]
        public void TypeRouteBuilder_WithType_String_WhitespaceType_ThrowsArgumentException()
        {
            // Arrange
            var builder = TypeRouteBuilder.Create();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => builder.WithType("   "));
        }

        [Fact]
        public async Task TypeRouteBuilder_WithType_String_MatchesTypeCaseInsensitive()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = "MYTYPE"
            });

            var builder = TypeRouteBuilder.Create()
                .WithType("myType")
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task TypeRouteBuilder_WithType_String_DoesNotMatchDifferentType()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = "differentType"
            });

            var builder = TypeRouteBuilder.Create()
                .WithType("myType")
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void TypeRouteBuilder_WithType_String_ThrowsWhenSelectorAlreadyDefined()
        {
            // Arrange
            var builder = TypeRouteBuilder.Create()
                .WithType("myType")
                .WithSelector((ctx, ct) => Task.FromResult(true));

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => builder.WithSelector((ctx, ct) => Task.FromResult(true)));
            Assert.Contains("TypeRouteBuilder.WithSelector()", ex.Message);
        }

        [Fact]
        public async Task TypeRouteBuilder_WithType_String_HandlesNullActivityType()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = null
            });

            var builder = TypeRouteBuilder.Create()
                .WithType("myType")
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task TypeRouteBuilder_WithType_String_MatchesInvokeType()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke
            });

            var builder = TypeRouteBuilder.Create()
                .WithType(ActivityTypes.Invoke)
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.True(result);
        }

        #endregion

        #region WithType(Regex) Tests

        [Fact]
        public void TypeRouteBuilder_WithType_Regex_SetsSelector()
        {
            // Arrange
            var builder = TypeRouteBuilder.Create();
            var pattern = new Regex("myType");

            // Act
            var result = builder.WithType(pattern);

            // Assert
            Assert.Same(builder, result);
            var route = builder
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();
            Assert.NotNull(route.Selector);
        }

        [Fact]
        public void TypeRouteBuilder_WithType_Regex_NullPattern_ThrowsArgumentNullException()
        {
            // Arrange
            var builder = TypeRouteBuilder.Create();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.WithType((Regex)null));
        }

        [Fact]
        public async Task TypeRouteBuilder_WithType_Regex_MatchesPattern()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = "invoke/custom"
            });

            var builder = TypeRouteBuilder.Create()
                .WithType(new Regex(@"invoke/.+"))
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task TypeRouteBuilder_WithType_Regex_DoesNotMatchNonPattern()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = "otherType"
            });

            var builder = TypeRouteBuilder.Create()
                .WithType(new Regex(@"invoke/.+"))
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task TypeRouteBuilder_WithType_Regex_HandlesNullActivityType()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = null
            });

            var builder = TypeRouteBuilder.Create()
                .WithType(new Regex("myType"))
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act & Assert - Should not throw
            var result = await route.Selector(mockContext.Object, CancellationToken.None);
            Assert.False(result);
        }

        [Fact]
        public async Task TypeRouteBuilder_WithType_Regex_ComplexPattern()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = "invoke/adaptiveCard/123-abc"
            });

            var builder = TypeRouteBuilder.Create()
                .WithType(new Regex(@"^invoke/adaptiveCard/[\w\-]+$"))
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.True(result);
        }

        #endregion

        #region WithSelector Tests

        [Fact]
        public void TypeRouteBuilder_WithSelector_SetsSelector()
        {
            // Arrange
            var builder = TypeRouteBuilder.Create();
            RouteSelector selector = (context, token) => Task.FromResult(true);

            // Act
            var result = builder.WithSelector(selector);

            // Assert
            Assert.Same(builder, result);
        }

        [Fact]
        public void TypeRouteBuilder_WithSelector_NullSelector_ThrowsArgumentNullException()
        {
            // Arrange
            var builder = TypeRouteBuilder.Create();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.WithSelector(null));
        }

        [Fact]
        public async Task TypeRouteBuilder_WithSelector_WrapsWithIsContextMatch()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = "customType"
            });

            var selectorCalled = false;
            RouteSelector selector = (context, token) =>
            {
                selectorCalled = true;
                return Task.FromResult(true);
            };

            var builder = TypeRouteBuilder.Create()
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
        public void TypeRouteBuilder_WithType_ThrowsWhenTypeAlreadyDefined()
        {
            Assert.Throws<InvalidOperationException>(() => TypeRouteBuilder.Create()
                .WithType("myType")
                .WithType(new Regex("pattern")));
        }

        [Fact]
        public void TypeRouteBuilder_WithType_ThrowsWhenTypeRegexAlreadyDefined()
        {
            Assert.Throws<InvalidOperationException>(() => TypeRouteBuilder.Create()
                .WithType(new Regex("pattern"))
                .WithType("myType"));
        }

        [Fact]
        public async Task TypeRouteBuilder_WithSelector_CustomLogic_ExecutesCorrectly()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "adaptiveCard/action",
                Value = new { action = "submit" }
            });

            RouteSelector selector = (context, token) =>
            {
                var hasValue = context.Activity.Type == ActivityTypes.Invoke && context.Activity.Value != null;
                return Task.FromResult(hasValue);
            };

            var builder = TypeRouteBuilder.Create()
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
        public void TypeRouteBuilder_WithHandler_SetsHandler()
        {
            // Arrange
            var builder = TypeRouteBuilder.Create();
            RouteHandler handler = (context, state, token) => Task.CompletedTask;

            // Act
            var result = builder.WithHandler(handler);

            // Assert
            Assert.Same(builder, result);
            var route = builder
                .WithType("test")
                .Build();
            Assert.Same(handler, route.Handler);
        }

        [Fact]
        public void TypeRouteBuilder_WithHandler_NullHandler_ThrowsArgumentNullException()
        {
            // Arrange
            var builder = TypeRouteBuilder.Create();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.WithHandler(null));
        }

        #endregion

        #region AsInvoke Tests

        [Fact]
        public void TypeRouteBuilder_AsInvoke_SetsInvokeFlag()
        {
            // Arrange
            var builder = TypeRouteBuilder.Create();

            // Act
            var result = builder.AsInvoke();

            // Assert
            Assert.Same(builder, result);
            var route = builder
                .WithType(ActivityTypes.Invoke)
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();

            Assert.True(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        [Fact]
        public void TypeRouteBuilder_AsInvoke_False_ClearsInvokeFlag()
        {
            // Arrange
            var builder = TypeRouteBuilder.Create()
                .AsInvoke();

            // Act
            var result = builder.AsInvoke(false);

            // Assert
            var route = builder
                .WithType("test")
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();
            Assert.False(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        [Fact]
        public void TypeRouteBuilder_AsInvoke_True_SetsFlag()
        {
            // Arrange
            var builder = TypeRouteBuilder.Create()
                .AsInvoke(true);

            // Act
            var route = builder
                .WithType(ActivityTypes.Invoke)
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();

            // Assert
            Assert.True(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        #endregion

        #region Agentic and ChannelId Tests

        [Fact]
        public async Task TypeRouteBuilder_WithType_RespectsAgenticFlag()
        {
            // Arrange - Agentic context
            var agenticContext = new Mock<ITurnContext>();
            agenticContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = "myType",
                Recipient = new ChannelAccount { Role = RoleTypes.AgenticUser }
            });

            // Arrange - Non-agentic context
            var nonAgenticContext = new Mock<ITurnContext>();
            nonAgenticContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = "myType"
            });

            var builder = TypeRouteBuilder.Create()
                .WithType("myType")
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
        public async Task TypeRouteBuilder_WithType_RespectsChannelId()
        {
            // Arrange - Matching channel
            var matchingContext = new Mock<ITurnContext>();
            matchingContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = "myType",
                ChannelId = Channels.Msteams
            });

            // Arrange - Non-matching channel
            var nonMatchingContext = new Mock<ITurnContext>();
            nonMatchingContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = "myType",
                ChannelId = Channels.Directline
            });

            var builder = TypeRouteBuilder.Create()
                .WithType("myType")
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
        public async Task TypeRouteBuilder_WithSelector_RespectsAgenticFlag()
        {
            // Arrange - Agentic context
            var agenticContext = new Mock<ITurnContext>();
            agenticContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = "myType",
                Recipient = new ChannelAccount { Role = RoleTypes.AgenticUser }
            });

            // Arrange - Non-agentic context
            var nonAgenticContext = new Mock<ITurnContext>();
            nonAgenticContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = "myType"
            });

            var builder = TypeRouteBuilder.Create()
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
        public async Task TypeRouteBuilder_WithSelector_RespectsChannelId()
        {
            // Arrange - Matching channel
            var matchingContext = new Mock<ITurnContext>();
            matchingContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = "myType",
                ChannelId = Channels.Msteams
            });

            // Arrange - Non-matching channel
            var nonMatchingContext = new Mock<ITurnContext>();
            nonMatchingContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = "myType",
                ChannelId = Channels.Directline
            });

            var builder = TypeRouteBuilder.Create()
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
        public void TypeRouteBuilder_FluentAPI_AllMethodsChainCorrectly()
        {
            // Arrange & Act
            var route = TypeRouteBuilder.Create()
                .WithType(ActivityTypes.Invoke)
                .WithHandler((context, state, token) => Task.CompletedTask)
                .WithChannelId(Channels.Msteams)
                .WithOrderRank(10)
                .AsAgentic()
                .AsNonTerminal()
                .WithOAuthHandlers("handler1,handler2")
                .AsInvoke()
                .Build();

            // Assert
            Assert.NotNull(route);
            Assert.Equal(Channels.Msteams, route.ChannelId);
            Assert.Equal((ushort)10, route.Rank);
            Assert.True(route.Flags.HasFlag(RouteFlags.Agentic));
            Assert.True(route.Flags.HasFlag(RouteFlags.NonTerminal));
            Assert.True(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        [Fact]
        public async Task TypeRouteBuilder_CompleteScenario_StringType_ExecutesCorrectly()
        {
            // Arrange
            var handlerExecuted = false;

            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount { Role = RoleTypes.AgenticUser }
            });

            var route = TypeRouteBuilder.Create()
                .WithType(ActivityTypes.Invoke)
                .WithHandler((context, state, token) =>
                {
                    handlerExecuted = true;
                    return Task.CompletedTask;
                })
                .WithChannelId(Channels.Msteams)
                .AsAgentic()
                .AsInvoke()
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
        public async Task TypeRouteBuilder_CompleteScenario_RegexType_ExecutesCorrectly()
        {
            // Arrange
            var handlerExecuted = false;

            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = "invoke/adaptiveCard",
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount { Role = RoleTypes.AgenticUser }
            });

            var route = TypeRouteBuilder.Create()
                .WithType(new Regex(@"invoke/.+"))
                .WithHandler((context, state, token) =>
                {
                    handlerExecuted = true;
                    return Task.CompletedTask;
                })
                .WithChannelId(Channels.Msteams)
                .AsAgentic()
                .AsInvoke()
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
        public async Task TypeRouteBuilder_CompleteScenario_CustomSelector_ExecutesCorrectly()
        {
            // Arrange
            var handlerExecuted = false;

            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "adaptiveCard/action",
                ChannelId = Channels.Msteams,
                Value = new { action = "submit" }
            });

            var route = TypeRouteBuilder.Create()
                .WithSelector((context, token) =>
                {
                    var hasValue = context.Activity.Type == ActivityTypes.Invoke && context.Activity.Value != null;
                    return Task.FromResult(hasValue);
                })
                .WithHandler((context, state, token) =>
                {
                    handlerExecuted = true;
                    return Task.CompletedTask;
                })
                .WithChannelId(Channels.Msteams)
                .AsInvoke()
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
        public void TypeRouteBuilder_Build_WithoutSelector_ThrowsException()
        {
            // Arrange
            var builder = TypeRouteBuilder.Create()
                .WithHandler((context, state, token) => Task.CompletedTask);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Fact]
        public void TypeRouteBuilder_Build_WithoutHandler_ThrowsArgumentNullException()
        {
            // Arrange
            var builder = TypeRouteBuilder.Create()
                .WithType("myType");

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.Build());
        }

        [Fact]
        public void TypeRouteBuilder_Build_MinimalConfiguration_ReturnsRoute()
        {
            // Arrange
            var builder = TypeRouteBuilder.Create()
                .WithType("myType")
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
        public async Task TypeRouteBuilder_WithType_Regex_CaseSensitivePattern()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = "Invoke"
            });

            var builder = TypeRouteBuilder.Create()
                .WithType(new Regex(@"^invoke$", RegexOptions.None)) // Case sensitive
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert - Should not match due to case sensitivity
            Assert.False(result);
        }

        [Fact]
        public async Task TypeRouteBuilder_MultipleConditions_AllMustMatch()
        {
            // Arrange - All conditions match
            var matchingContext = new Mock<ITurnContext>();
            matchingContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount { Role = RoleTypes.AgenticUser }
            });

            // Arrange - Wrong type
            var wrongTypeContext = new Mock<ITurnContext>();
            wrongTypeContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount { Role = RoleTypes.AgenticUser }
            });

            var route = TypeRouteBuilder.Create()
                .WithType(ActivityTypes.Invoke)
                .WithChannelId(Channels.Msteams)
                .AsAgentic()
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();

            // Act
            var matchingResult = await route.Selector(matchingContext.Object, CancellationToken.None);
            var wrongTypeResult = await route.Selector(wrongTypeContext.Object, CancellationToken.None);

            // Assert
            Assert.True(matchingResult);
            Assert.False(wrongTypeResult);
        }

        [Fact]
        public async Task TypeRouteBuilder_WithType_String_HandlesEmptyActivityType()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = string.Empty
            });

            var builder = TypeRouteBuilder.Create()
                .WithType("myType")
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task TypeRouteBuilder_WithSelector_BlocksNonAgenticRequest_WhenAgenticFlagSet()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount { Id = "bot" } // No agentic role
            });

            var selectorCalled = false;
            RouteSelector selector = (context, token) =>
            {
                selectorCalled = true;
                return Task.FromResult(true);
            };

            var builder = TypeRouteBuilder.Create()
                .WithSelector(selector)
                .WithHandler((context, state, token) => Task.CompletedTask)
                .AsAgentic();

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.False(result);
            Assert.False(selectorCalled); // Should not reach the selector
        }

        #endregion

        #region Invoke-Specific Tests

        [Fact]
        public async Task TypeRouteBuilder_InvokeType_WithAsInvokeFlag_MatchesCorrectly()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "adaptiveCard/action"
            });

            var route = TypeRouteBuilder.Create()
                .WithType(ActivityTypes.Invoke)
                .AsInvoke()
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.True(result);
            Assert.True(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        [Fact]
        public async Task TypeRouteBuilder_NonInvokeType_WithAsInvokeFlag_StillSetsFlag()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Message
            });

            var route = TypeRouteBuilder.Create()
                .WithType(ActivityTypes.Message)
                .AsInvoke()
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.True(result); // Matches the type
            Assert.True(route.Flags.HasFlag(RouteFlags.Invoke)); // Flag is still set
        }

        #endregion
    }
}