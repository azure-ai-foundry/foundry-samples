// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class ApplicationRouteTests
    {
        [Fact]
        public async Task Test_RouteList_RouteRank()
        {
            List<string> values = [];
            RouteList routes = new();

            routes.AddRoute(
                RouteBuilder.Create()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("1"); return Task.CompletedTask; })
                    .WithOrderRank(2)
                    .Build()
            );

            routes.AddRoute(
                RouteBuilder.Create()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("2"); return Task.CompletedTask; })
                    .WithOrderRank(0)
                    .Build()
            );

            routes.AddRoute(
                RouteBuilder.Create()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("3"); return Task.CompletedTask; })
                    .WithOrderRank(1)
                    .Build()
            );

            routes.AddRoute(
                RouteBuilder.Create()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("4"); return Task.CompletedTask; })
                    .WithOrderRank(1)
                    .Build()
            );

            foreach ( var route in routes.Enumerate())
            {
                await route.Handler(null, null, CancellationToken.None);
            }

            Assert.Equal(4, values.Count);
            Assert.Equal("2", values[0]);
            Assert.Equal("3", values[1]);
            Assert.Equal("4", values[2]);
            Assert.Equal("1", values[3]);
        }

        [Fact]
        public void Test_RouteList_Count_Empty()
        {
            RouteList routes = new();
            Assert.Equal(0, routes.FormatRouteList().Count);
        }

        [Fact]
        public void Test_RouteList_Count_WithRoutes()
        {
            RouteList routes = new();
            routes.AddRoute(RouteBuilder.Create()
                .WithSelector((ctx, ct) => Task.FromResult(true))
                .WithHandler((ctx, ts, ct) => Task.CompletedTask)
                .Build());
            routes.AddRoute(RouteBuilder.Create()
                .WithSelector((ctx, ct) => Task.FromResult(true))
                .WithHandler((ctx, ts, ct) => Task.CompletedTask)
                .Build());
            Assert.Equal(2, routes.FormatRouteList().Count);
        }

        [Fact]
        public void Test_RouteList_FormatRouteList_Empty()
        {
            RouteList routes = new();
            string result = routes.FormatRouteList().Formatted;
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void Test_RouteList_FormatRouteList_NoFlags()
        {
            RouteList routes = new();
            routes.AddRoute(RouteBuilder.Create()
                .WithSelector((ctx, ct) => Task.FromResult(true))
                .WithHandler(MyNamedHandler)
                .Build());

            string result = routes.FormatRouteList().Formatted;

            Assert.Contains("[0]", result);
            Assert.Contains("MyNamedHandler", result);
            Assert.Contains("flags=None", result);
            Assert.Contains($"rank={RouteRank.Unspecified}", result);
            Assert.DoesNotContain("channel=", result);
        }

        [Fact]
        public void Test_RouteList_FormatRouteList_InvokeAndAgenticFlags()
        {
            RouteList routes = new();
            routes.AddRoute(RouteBuilder.Create()
                .WithSelector((ctx, ct) => Task.FromResult(true))
                .WithHandler(MyNamedHandler)
                .AsInvoke(true)
                .AsAgentic(true)
                .Build());

            string result = routes.FormatRouteList().Formatted;

            Assert.Contains("flags=Invoke,Agentic", result);
        }

        [Fact]
        public void Test_RouteList_FormatRouteList_NonTerminalFlag()
        {
            RouteList routes = new();
            routes.AddRoute(RouteBuilder.Create()
                .WithSelector((ctx, ct) => Task.FromResult(true))
                .WithHandler(MyNamedHandler)
                .AsNonTerminal()
                .Build());

            string result = routes.FormatRouteList().Formatted;

            Assert.Contains("flags=NonTerminal", result);
        }

        [Fact]
        public void Test_RouteList_FormatRouteList_WithChannel()
        {
            RouteList routes = new();
            routes.AddRoute(RouteBuilder.Create()
                .WithSelector((ctx, ct) => Task.FromResult(true))
                .WithHandler(MyNamedHandler)
                .WithChannelId(Channels.Msteams)
                .Build());

            string result = routes.FormatRouteList().Formatted;

            Assert.Contains("channel=msteams", result);
        }

        [Fact]
        public void Test_RouteList_FormatRouteList_MultipleRoutes_OrderedByIndex()
        {
            RouteList routes = new();
            routes.AddRoute(RouteBuilder.Create()
                .WithSelector((ctx, ct) => Task.FromResult(true))
                .WithHandler(MyNamedHandler)
                .AsInvoke(true)
                .Build());
            routes.AddRoute(RouteBuilder.Create()
                .WithSelector((ctx, ct) => Task.FromResult(true))
                .WithHandler(MyNamedHandler)
                .Build());

            string result = routes.FormatRouteList().Formatted;

            // Invoke route comes first (higher priority)
            int idx0 = result.IndexOf("[0]");
            int idx1 = result.IndexOf("[1]");
            Assert.True(idx0 >= 0);
            Assert.True(idx1 >= 0);
            Assert.True(idx0 < idx1);
            Assert.Contains("flags=Invoke", result);
        }

        private static Task MyNamedHandler(ITurnContext ctx, ITurnState state, CancellationToken ct)
            => Task.CompletedTask;

        [Fact]
        public async Task Test_RouteList_ByAgenticThenInvokeThenRank()
        {
            List<string> values = [];
            RouteList routes = new();

            routes.AddRoute(
                RouteBuilder.Create()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("2"); return Task.CompletedTask; })
                    .Build()
            );

            routes.AddRoute(
                RouteBuilder.Create()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("1"); return Task.CompletedTask; })
                    .Build()
            );

            routes.AddRoute(
                RouteBuilder.Create()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("3"); return Task.CompletedTask; })
                    .WithOrderRank(RouteRank.First)
                    .Build()
            );

            routes.AddRoute(
                RouteBuilder.Create()
                    .AsInvoke()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("invoke"); return Task.CompletedTask; })
                    .Build()
            );

            routes.AddRoute(
                RouteBuilder.Create()
                    .AsAgentic()
                    .AsInvoke()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("agenticInvoke2"); return Task.CompletedTask; })
                    .Build()
            );

            routes.AddRoute(
                RouteBuilder.Create()
                    .AsAgentic()
                    .AsInvoke()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("agenticInvoke1"); return Task.CompletedTask; })
                    .WithOrderRank(RouteRank.First)
                    .Build()
            );

            routes.AddRoute(
                RouteBuilder.Create()
                    .AsAgentic()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("agentic"); return Task.CompletedTask; })
                    .Build()
            );


            foreach (var route in routes.Enumerate())
            {
                await route.Handler(null, null, CancellationToken.None);
            }

            Assert.Equal(7, values.Count);
            Assert.Equal("agenticInvoke1", values[0]);
            Assert.Equal("agenticInvoke2", values[1]);
            Assert.Equal("invoke", values[2]);
            Assert.Equal("agentic", values[3]);
            Assert.Equal("3", values[4]);
            Assert.Equal("2", values[5]);
            Assert.Equal("1", values[6]);
        }

        [Fact]
        public async Task Test_RouteList_ByInvokeThenByRank()
        {
            List<string> values = [];
            RouteList routes = new();

            routes.AddRoute(
                RouteBuilder.Create()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("2"); return Task.CompletedTask; })
                    .WithOrderRank(RouteRank.Unspecified)
                    .Build()
            );

            routes.AddRoute(
                RouteBuilder.Create()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("1"); return Task.CompletedTask; })
                    .WithOrderRank(0)
                    .Build()
            );

            routes.AddRoute(
                RouteBuilder.Create()
                    .AsInvoke()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("invoke1"); return Task.CompletedTask; })
                    .WithOrderRank(RouteRank.Last)
                    .Build()
            );
            routes.AddRoute(
                RouteBuilder.Create()
                    .AsInvoke()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("invoke2"); return Task.CompletedTask; })
                    .WithOrderRank(0)
                    .Build()
            );

            foreach (var route in routes.Enumerate())
            {
                await route.Handler(null, null, CancellationToken.None);
            }

            Assert.Equal(4, values.Count);
            Assert.Equal("invoke2", values[0]);
            Assert.Equal("invoke1", values[1]);
            Assert.Equal("1", values[2]);
            Assert.Equal("2", values[3]);
        }

        [Fact]
        public async Task Test_Application_Route()
        {
            // Arrange
            var activity1 = MessageFactory.Text("hello.1");
            activity1.Recipient = new() { Id = "recipientId" };
            activity1.Conversation = new() { Id = "conversationId" };
            activity1.From = new() { Id = "fromId" };
            activity1.ChannelId = "channelId";
            var activity2 = MessageFactory.Text("hello.2");
            activity2.Recipient = new() { Id = "recipientId" };
            activity2.Conversation = new() { Id = "conversationId" };
            activity2.From = new() { Id = "fromId" };
            activity2.ChannelId = "channelId";
            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });
            var messages = new List<string>();

            app.AddRoute(
                (context, _) => Task.FromResult(string.Equals("hello.1", context.Activity.Text)),
                (context, _, _) =>
                {
                    messages.Add(context.Activity.Text);
                    return Task.CompletedTask;
                },
                false);

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);

            // Assert
            Assert.Single(messages);
            Assert.Equal("hello.1", messages[0]);
        }

        [Fact]
        public async Task Test_Application_Routes_Are_Called_InOrder()
        {
            // Arrange
            var activity = MessageFactory.Text("hello.1");
            activity.Recipient = new() { Id = "recipientId" };
            activity.Conversation = new() { Id = "conversationId" };
            activity.From = new() { Id = "fromId" };
            activity.ChannelId = "channelId";
            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, activity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });
            var selectedRoutes = new List<int>();
            app.AddRoute(
                (context, _) => Task.FromResult(string.Equals("hello", context.Activity.Text)),
                (context, _, _) =>
                {
                    selectedRoutes.Add(0);
                    return Task.CompletedTask;
                },
                false);
            app.AddRoute(
                (context, _) => Task.FromResult(string.Equals("hello.1", context.Activity.Text)),
                (context, _, _) =>
                {
                    selectedRoutes.Add(1);
                    return Task.CompletedTask;
                },
                false);
            app.AddRoute(
                (_, _) => Task.FromResult(true),
                (context, _, _) =>
                {
                    selectedRoutes.Add(2);
                    return Task.CompletedTask;
                },
                false);

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Single(selectedRoutes);
            Assert.Equal(1, selectedRoutes[0]);
        }

        [Fact]
        public async Task Test_Application_InvokeRoute()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "invoke.1",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "invoke.2",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var turnContext2 = new TurnContext(adapter, activity2);

            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            var names = new List<string>();
            app.AddRoute(
                (context, _) => Task.FromResult(string.Equals("invoke.1", context.Activity.Name)),
                (context, _, _) =>
                {
                    names.Add(context.Activity.Name);
                    return Task.CompletedTask;
                },
                true);

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);

            // Assert
            Assert.Single(names);
            Assert.Equal("invoke.1", names[0]);
        }

        [Fact]
        public async Task Test_Application_InvokeRoutes_Are_Called_InOrder()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "invoke.1",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, activity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            var selectedRoutes = new List<int>();
            app.AddRoute(
                (context, _) => Task.FromResult(string.Equals("invoke", context.Activity.Name)),
                (context, _, _) =>
                {
                    selectedRoutes.Add(0);
                    return Task.CompletedTask;
                },
                true);
            app.AddRoute(
                (context, _) => Task.FromResult(string.Equals("invoke.1", context.Activity.Name)),
                (context, _, _) =>
                {
                    selectedRoutes.Add(1);
                    return Task.CompletedTask;
                },
                true);
            app.AddRoute(
                (_, _) => Task.FromResult(true),
                (context, _, _) =>
                {
                    selectedRoutes.Add(2);
                    return Task.CompletedTask;
                },
                true);

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Single(selectedRoutes);
            Assert.Equal(1, selectedRoutes[0]);
        }

        [Fact]
        public async Task Test_Application_InvokeRoutes_Are_Called_First()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "invoke.1",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, activity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            var selectedRoutes = new List<int>();
            app.AddRoute(
                (_, _) => Task.FromResult(true),
                (context, _, _) =>
                {
                    selectedRoutes.Add(0);
                    return Task.CompletedTask;
                },
                true);
            app.AddRoute(
                (_, _) => Task.FromResult(true),
                (context, _, _) =>
                {
                    selectedRoutes.Add(1);
                    return Task.CompletedTask;
                },
                false);

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Single(selectedRoutes);
            Assert.Equal(0, selectedRoutes[0]);
        }

        [Fact]
        public async Task Test_Application_No_InvokeRoute_Matched_Fallback_To_Routes()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "invoke.1",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, activity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            var selectedRoutes = new List<int>();
            app.AddRoute(
                (_, _) => Task.FromResult(false),
                (context, _, _) =>
                {
                    selectedRoutes.Add(0);
                    return Task.CompletedTask;
                },
                true);
            app.AddRoute(
                (context, _) => Task.FromResult(string.Equals("invoke.1", context.Activity.Name)),
                (context, _, _) =>
                {
                    selectedRoutes.Add(1);
                    return Task.CompletedTask;
                },
                false);
            app.AddRoute(
                (_, _) => Task.FromResult(true),
                (context, _, _) =>
                {
                    selectedRoutes.Add(2);
                    return Task.CompletedTask;
                },
                false);

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Single(selectedRoutes);
            Assert.Equal(1, selectedRoutes[0]);
        }

        [Fact]
        public async Task Test_OnActivity_String_Selector()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.Message,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var agenticActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var agenticTurnContext = new TurnContext(adapter, agenticActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            var types = new List<string>();
            var agenticTypes = new List<string>();

            // Agentic
            app.OnActivity(ActivityTypes.Message, (context, _, _) =>
            {
                agenticTypes.Add(context.Activity.Type);
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // Non-agentic
            app.OnActivity(ActivityTypes.Message, (context, _, _) =>
            {
                types.Add(context.Activity.Type);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(agenticTurnContext, CancellationToken.None);

            // Assert
            Assert.Single(types);
            Assert.Equal(ActivityTypes.Message, types[0]);

            Assert.Single(agenticTypes);
            Assert.Equal(ActivityTypes.Message, agenticTypes[0]);
        }

        [Fact]
        public async Task Test_OnActivity_Regex_Selector()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.Message,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.MessageDelete,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var agenticActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var agenticTurnContext = new TurnContext(adapter, agenticActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            var types = new List<string>();
            var agenticTypes = new List<string>();

            // Agentic
            app.OnActivity(new Regex("^message$"), (context, _, _) =>
            {
                agenticTypes.Add(context.Activity.Type);
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // Non-agentic
            app.OnActivity(new Regex("^message$"), (context, _, _) =>
            {
                types.Add(context.Activity.Type);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(agenticTurnContext, CancellationToken.None);

            // Assert
            Assert.Single(types);
            Assert.Equal(ActivityTypes.Message, types[0]);

            Assert.Single(agenticTypes);
            Assert.Equal(ActivityTypes.Message, agenticTypes[0]);
        }

        [Fact]
        public async Task Test_OnActivity_Function_Selector()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.Message,
                Name = "Message",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var agenticActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Name = "Message",
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var agenticTurnContext = new TurnContext(adapter, agenticActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            var types = new List<string>();
            var agenticTypes = new List<string>();

            // agentic
#pragma warning disable CS0618 // Type or member is obsolete
            app.OnActivity((context, _) => Task.FromResult(context.Activity?.Name != null), (context, _, _) =>
            {
                agenticTypes.Add(context.Activity.Type);
                return Task.CompletedTask;
            }, isAgenticOnly: true);
#pragma warning restore CS0618 // Type or member is obsolete

            // non-agentic
#pragma warning disable CS0618 // Type or member is obsolete
            app.OnActivity((context, _) => Task.FromResult(context.Activity?.Name != null), (context, _, _) =>
            {
                types.Add(context.Activity.Type);
                return Task.CompletedTask;
            });
#pragma warning restore CS0618 // Type or member is obsolete

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(agenticTurnContext, CancellationToken.None);

            // Assert
            Assert.Single(types);
            Assert.Equal(ActivityTypes.Message, types[0]);

            Assert.Single(agenticTypes);
            Assert.Equal(ActivityTypes.Message, agenticTypes[0]);
        }

        [Fact]
        public async Task Test_OnActivity_Multiple_Selectors()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.Message,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.MessageDelete,
                Name = "Delete",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });
            var types = new List<string>();
#pragma warning disable CS0618 // Type or member is obsolete
            app.OnActivity(new MultipleRouteSelector
            {
                Strings = new[] { ActivityTypes.Invoke },
                Regexes = new[] { new Regex("^message$") },
                RouteSelectors = new RouteSelector[] { (context, _) => Task.FromResult(context.Activity?.Name != null) },
            },
            (context, _, _) =>
            {
                types.Add(context.Activity.Type);
                return Task.CompletedTask;
            });
#pragma warning restore CS0618 // Type or member is obsolete

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);

            // Assert
            Assert.Equal(3, types.Count);
            Assert.Equal(ActivityTypes.Message, types[0]);
            Assert.Equal(ActivityTypes.MessageDelete, types[1]);
            Assert.Equal(ActivityTypes.Invoke, types[2]);
        }

        [Fact]
        public async Task Test_OnConversationUpdate_MembersAdded()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                MembersAdded = new List<ChannelAccount> { new() },
                Name = "1",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var agenticActivity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                MembersAdded = new List<ChannelAccount> { new() },
                Name = "agentic1",
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var agenticTurnContext = new TurnContext(adapter, agenticActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            var names = new List<string>();
            var agenticNames = new List<string>();

            // agentic
            app.OnConversationUpdate(ConversationUpdateEvents.MembersAdded, (context, _, _) =>
            {
                agenticNames.Add(context.Activity.Name);
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // non-agentic
            app.OnConversationUpdate(ConversationUpdateEvents.MembersAdded, (context, _, _) =>
            {
                names.Add(context.Activity.Name);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);
            await app.OnTurnAsync(agenticTurnContext, CancellationToken.None);

            // Assert
            Assert.Single(names);
            Assert.Equal("1", names[0]);

            Assert.Single(agenticNames);
            Assert.Equal("agentic1", agenticNames[0]);
        }

        [Fact]
        public async Task Test_OnConversationUpdate_MembersRemoved()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                MembersRemoved = new List<ChannelAccount> { new() },
                Name = "1",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var agenticActivity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                MembersRemoved = new List<ChannelAccount> { new() },
                Name = "agentic1",
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var agenticTurnContext = new TurnContext(adapter, agenticActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            var names = new List<string>();
            var agenticNames = new List<string>();

            // agentic
            app.OnConversationUpdate(ConversationUpdateEvents.MembersRemoved, (context, _, _) =>
            {
                agenticNames.Add(context.Activity.Name);
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // non-agentic
            app.OnConversationUpdate(ConversationUpdateEvents.MembersRemoved, (context, _, _) =>
            {
                names.Add(context.Activity.Name);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);
            await app.OnTurnAsync(agenticTurnContext, CancellationToken.None);

            // Assert
            Assert.Single(names);
            Assert.Equal("1", names[0]);

            Assert.Single(agenticNames);
            Assert.Equal("agentic1", agenticNames[0]);
        }

        [Fact]
        public async Task Test_OnConversationUpdate_UnknownEventName()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                Name = "1",
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, activity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });
            var names = new List<string>();
            app.OnConversationUpdate("unknown",
                (context, _, _) =>
                {
                    names.Add(context.Activity.Name);
                    return Task.CompletedTask;
                });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Single(names);
            Assert.Equal("1", names[0]);
        }

        [Fact]
        public async Task Test_OnMessage_String_Selector()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "HELLO",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Event,
                Text = "hello",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var agenticActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello",
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var nullTextActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var agenticTurnContext = new TurnContext(adapter, agenticActivity);
            var nullTextContext = new TurnContext(adapter, nullTextActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            var texts = new List<string>();
            var agenticTexts = new List<string>();

            // agentic
            app.OnMessage("hello", (context, _, _) =>
            {
                agenticTexts.Add(context.Activity.Text);
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // non-agentic
            app.OnMessage("hello", (context, _, _) =>
            {
                texts.Add(context.Activity.Text);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(agenticTurnContext, CancellationToken.None);
            await app.OnTurnAsync(nullTextContext, CancellationToken.None);

            // Assert
            Assert.Equal(2, texts.Count);
            Assert.Single(agenticTexts);
        }

        [Fact]
        public async Task Test_OnMessage_StringWord_Selector()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello a",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "welcome",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "i say hello b",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });
            var texts = new List<string>();
            app.OnMessage(new Regex(@"\bhello\b"), (context, _, _) =>
            {
                texts.Add(context.Activity.Text);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);

            // Assert
            Assert.Equal(2, texts.Count);
        }

        [Fact]
        public async Task Test_OnMessage_Regex_Selector()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "welcome",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Text = "hello",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var agenticActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "agentic hello",
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var agenticTurnContext = new TurnContext(adapter, agenticActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            var texts = new List<string>();
            var agenticTexts = new List<string>();

            // agentic
            app.OnMessage(new Regex("llo"), (context, _, _) =>
            {
                agenticTexts.Add(context.Activity.Text);
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // non-agentic
            app.OnMessage(new Regex("llo"), (context, _, _) =>
            {
                texts.Add(context.Activity.Text);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);
            await app.OnTurnAsync(agenticTurnContext, CancellationToken.None);

            // Assert
            Assert.Single(texts);
            Assert.Equal("hello", texts[0]);

            Assert.Single(agenticTexts);
            Assert.Equal("agentic hello", agenticTexts[0]);
        }

        [Fact]
        public async Task Test_OnMessage_Function_Selector()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var agenticActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "agentic hello",
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var agenticTurnContext = new TurnContext(adapter, agenticActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            var texts = new List<string>();
            var agenticTexts = new List<string>();

            // agentic
#pragma warning disable CS0618 // Type or member is obsolete
            app.OnMessage((context, _) => Task.FromResult(context.Activity?.Text != null), (context, _, _) =>
            {
                agenticTexts.Add(context.Activity.Text);
                return Task.CompletedTask;
            }, isAgenticOnly: true);
#pragma warning restore CS0618 // Type or member is obsolete

            // non-agentic
#pragma warning disable CS0618 // Type or member is obsolete
            app.OnMessage((context, _) => Task.FromResult(context.Activity?.Text != null), (context, _, _) =>
            {
                texts.Add(context.Activity.Text);
                return Task.CompletedTask;
            });
#pragma warning restore CS0618 // Type or member is obsolete

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(agenticTurnContext, CancellationToken.None);

            // Assert
            Assert.Single(texts);
            Assert.Equal("hello", texts[0]);

            Assert.Single(agenticTexts);
            Assert.Equal("agentic hello", agenticTexts[0]);
        }

        [Fact]
        public async Task Test_OnMessage_Multiple_Selectors()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "welcome",
                Name = "hello",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello world",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });
            var texts = new List<string>();
#pragma warning disable CS0618 // Type or member is obsolete
            app.OnMessage(new MultipleRouteSelector
            {
                Strings = new[] { "hello" },
                Regexes = new[] { new Regex(@"\bworld\b") },
                RouteSelectors = new RouteSelector[] { (context, _) => Task.FromResult(context.Activity?.Name != null) },
            },
            (context, _, _) =>
            {
                texts.Add(context.Activity.Text);
                return Task.CompletedTask;
            });
#pragma warning restore CS0618 // Type or member is obsolete

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);

            // Assert
            Assert.Equal(3, texts.Count);
            Assert.Equal("hello", texts[0]);
            Assert.Equal("welcome", texts[1]);
            Assert.Equal("hello world", texts[2]);
        }

        [Fact]
        public async Task Test_OnMessageReactionsAdded()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.MessageReaction,
                ReactionsAdded = new List<MessageReaction> { new() },
                Name = "1",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.MessageReaction,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Message,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var agenticActivity = new Activity
            {
                Type = ActivityTypes.MessageReaction,
                ReactionsAdded = new List<MessageReaction> { new() },
                Name = "agentic1",
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var agenticTurnContext = new TurnContext(adapter, agenticActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            var names = new List<string>();
            var agenticNames = new List<string>();

            // agentic
            app.OnMessageReactionsAdded((context, _, _) =>
            {
                agenticNames.Add(context.Activity.Name);
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // non-agentic
            app.OnMessageReactionsAdded((context, _, _) =>
            {
                names.Add(context.Activity.Name);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);
            await app.OnTurnAsync(agenticTurnContext, CancellationToken.None);

            // Assert
            Assert.Single(names);
            Assert.Equal("1", names[0]);

            Assert.Single(agenticNames);
            Assert.Equal("agentic1", agenticNames[0]);
        }

        [Fact]
        public async Task Test_OnMessageReactionsRemoved()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.MessageReaction,
                ReactionsRemoved = new List<MessageReaction> { new() },
                Name = "1",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.MessageReaction,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Message,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var agenticActivity = new Activity
            {
                Type = ActivityTypes.MessageReaction,
                ReactionsRemoved = new List<MessageReaction> { new() },
                Name = "agentic1",
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var agenticTurnContext = new TurnContext(adapter, agenticActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            var names = new List<string>();
            var agenticNames = new List<string>();

            // agentic
            app.OnMessageReactionsRemoved((context, _, _) =>
            {
                agenticNames.Add(context.Activity.Name);
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // non-agentic
            app.OnMessageReactionsRemoved((context, _, _) =>
            {
                names.Add(context.Activity.Name);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);
            await app.OnTurnAsync(agenticTurnContext, CancellationToken.None);

            // Assert
            Assert.Single(names);
            Assert.Equal("1", names[0]);

            Assert.Single(agenticNames);
            Assert.Equal("agentic1", agenticNames[0]);
        }

        [Fact]
        public async Task Test_OnHandoff()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);
            var activity1 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "handoff/action",
                Value = new { Continuation = "test" },
                Id = "test",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.Event,
                Name = "actionableMessage/executeAction",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/queryLink",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var agenticActivity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "handoff/action",
                Value = new { Continuation = "test" },
                Id = "agentic test",
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };

            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var agenticTurnContext = new TurnContext(adapter, agenticActivity);
            var expectedInvokeResponse = new InvokeResponse
            {
                Status = 200
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });


            var ids = new List<string>();
            var agenticIds = new List<string>();

            // agentic
            app.OnHandoff((turnContext, _, _, _) =>
            {
                agenticIds.Add(turnContext.Activity.Id);
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // non-agentic
            app.OnHandoff((turnContext, _, _, _) =>
            {
                ids.Add(turnContext.Activity.Id);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);
            await app.OnTurnAsync(agenticTurnContext, CancellationToken.None);

            // Assert
            Assert.Single(ids);
            Assert.Equal("test", ids[0]);
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);

            Assert.Single(agenticIds);
            Assert.Equal("agentic test", agenticIds[0]);
        }

        [Fact]
        public async Task Test_AgenticRoute_BeatsNonAgenticRoute_ForAgenticRequest()
        {
            // Arrange - An agentic message should be handled by the agentic route,
            // not by a competing non-agentic route registered first.
            var agenticActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello",
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, agenticActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            string handlerCalled = null;

            // Register non-agentic route first
            app.OnActivity(ActivityTypes.Message, (context, _, _) =>
            {
                handlerCalled = "nonAgentic";
                return Task.CompletedTask;
            }, isAgenticOnly: false);

            // Register agentic route second
            app.OnActivity(ActivityTypes.Message, (context, _, _) =>
            {
                handlerCalled = "agentic";
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert - agentic route wins due to route ordering priority
            Assert.Equal("agentic", handlerCalled);
        }

        [Fact]
        public async Task Test_TwoAgenticRoutes_SameRank_FirstMatchingWins()
        {
            // Arrange - Two agentic routes with the same rank; first matching selector wins.
            var agenticActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello",
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, agenticActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            string handlerCalled = null;

            // Both agentic, same rank, both match - first registered wins
            app.OnActivity(ActivityTypes.Message, (context, _, _) =>
            {
                handlerCalled = "first";
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            app.OnActivity(ActivityTypes.Message, (context, _, _) =>
            {
                handlerCalled = "second";
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal("first", handlerCalled);
        }

        [Fact]
        public async Task Test_NonAgenticRequest_SkipsAgenticRoutes()
        {
            // Arrange - A regular (non-agentic) message must not match an agentic-only route
            // and should fall through to the non-agentic handler.
            var normalActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, normalActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            string handlerCalled = null;

            // Agentic route registered first (higher priority in ordering)
            app.OnActivity(ActivityTypes.Message, (context, _, _) =>
            {
                handlerCalled = "agentic";
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // Non-agentic route registered second
            app.OnActivity(ActivityTypes.Message, (context, _, _) =>
            {
                handlerCalled = "nonAgentic";
                return Task.CompletedTask;
            }, isAgenticOnly: false);

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert - non-agentic request should skip the agentic route
            Assert.Equal("nonAgentic", handlerCalled);
        }

        [Fact]
        public async Task Test_AgentExtension_AddRoute_PropagatesAgenticFlag()
        {
            // Arrange - Verify that isAgenticOnly=true flows through AgentExtension.AddRoute
            // and results in the route being ordered above non-agentic routes.
            // This is the pattern used by A365 Notifications and other extensions.
            var agenticActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello",
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "testChannel",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, agenticActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            string handlerCalled = null;

            // Register non-agentic route directly on the app first
            app.OnActivity(ActivityTypes.Message, (context, _, _) =>
            {
                handlerCalled = "nonAgentic";
                return Task.CompletedTask;
            }, isAgenticOnly: false);

            // Register agentic route through the extension model (mimics A365 Notifications pattern)
            var extension = new TestExtension("testChannel");
            extension.AddRoute(
                app,
                (context, _) => Task.FromResult(context.Activity.Type == ActivityTypes.Message),
                (context, _, _) =>
                {
                    handlerCalled = "extensionAgentic";
                    return Task.CompletedTask;
                },
                isAgenticOnly: true);

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert - extension's agentic route wins due to ordering priority
            Assert.Equal("extensionAgentic", handlerCalled);
        }

        private class TestExtension : AgentExtension
        {
            public TestExtension(string channelId)
            {
                ChannelId = new ChannelId(channelId);
            }
        }

        [Fact]
        public async Task Test_AgentApplication_LogsRouteList_WhenDebugEnabled()
        {
            // Arrange: create a logger that captures debug messages
            var logMessages = new List<string>();
            var loggerFactory = LoggerFactory.Create(builder =>
                builder
                    .AddProvider(new CaptureLoggerProvider(logMessages))
                    .SetMinimumLevel(LogLevel.Debug));

            var adapter = new SimpleAdapter();
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Message,
                Text = "hi",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });

            var options = new AgentApplicationOptions((IStorage)null, loggerFactory)
            {
                StartTypingTimer = false,
            };
            var app = new AgentApplication(options);
            app.OnActivity(ActivityTypes.Message, (ctx, ts, ct) => Task.CompletedTask);

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert: at least one debug message contains route evaluation order text
            Assert.Contains(logMessages, m => m.Contains("route evaluation order"));
        }

        [Fact]
        public async Task Test_AgentApplication_DoesNotLogRouteList_WhenDebugDisabled()
        {
            var logMessages = new List<string>();
            var loggerFactory = LoggerFactory.Create(builder =>
                builder
                    .AddProvider(new CaptureLoggerProvider(logMessages))
                    .SetMinimumLevel(LogLevel.Information));

            var adapter = new SimpleAdapter();
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Message,
                Text = "hi",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });

            var options = new AgentApplicationOptions((IStorage)null, loggerFactory)
            {
                StartTypingTimer = false,
            };
            var app = new AgentApplication(options);
            app.OnActivity(ActivityTypes.Message, (ctx, ts, ct) => Task.CompletedTask);

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.DoesNotContain(logMessages, m => m.Contains("route evaluation order"));
        }

        private class CaptureLoggerProvider(List<string> messages) : ILoggerProvider
        {
            public ILogger CreateLogger(string categoryName) => new CaptureLogger(messages);
            public void Dispose() { }
        }

        private class CaptureLogger(List<string> messages) : ILogger
        {
            private static readonly IDisposable _noopScope = new NoopDisposable();
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => _noopScope;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception exception, Func<TState, Exception, string> formatter)
                => messages.Add(formatter(state, exception));
            private sealed class NoopDisposable : IDisposable { public void Dispose() { } }
        }
    }
}
