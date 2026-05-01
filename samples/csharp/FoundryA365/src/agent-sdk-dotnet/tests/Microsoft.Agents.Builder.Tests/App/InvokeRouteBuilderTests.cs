// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Moq;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class InvokeRouteBuilderTests
    {
        [Fact]
        public void Constructor_SetsInvokeFlag()
        {
            // Arrange & Act
            var builder = new InvokeRouteBuilder();
            var route = builder
                .WithName("testInvoke")
                .WithHandler((context, state, ct) => Task.CompletedTask)
                .Build();

            // Assert
            Assert.True(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        [Fact]
        public async Task WithName_String_ValidName_SetsSelector()
        {
            // Arrange
            var builder = new InvokeRouteBuilder();
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "testInvoke",
                ChannelId = "testChannel"
            };
            var mockContext = InvokeRouteBuilderTests.CreateMockTurnContext(activity);

            // Act
            var route = builder
                .WithName("testInvoke")
                .WithHandler((context, state, ct) => Task.CompletedTask)
                .Build();

            // Assert
            Assert.NotNull(route.Selector);
            var result = await route.Selector(mockContext.Object, CancellationToken.None);
            Assert.True(result);
        }

        [Fact]
        public async Task WithName_String_CaseInsensitive()
        {
            // Arrange
            var builder = new InvokeRouteBuilder();
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "TESTINVOKE",
                ChannelId = "testChannel"
            };
            var mockContext = InvokeRouteBuilderTests.CreateMockTurnContext(activity);

            // Act
            var route = builder
                .WithName("testinvoke")
                .WithHandler((context, state, ct) => Task.CompletedTask)
                .Build();

            // Assert
            var result = await route.Selector(mockContext.Object, CancellationToken.None);
            Assert.True(result);
        }

        [Fact]
        public async Task WithName_String_WrongName_DoesNotMatch()
        {
            // Arrange
            var builder = new InvokeRouteBuilder();
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "differentInvoke",
                ChannelId = "testChannel"
            };
            var mockContext = InvokeRouteBuilderTests.CreateMockTurnContext(activity);

            // Act
            var route = builder
                .WithName("testInvoke")
                .WithHandler((context, state, ct) => Task.CompletedTask)
                .Build();

            // Assert
            var result = await route.Selector(mockContext.Object, CancellationToken.None);
            Assert.False(result);
        }

        [Fact]
        public async Task WithName_String_WrongActivityType_DoesNotMatch()
        {
            // Arrange
            var builder = new InvokeRouteBuilder();
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Name = "testInvoke",
                ChannelId = "testChannel"
            };
            var mockContext = InvokeRouteBuilderTests.CreateMockTurnContext(activity);

            // Act
            var route = builder
                .WithName("testInvoke")
                .WithHandler((context, state, ct) => Task.CompletedTask)
                .Build();

            // Assert
            var result = await route.Selector(mockContext.Object, CancellationToken.None);
            Assert.False(result);
        }

        [Fact]
        public void WithName_String_NullName_ThrowsException()
        {
            // Arrange
            var builder = new InvokeRouteBuilder();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.WithName((string)null));
        }

        [Fact]
        public void WithName_String_WhitespaceName_ThrowsException()
        {
            // Arrange
            var builder = new InvokeRouteBuilder();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => builder.WithName("   "));
        }

        [Fact]
        public async Task WithName_Regex_ValidPattern_SetsSelector()
        {
            // Arrange
            var builder = new InvokeRouteBuilder();
            var pattern = new Regex("^test.*");
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "testInvoke123",
                ChannelId = "testChannel"
            };
            var mockContext = InvokeRouteBuilderTests.CreateMockTurnContext(activity);

            // Act
            var route = builder
                .WithName(pattern)
                .WithHandler((context, state, ct) => Task.CompletedTask)
                .Build();

            // Assert
            Assert.NotNull(route.Selector);
            var result = await route.Selector(mockContext.Object, CancellationToken.None);
            Assert.True(result);
        }

        [Fact]
        public async Task WithName_Regex_NoMatch_DoesNotMatch()
        {
            // Arrange
            var builder = new InvokeRouteBuilder();
            var pattern = new Regex("^test.*");
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "invoke123",
                ChannelId = "testChannel"
            };
            var mockContext = InvokeRouteBuilderTests.CreateMockTurnContext(activity);

            // Act
            var route = builder
                .WithName(pattern)
                .WithHandler((context, state, ct) => Task.CompletedTask)
                .Build();

            // Assert
            var result = await route.Selector(mockContext.Object, CancellationToken.None);
            Assert.False(result);
        }

        [Fact]
        public void WithName_Regex_NullPattern_ThrowsException()
        {
            // Arrange
            var builder = new InvokeRouteBuilder();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.WithName((Regex)null));
        }

        [Fact]
        public void WithName_Regex_AlreadyHasSelector_ThrowsWhenNameAlreadyDefined()
        {
            Assert.Throws<InvalidOperationException>(() => InvokeRouteBuilder.Create()
                .WithName("name")
                .WithName(new Regex("^second.*")));
        }

        [Fact]
        public void WithName_Name_DuplicateNameCriteria_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => InvokeRouteBuilder.Create()
                .WithName(new Regex("^second.*"))
                .WithName("name"));
        }

        [Fact]
        public async Task WithSelector_CustomSelector_SetsSelector()
        {
            // Arrange
            var builder = new InvokeRouteBuilder();
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "customInvoke",
                ChannelId = "testChannel"
            };
            var mockContext = InvokeRouteBuilderTests.CreateMockTurnContext(activity);
            RouteSelector customSelector = (context, ct) => Task.FromResult(context.Activity.Name == "customInvoke");

            // Act
            var route = builder
                .WithSelector(customSelector)
                .WithHandler((context, state, ct) => Task.CompletedTask)
                .Build();

            // Assert
            Assert.NotNull(route.Selector);
            var result = await route.Selector(mockContext.Object, CancellationToken.None);
            Assert.True(result);
        }

        [Fact]
        public async Task WithSelector_CustomSelector_EnforcesInvokeType()
        {
            // Arrange
            var builder = new InvokeRouteBuilder();
            var activity = new Activity
            {
                Type = ActivityTypes.Message, // Not invoke type
                Name = "customInvoke",
                ChannelId = "testChannel"
            };
            var mockContext = InvokeRouteBuilderTests.CreateMockTurnContext(activity);
            RouteSelector customSelector = (context, ct) => Task.FromResult(true); // Always returns true

            // Act
            var route = builder
                .WithSelector(customSelector)
                .WithHandler((context, state, ct) => Task.CompletedTask)
                .Build();

            // Assert
            var result = await route.Selector(mockContext.Object, CancellationToken.None);
            Assert.False(result); // Should be false because activity type is not Invoke
        }

        [Fact]
        public void WithSelector_AlreadyHasSelector_ThrowsException()
        {
            // Arrange
            var builder = new InvokeRouteBuilder()
                .WithSelector((context, ct) => Task.FromResult(true));

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => builder.WithSelector((context, ct) => Task.FromResult(true)));
        }

        [Fact]
        public async Task WithHandler_ValidHandler_SetsHandler()
        {
            // Arrange
            var builder = new InvokeRouteBuilder();
            var handlerCalled = false;
            RouteHandler handler = (context, state, ct) =>
            {
                handlerCalled = true;
                return Task.CompletedTask;
            };

            // Act
            var route = builder
                .WithName("testInvoke")
                .WithHandler(handler)
                .Build();

            // Assert
            Assert.NotNull(route.Handler);
            await route.Handler(null, null, CancellationToken.None);
            Assert.True(handlerCalled);
        }

        [Fact]
        public void WithHandler_NullHandler_AllowsNull()
        {
            // Arrange
            var builder = new InvokeRouteBuilder();

            // Act
            var result = builder.WithHandler(null);

            // Assert - Should not throw, but Build() will throw later
            Assert.NotNull(result);
        }

        [Fact]
        public void AsInvoke_True_KeepsInvokeFlag()
        {
            // Arrange
            var builder = new InvokeRouteBuilder();

            // Act
            var result = builder.AsInvoke(true);
            var route = result
                .WithName("testInvoke")
                .WithHandler((context, state, ct) => Task.CompletedTask)
                .Build();

            // Assert
            Assert.True(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        [Fact]
        public void AsInvoke_False_KeepsInvokeFlag()
        {
            // Arrange
            var builder = new InvokeRouteBuilder();

            // Act
            var result = builder.AsInvoke(false);
            var route = result
                .WithName("testInvoke")
                .WithHandler((context, state, ct) => Task.CompletedTask)
                .Build();

            // Assert - InvokeRouteBuilder should always have Invoke flag
            Assert.True(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        [Fact]
        public void AsInvoke_NoParameter_KeepsInvokeFlag()
        {
            // Arrange
            var builder = new InvokeRouteBuilder();

            // Act
            var result = builder.AsInvoke();
            var route = result
                .WithName("testInvoke")
                .WithHandler((context, state, ct) => Task.CompletedTask)
                .Build();

            // Assert
            Assert.True(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        [Fact]
        public async Task WithName_AgenticRequest_Matches()
        {
            // Arrange
            var builder = new InvokeRouteBuilder();
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "testInvoke",
                ChannelId = "testChannel",
                Recipient = new ChannelAccount { Role = RoleTypes.AgenticUser }
            };
            var mockContext = InvokeRouteBuilderTests.CreateMockTurnContext(activity);

            // Act
            var route = builder
                .AsAgentic()
                .WithName("testInvoke")
                .WithHandler((context, state, ct) => Task.CompletedTask)
                .Build();

            // Assert
            var result = await route.Selector(mockContext.Object, CancellationToken.None);
            Assert.True(result);
        }

        [Fact]
        public async Task WithName_NonAgenticRequest_WhenAgenticRequired_DoesNotMatch()
        {
            // Arrange
            var builder = new InvokeRouteBuilder();
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "testInvoke",
                ChannelId = "testChannel"
            };
            var mockContext = InvokeRouteBuilderTests.CreateMockTurnContext(activity);

            // Act
            var route = builder
                .AsAgentic()
                .WithName("testInvoke")
                .WithHandler((context, state, ct) => Task.CompletedTask)
                .Build();

            // Assert
            var result = await route.Selector(mockContext.Object, CancellationToken.None);
            Assert.False(result);
        }

        [Fact]
        public async Task WithName_ChannelIdFilter_Matches()
        {
            // Arrange
            var builder = new InvokeRouteBuilder();
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "testInvoke",
                ChannelId = "msteams"
            };
            var mockContext = InvokeRouteBuilderTests.CreateMockTurnContext(activity);

            // Act
            var route = builder
                .WithChannelId(new ChannelId("msteams"))
                .WithName("testInvoke")
                .WithHandler((context, state, ct) => Task.CompletedTask)
                .Build();

            // Assert
            var result = await route.Selector(mockContext.Object, CancellationToken.None);
            Assert.True(result);
        }

        [Fact]
        public async Task WithName_DifferentChannelId_DoesNotMatch()
        {
            // Arrange
            var builder = new InvokeRouteBuilder();
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "testInvoke",
                ChannelId = "webchat"
            };
            var mockContext = InvokeRouteBuilderTests.CreateMockTurnContext(activity);

            // Act
            var route = builder
                .WithChannelId(new ChannelId("msteams"))
                .WithName("testInvoke")
                .WithHandler((context, state, ct) => Task.CompletedTask)
                .Build();

            // Assert
            var result = await route.Selector(mockContext.Object, CancellationToken.None);
            Assert.False(result);
        }

        [Fact]
        public void Build_NoSelector_ThrowsException()
        {
            // Arrange
            var builder = new InvokeRouteBuilder()
                .WithHandler((context, state, ct) => Task.CompletedTask);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Fact]
        public void Build_NoHandler_ThrowsException()
        {
            // Arrange
            var builder = new InvokeRouteBuilder()
                .WithName("testInvoke");

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.Build());
        }

        [Fact]
        public void WithOrderRank_SetsRank()
        {
            // Arrange
            var builder = new InvokeRouteBuilder();
            ushort expectedRank = 100;

            // Act
            var route = builder
                .WithName("testInvoke")
                .WithHandler((context, state, ct) => Task.CompletedTask)
                .WithOrderRank(expectedRank)
                .Build();

            // Assert
            Assert.Equal(expectedRank, route.Rank);
        }

        [Fact]
        public async Task IntegrationTest_InvokeRouteInApplication()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "testInvoke",
                Recipient = new ChannelAccount { Id = "recipientId" },
                Conversation = new ConversationAccount { Id = "conversationId" },
                From = new ChannelAccount { Id = "fromId" },
                ChannelId = "channelId"
            };
            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, activity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            var handlerCalled = false;

            app.AddRoute(
                InvokeRouteBuilder.Create()
                    .WithName("testInvoke")
                    .WithHandler((context, state, ct) =>
                    {
                        handlerCalled = true;
                        return Task.CompletedTask;
                    })
                    .Build()
            );

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(handlerCalled);
        }

        [Fact]
        public async Task IntegrationTest_InvokeRouteWithRegex()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "adaptiveCard/action",
                Recipient = new ChannelAccount { Id = "recipientId" },
                Conversation = new ConversationAccount { Id = "conversationId" },
                From = new ChannelAccount { Id = "fromId" },
                ChannelId = "channelId"
            };
            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, activity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            var handlerCalled = false;

            app.AddRoute(
                InvokeRouteBuilder.Create()
                    .WithName(new Regex("^adaptiveCard/.*"))
                    .WithHandler((context, state, ct) =>
                    {
                        handlerCalled = true;
                        return Task.CompletedTask;
                    })
                    .Build()
            );

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(handlerCalled);
        }

        private static Mock<ITurnContext> CreateMockTurnContext(IActivity activity)
        {
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(activity);
            return mockContext;
        }
    }
}