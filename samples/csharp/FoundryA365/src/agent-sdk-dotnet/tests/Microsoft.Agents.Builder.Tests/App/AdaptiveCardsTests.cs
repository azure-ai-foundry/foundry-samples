// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.AdaptiveCards;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Moq;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class AdaptiveCardsTests
    {
        [Fact]
        public async Task Test_OnActionExecute_Success()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "adaptiveCard/action",
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    action = new
                    {
                        type = "Action.Execute",
                        verb = "test-verb",
                        data = new { testKey = "test-value" }
                    }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var adaptiveCardInvokeResponseMock = new Mock<AdaptiveCardInvokeResponse>();
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = adaptiveCardInvokeResponseMock.Object,
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            ActionExecuteHandler handler = (turnContext, turnState, data, cancellationToken) =>
            {
                TestAdaptiveCardActionData actionData = Cast<TestAdaptiveCardActionData>(data);
                Assert.Equal("test-value", actionData.TestKey);
                return Task.FromResult(adaptiveCardInvokeResponseMock.Object);
            };

            // Act
            app.AdaptiveCards.OnActionExecute("test-verb", handler);
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal(ActivityTypes.InvokeResponse, activitiesToSend[0].Type);
            Assert.IsAssignableFrom<InvokeResponse>(activitiesToSend[0].Value);
            var invokeResponse = (InvokeResponse)activitiesToSend[0].Value;
            Assert.Equal(200, invokeResponse.Status);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnActionExecute_Verb_NotMatched()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);
            var turnContext1 = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "adaptiveCard/action",
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    action = new
                    {
                        type = "Action.Execute",
                        verb = "not-test-verb"
                    }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var adaptiveCardInvokeResponseMock = new Mock<AdaptiveCardInvokeResponse>();
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);

            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            ActionExecuteHandler handler = (turnContext, turnState, data, cancellationToken) =>
            {
                return Task.FromResult(adaptiveCardInvokeResponseMock.Object);
            };

            // Act
            app.AdaptiveCards.OnActionExecute("test-verb", handler);
            await app.OnTurnAsync(turnContext1, CancellationToken.None);

            // Assert
            // The Activity is just ignored since it didn't match the verb.
            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnActionExecute_UnexpectedAction()
        {
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }

            var adapter = new SimpleAdapter(CaptureSend);
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "adaptiveCard/action",
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    action = new
                    {
                        type = "Not.Action.Execute",
                        verb = "not-test-verb"
                    }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var adaptiveCardInvokeResponseMock = new Mock<AdaptiveCardInvokeResponse>();
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            RouteSelector routeSelector = (turnContext, cancellationToken) =>
            {
                return Task.FromResult(true);
            };
            ActionExecuteHandler handler = (turnContext, turnState, data, cancellationToken) =>
            {
                return Task.FromResult(adaptiveCardInvokeResponseMock.Object);
            };

            // Act
            app.AdaptiveCards.OnActionExecute(routeSelector, handler);
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Single(activitiesToSend);
            Assert.Equal(ActivityTypes.InvokeResponse, activitiesToSend[0].Type);
            Assert.IsAssignableFrom<InvokeResponse>(activitiesToSend[0].Value);
            var invokeResponse = (InvokeResponse)activitiesToSend[0].Value;
            Assert.Equal(400, invokeResponse.Status);
            Assert.IsAssignableFrom<AdaptiveCardInvokeResponse>(invokeResponse.Body);
            var acResponse = (AdaptiveCardInvokeResponse)invokeResponse.Body;
            Assert.Equal("application/vnd.microsoft.error", acResponse.Type);
            Assert.Equal("NotSupported", ((Error)acResponse.Value).Code);
        }

        [Fact]
        public async Task Test_OnActionExecute_NullAction()
        {
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }

            var adapter = new SimpleAdapter(CaptureSend);
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "adaptiveCard/action",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var adaptiveCardInvokeResponseMock = new Mock<AdaptiveCardInvokeResponse>();
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            RouteSelector routeSelector = (turnContext, cancellationToken) =>
            {
                return Task.FromResult(true);
            };
            ActionExecuteHandler handler = (turnContext, turnState, data, cancellationToken) =>
            {
                return Task.FromResult(adaptiveCardInvokeResponseMock.Object);
            };

            // Act
            app.AdaptiveCards.OnActionExecute(routeSelector, handler);
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Single(activitiesToSend);
            Assert.Equal(ActivityTypes.InvokeResponse, activitiesToSend[0].Type);
            Assert.IsAssignableFrom<InvokeResponse>(activitiesToSend[0].Value);
            var invokeResponse = (InvokeResponse)activitiesToSend[0].Value;
            Assert.Equal(400, invokeResponse.Status);
            Assert.IsAssignableFrom<AdaptiveCardInvokeResponse>(invokeResponse.Body);
            var acResponse = (AdaptiveCardInvokeResponse)invokeResponse.Body;
            Assert.Equal("application/vnd.microsoft.error", acResponse.Type);
            Assert.Equal("BadRequest", ((Error)acResponse.Value).Code);
        }

        [Fact]
        public async Task Test_OnActionSubmit_Verb()
        {
            // Arrange
            var adapter = new SimpleAdapter();
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Message,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    verb = "test-verb",
                    testKey = "test-value"
                }),
                Recipient = new("test-id"),
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            var called = false;
            ActionSubmitHandler handler = (turnContext, turnState, data, cancellationToken) =>
            {
                called = true;
                TestAdaptiveCardSubmitData submitData = Cast<TestAdaptiveCardSubmitData>(data);
                Assert.Equal("test-verb", submitData.Verb);
                Assert.Equal("test-value", submitData.TestKey);
                return Task.CompletedTask;
            };

            // Act
            app.AdaptiveCards.OnActionSubmit("test-verb", handler);
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(called);
        }

        [Fact]
        public async Task Test_OnActionSubmit_Verb_NotHit()
        {
            // Arrange
            var adapter = new SimpleAdapter();
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Message,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    verb = "test-verb",
                    testKey = "test-value"
                }),
                Recipient = new("test-id"),
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            var called = false;
            ActionSubmitHandler handler = (turnContext, turnState, data, cancellationToken) =>
            {
                called = true;
                TestAdaptiveCardSubmitData submitData = Cast<TestAdaptiveCardSubmitData>(data);
                Assert.Equal("test-verb", submitData.Verb);
                Assert.Equal("test-value", submitData.TestKey);
                return Task.CompletedTask;
            };

            // Act
            app.AdaptiveCards.OnActionSubmit("not-test-verb", handler);
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.False(called);
        }

        [Fact]
        public async Task Test_OnActionSubmit_RouteSelector_ActivityNotMatched()
        {
            // Arrange
            var adapter = new SimpleAdapter();
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Message,
                Text = "test-text",
                Recipient = new("test-id"),
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            RouteSelector routeSelector = (turnContext, cancellationToken) =>
            {
                return Task.FromResult(true);
            };
            ActionSubmitHandler handler = (turnContext, turnState, data, cancellationToken) =>
            {
                return Task.CompletedTask;
            };

            // Act
            app.AdaptiveCards.OnActionSubmit(routeSelector, handler);
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await app.OnTurnAsync(turnContext, CancellationToken.None));

            // Assert
            Assert.Equal("Unexpected AdaptiveCards.OnActionSubmit() triggered for activity type: message", exception.Message);
        }

        [Fact]
        public async Task Test_OnSearch_Dataset()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "application/search",
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    kind = "search",
                    queryText = "test-query",
                    queryOptions = new
                    {
                        skip = 0,
                        top = 15
                    },
                    dataset = "test-dataset"
                }),
                Recipient = new("test-id"),
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            IList<AdaptiveCardsSearchResult> searchResults = new List<AdaptiveCardsSearchResult>
            {
                new("test-title", "test-value")
            };
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = new SearchInvokeResponse()
                {
                    StatusCode = 200,
                    Type = ContentTypes.SearchResponse,
                    Value = new
                    {
                        Results = searchResults
                    }
                }
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            SearchHandler handler = (turnContext, turnState, query, cancellationToken) =>
            {
                Assert.Equal("test-query", query.Parameters.QueryText);
                Assert.Equal("test-dataset", query.Parameters.Dataset);
                return Task.FromResult(searchResults);
            };

            // Act
            app.AdaptiveCards.OnSearch("test-dataset", handler);
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnSearch_Dataset_NotHit()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "application/search",
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    kind = "search",
                    queryText = "test-query",
                    queryOptions = new
                    {
                        skip = 0,
                        top = 15
                    },
                    dataset = "test-dataset"
                }),
                Recipient = new("test-id"),
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            IList<AdaptiveCardsSearchResult> searchResults = new List<AdaptiveCardsSearchResult>
            {
                new("test-title", "test-value")
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            SearchHandler handler = (turnContext, turnState, query, cancellationToken) =>
            {
                Assert.Equal("test-query", query.Parameters.QueryText);
                Assert.Equal("test-dataset", query.Parameters.Dataset);
                return Task.FromResult(searchResults);
            };

            // Act
            app.AdaptiveCards.OnSearch("not-test-dataset", handler);
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnSearch_RouteSelector_ActivityNotMatched()
        {
            // Arrange
            var adapter = new SimpleAdapter();
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "adaptiveCard/action",
                Recipient = new("test-id"),
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            IList<AdaptiveCardsSearchResult> searchResults =
            [
                new("test-title", "test-value")
            ];
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            RouteSelector routeSelector = (turnContext, cancellationToken) =>
            {
                return Task.FromResult(true);
            };
            SearchHandler handler = (turnContext, turnState, query, cancellationToken) =>
            {
                Assert.Equal("test-query", query.Parameters.QueryText);
                Assert.Equal("test-dataset", query.Parameters.Dataset);
                return Task.FromResult(searchResults);
            };

            // Act
            app.AdaptiveCards.OnSearch(routeSelector, handler);
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await app.OnTurnAsync(turnContext, CancellationToken.None));

            // Assert
            Assert.Equal("Unexpected AdaptiveCards.OnSearch() triggered for activity type: invoke", exception.Message);
        }

        [Fact]
        public async Task Test_OnSearch_NoValue()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "application/search",
                Recipient = new("test-id"),
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            SearchHandler handler = (turnContext, turnState, query, cancellationToken) =>
            {
                throw new NotImplementedException();
            };

            // Act
            app.AdaptiveCards.OnSearch((ctx, ct) => Task.FromResult(true), handler);
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Single(activitiesToSend);
            Assert.Equal(ActivityTypes.InvokeResponse, activitiesToSend[0].Type);
            Assert.IsAssignableFrom<InvokeResponse>(activitiesToSend[0].Value);
            var invokeResponse = (InvokeResponse)activitiesToSend[0].Value;
            Assert.Equal(400, invokeResponse.Status);
            var acResponse = (AdaptiveCardInvokeResponse)invokeResponse.Body;
            Assert.Equal("application/vnd.microsoft.error", acResponse.Type);
            Assert.Equal("BadRequest", ((Error)acResponse.Value).Code);
        }

        [Fact]
        public async Task Test_OnSearch_NoKind()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "application/search",
                Recipient = new("test-id"),
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    //kind = "search",
                    queryText = "test-query",
                    queryOptions = new
                    {
                        skip = 0,
                        top = 15
                    },
                    dataset = "test-dataset"
                }),
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            SearchHandler handler = (turnContext, turnState, query, cancellationToken) =>
            {
                throw new NotImplementedException();
            };

            // Act
            app.AdaptiveCards.OnSearch((ctx, ct) => Task.FromResult(true), handler);
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Single(activitiesToSend);
            Assert.Equal(ActivityTypes.InvokeResponse, activitiesToSend[0].Type);
            Assert.IsAssignableFrom<InvokeResponse>(activitiesToSend[0].Value);
            var invokeResponse = (InvokeResponse)activitiesToSend[0].Value;
            Assert.Equal(400, invokeResponse.Status);
            Assert.IsAssignableFrom<AdaptiveCardInvokeResponse>(invokeResponse.Body);
            var response = (AdaptiveCardInvokeResponse)invokeResponse.Body;
            Assert.Equal("application/vnd.microsoft.error", response.Type);
            Assert.IsAssignableFrom<Core.Models.Error>(response.Value);
            var error = (Core.Models.Error)response.Value;
            Assert.Contains("kind", error.Message);
        }

        [Fact]
        public async Task Test_OnSearch_NoQueryText()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "application/search",
                Recipient = new("test-id"),
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    kind = "search",
                    //queryText = "test-query",
                    queryOptions = new
                    {
                        skip = 0,
                        top = 15
                    },
                    dataset = "test-dataset"
                }),
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            SearchHandler handler = (turnContext, turnState, query, cancellationToken) =>
            {
                throw new NotImplementedException();
            };

            // Act
            app.AdaptiveCards.OnSearch((ctx, ct) => Task.FromResult(true), handler);
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Single(activitiesToSend);
            Assert.Equal(ActivityTypes.InvokeResponse, activitiesToSend[0].Type);
            Assert.IsAssignableFrom<InvokeResponse>(activitiesToSend[0].Value);
            var invokeResponse = (InvokeResponse)activitiesToSend[0].Value;
            Assert.Equal(400, invokeResponse.Status);
            Assert.IsAssignableFrom<AdaptiveCardInvokeResponse>(invokeResponse.Body);
            var response = (AdaptiveCardInvokeResponse)invokeResponse.Body;
            Assert.Equal("application/vnd.microsoft.error", response.Type);
            Assert.IsAssignableFrom<Core.Models.Error>(response.Value);
            var error = (Core.Models.Error)response.Value;
            Assert.Contains("queryText", error.Message);
        }


        private static T Cast<T>(object data)
        {
            T result = ProtocolJsonSerializer.ToObject<T>(data);
            Assert.NotNull(result);
            return result;
        }

        private sealed class TestAdaptiveCardActionData
        {
            public string TestKey { get; set; }
        }

        private sealed class TestAdaptiveCardSubmitData
        {
            public string Verb { get; set; }

            public string TestKey { get; set; }
        }
    }
}
