// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Builder.Tests;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.App;
using Microsoft.Agents.Extensions.Teams.App.MessageExtensions;
using Microsoft.Agents.Extensions.Teams.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.App
{
    public class MessageExtensionsTests
    {
        [Fact]
        public async Task Test_OnSubmitAction_CommandId()
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
                Name = "composeExtension/submitAction",
                Value = ProtocolJsonSerializer.ToJsonElements(new
                {
                    commandId = "test-command",
                    data = new
                    {
                        title = "test-title",
                        content = "test-content"
                    }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var actionResponseMock = new Mock<MessagingExtensionActionResponse>();
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = actionResponseMock.Object
            };
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            var extension = new TeamsAgentExtension(app);
            SubmitActionHandlerAsync handler = (turnContext, turnState, data, cancellationToken) =>
            {
                MessageExtensionActionData actionData = Cast<MessageExtensionActionData>(data);
                Assert.Equal("test-title", actionData.Title);
                Assert.Equal("test-content", actionData.Content);
                return Task.FromResult(actionResponseMock.Object);
            };
            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnSubmitAction("test-command", handler);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnSubmitAction_CommandId_NotHit()
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
                Name = "composeExtension/submitAction",
                Value = ProtocolJsonSerializer.ToJsonElements(new
                {
                    commandId = "test-command",
                    data = new
                    {
                        title = "test-title",
                        content = "test-content"
                    }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var actionResponseMock = new Mock<MessagingExtensionActionResponse>();
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            var extension = new TeamsAgentExtension(app);
            SubmitActionHandlerAsync handler = (turnContext, turnState, data, cancellationToken) =>
            {
                MessageExtensionActionData actionData = Cast<MessageExtensionActionData>(data);
                Assert.Equal("test-title", actionData.Title);
                Assert.Equal("test-content", actionData.Content);
                return Task.FromResult(actionResponseMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnSubmitAction("not-test-command", handler);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnSubmitAction_RouteSelector_ActivityNotMatched()
        {
            var adapter = new SimpleAdapter();
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/fetchTask",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var actionResponseMock = new Mock<MessagingExtensionActionResponse>();
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            var extension = new TeamsAgentExtension(app);

            RouteSelector routeSelector = (turnContext, cancellationToken) =>
            {
                return Task.FromResult(true);
            };
            SubmitActionHandlerAsync handler = (turnContext, turnState, data, cancellationToken) =>
            {
                return Task.FromResult(actionResponseMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnSubmitAction(routeSelector, handler);
            });
            // Act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await app.OnTurnAsync(turnContext, CancellationToken.None));

            // Assert
            Assert.Equal("Unexpected MessageExtensions.OnSubmitAction() triggered for activity type: invoke", exception.Message);
        }

        [Fact]
        public async Task Test_OnAgentMessagePreviewEdit_CommandId()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);
            var activity = new Activity()
            {
                Type = ActivityTypes.Message,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/submitAction",
                Value = ProtocolJsonSerializer.ToJsonElements(new
                {
                    commandId = "test-command",
                    botMessagePreviewAction = "edit",
                    botActivityPreview = new List<IActivity> { activity }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var actionResponseMock = new Mock<MessagingExtensionActionResponse>();
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = actionResponseMock.Object,
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,

            });
            var extension = new TeamsAgentExtension(app);

            BotMessagePreviewEditHandlerAsync handler = (turnContext, turnState, activityPreview, cancellationToken) =>
            {
                Assert.Equivalent(activity, activityPreview);
                return Task.FromResult(actionResponseMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnAgentMessagePreviewEdit("test-command", handler);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnAgentMessagePreviewEdit_CommandId_NotHit()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);
            var activity = new Activity()
            {
                Type = ActivityTypes.Message,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/submitAction",
                Value = ProtocolJsonSerializer.ToJsonElements(new
                {
                    commandId = "test-command",
                    botMessagePreviewAction = "send",
                    botActivityPreview = new List<Activity> { activity }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var actionResponseMock = new Mock<MessagingExtensionActionResponse>();
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            var extension = new TeamsAgentExtension(app);
            BotMessagePreviewEditHandlerAsync handler = (turnContext, turnState, activityPreview, cancellationToken) =>
            {
                Assert.Equivalent(activity, activityPreview);
                return Task.FromResult(actionResponseMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnAgentMessagePreviewEdit("not-test-command", handler);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnAgentMessagePreviewEdit_RouteSelector_ActivityNotMatched()
        {
            var adapter = new SimpleAdapter();
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/fetchTask",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var actionResponseMock = new Mock<MessagingExtensionActionResponse>();
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            var extension = new TeamsAgentExtension(app);
            RouteSelector routeSelector = (turnContext, cancellationToken) =>
            {
                return Task.FromResult(true);
            };
            BotMessagePreviewEditHandlerAsync handler = (turnContext, turnState, data, cancellationToken) =>
            {
                return Task.FromResult(actionResponseMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnAgentMessagePreviewEdit(routeSelector, handler);
            });
            // Act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await app.OnTurnAsync(turnContext, CancellationToken.None));

            // Assert
            Assert.Equal("Unexpected MessageExtensions.OnAgentMessagePreviewEdit() triggered for activity type: invoke", exception.Message);
        }

        [Fact]
        public async Task Test_OnAgentMessagePreviewSend_CommandId()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);
            var activity = new Activity()
            {
                Type = ActivityTypes.Message,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/submitAction",
                Value = ProtocolJsonSerializer.ToJsonElements(new
                {
                    commandId = "test-command",
                    botMessagePreviewAction = "send",
                    botActivityPreview = new List<Activity> { activity }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = new MessagingExtensionActionResponse()
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            var extension = new TeamsAgentExtension(app);
            BotMessagePreviewSendHandler handler = (turnContext, turnState, activityPreview, cancellationToken) =>
            {
                Assert.Equivalent(activity, activityPreview);
                return Task.CompletedTask;
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnAgentMessagePreviewSend("test-command", handler);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnAgentMessagePreviewSend_CommandId_NotHit()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);
            var activity = new Activity()
            {
                Type = ActivityTypes.Message,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/submitAction",
                Value = ProtocolJsonSerializer.ToJsonElements(new
                {
                    commandId = "test-command",
                    botMessagePreviewAction = "edit",
                    botActivityPreview = new List<Activity> { activity }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });

            var extension = new TeamsAgentExtension(app);
            BotMessagePreviewSendHandler handler = (turnContext, turnState, activityPreview, cancellationToken) =>
            {
                Assert.Equivalent(activity, activityPreview);
                return Task.CompletedTask;
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnAgentMessagePreviewSend("not-test-command", handler);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnAgentMessagePreviewSend_RouteSelector_ActivityNotMatched()
        {
            var adapter = new SimpleAdapter();
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/fetchTask",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            var extension = new TeamsAgentExtension(app);
            RouteSelector routeSelector = (turnContext, cancellationToken) =>
            {
                return Task.FromResult(true);
            };
            BotMessagePreviewSendHandler handler = (turnContext, turnState, data, cancellationToken) =>
            {
                return Task.CompletedTask;
            };
            
            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnAgentMessagePreviewSend(routeSelector, handler);
            });
            // Act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await app.OnTurnAsync(turnContext, CancellationToken.None));

            // Assert
            Assert.Equal("Unexpected MessageExtensions.OnAgentMessagePreviewSend() triggered for activity type: invoke", exception.Message);
        }

        [Fact]
        public async Task Test_OnFetchTask_CommandId()
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
                Name = "composeExtension/fetchTask",
                Value = ProtocolJsonSerializer.ToJsonElements(new
                {
                    commandId = "test-command",
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var taskModuleResponseMock = new Mock<TaskModuleResponse>();
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = taskModuleResponseMock.Object
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            var extension = new TeamsAgentExtension(app);
            FetchTaskHandlerAsync handler = (turnContext, turnState, cancellationToken) =>
            {
                return Task.FromResult(taskModuleResponseMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnFetchTask("test-command", handler);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnFetchTask_CommandId_NotHit()
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
                Name = "composeExtension/fetchTask",
                Value = ProtocolJsonSerializer.ToJsonElements(new
                {
                    commandId = "test-command",
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var taskModuleResponseMock = new Mock<TaskModuleResponse>();
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            var extension = new TeamsAgentExtension(app);
            FetchTaskHandlerAsync handler = (turnContext, turnState, cancellationToken) =>
            {
                return Task.FromResult(taskModuleResponseMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnFetchTask("not-test-command", handler);
            });
            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnFetchTask_RouteSelector_ActivityNotMatched()
        {
            var adapter = new SimpleAdapter();
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/submitAction",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var taskModuleResponseMock = new Mock<TaskModuleResponse>();
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            var extension = new TeamsAgentExtension(app);
            RouteSelector routeSelector = (turnContext, cancellationToken) =>
            {
                return Task.FromResult(true);
            };
            FetchTaskHandlerAsync handler = (turnContext, turnState, cancellationToken) =>
            {
                return Task.FromResult(taskModuleResponseMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnFetchTask(routeSelector, handler);
            });
            // Act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await app.OnTurnAsync(turnContext, CancellationToken.None));

            // Assert
            Assert.Equal("Unexpected MessageExtensions.OnFetchTask() triggered for activity type: invoke", exception.Message);
        }

        [Fact]
        public async Task Test_OnQuery_CommandId()
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
                Name = "composeExtension/query",
                Value = ProtocolJsonSerializer.ToJsonElements(new
                {
                    commandId = "test-command",
                    parameters = new List<MessagingExtensionParameter>
                    {
                        new("test-name", "test-value")
                    },
                    queryOptions = new
                    {
                        count = 10,
                        skip = 0
                    }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var messagingExtensionResultMock = new Mock<MessagingExtensionResult>();
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = new MessagingExtensionActionResponse()
                {
                    ComposeExtension = messagingExtensionResultMock.Object
                }
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            var extension = new TeamsAgentExtension(app);
            QueryHandlerAsync handler = (turnContext, turnState, query, cancellationToken) =>
            {
                Assert.Single(query.Parameters);
                Assert.Equal("test-value", query.Parameters["test-name"].ToString());
                Assert.Equal(10, query.Count);
                Assert.Equal(0, query.Skip);
                return Task.FromResult(messagingExtensionResultMock.Object);
            };
            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnQuery("test-command", handler);
            });
            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnQuery_CommandId_NotHit()
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
                Name = "composeExtension/query",
                Value = ProtocolJsonSerializer.ToJsonElements(new
                {
                    commandId = "test-command",
                    parameters = new List<MessagingExtensionParameter>
                    {
                        new("test-name", "test-value")
                    },
                    queryOptions = new
                    {
                        count = 10,
                        skip = 0
                    }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var messagingExtensionResultMock = new Mock<MessagingExtensionResult>();
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            var extension = new TeamsAgentExtension(app);
            QueryHandlerAsync handler = (turnContext, turnState, query, cancellationToken) =>
            {
                Assert.Single(query.Parameters);
                Assert.Equal("test-value", query.Parameters["test-name"]);
                Assert.Equal(10, query.Count);
                Assert.Equal(0, query.Skip);
                return Task.FromResult(messagingExtensionResultMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnQuery("not-test-command", handler);
            });
            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnQuery_RouteSelector_NotMatched()
        {
            var adapter = new SimpleAdapter();
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/selectItem",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var messagingExtensionResultMock = new Mock<MessagingExtensionResult>();
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            var extension = new TeamsAgentExtension(app);
            RouteSelector routeSelector = (turnContext, cancellationToken) =>
            {
                return Task.FromResult(true);
            };
            QueryHandlerAsync handler = (turnContext, turnState, data, cancellationToken) =>
            {
                return Task.FromResult(messagingExtensionResultMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnQuery(routeSelector, handler);
            });

            // Act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await app.OnTurnAsync(turnContext, CancellationToken.None));

            // Assert
            Assert.Equal("Unexpected MessageExtensions.OnQuery() triggered for activity type: invoke", exception.Message);
        }

        [Fact]
        public async Task Test_SelectItem()
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
                Name = "composeExtension/selectItem",
                Value = ProtocolJsonSerializer.ToJsonElements(new { }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var messagingExtensionResultMock = new Mock<MessagingExtensionResult>();
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = new MessagingExtensionActionResponse()
                {
                    ComposeExtension = messagingExtensionResultMock.Object
                }
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            var extension = new TeamsAgentExtension(app);
            SelectItemHandlerAsync handler = (turnContext, turnState, item, cancellationToken) =>
            {
                return Task.FromResult(messagingExtensionResultMock.Object);
            };
            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnSelectItem(handler);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_SelectItem_NotHit()
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
                Name = "composeExtension/query",
                Value = ProtocolJsonSerializer.ToJsonElements(new { }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var messagingExtensionResultMock = new Mock<MessagingExtensionResult>();
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = new MessagingExtensionActionResponse()
                {
                    ComposeExtension = messagingExtensionResultMock.Object
                }
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            var extension = new TeamsAgentExtension(app);
            SelectItemHandlerAsync handler = (turnContext, turnState, item, cancellationToken) =>
            {
                return Task.FromResult(messagingExtensionResultMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnSelectItem(handler);
            });
            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnQueryLink()
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
                Name = "composeExtension/queryLink",
                Value = new
                {
                    url = "test-url"
                },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var messagingExtensionResultMock = new Mock<MessagingExtensionResult>();
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = new MessagingExtensionActionResponse()
                {
                    ComposeExtension = messagingExtensionResultMock.Object
                }
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            var extension = new TeamsAgentExtension(app);
            QueryLinkHandlerAsync handler = (turnContext, turnState, url, cancellationToken) =>
            {
                Assert.Equal("test-url", url);
                return Task.FromResult(messagingExtensionResultMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnQueryLink(handler);
            });
            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnQueryLink_NotHit()
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
                Name = "composeExtension/query",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var messagingExtensionResultMock = new Mock<MessagingExtensionResult>();
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            var extension = new TeamsAgentExtension(app);
            QueryLinkHandlerAsync handler = (turnContext, turnState, url, cancellationToken) =>
            {
                return Task.FromResult(messagingExtensionResultMock.Object);
            };
            
            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnQueryLink(handler);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnAnonymousQueryLink()
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
                Name = "composeExtension/anonymousQueryLink",
                Value = new
                {
                    url = "test-url"
                },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var messagingExtensionResultMock = new Mock<MessagingExtensionResult>();
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = new MessagingExtensionActionResponse()
                {
                    ComposeExtension = messagingExtensionResultMock.Object
                }
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            var extension = new TeamsAgentExtension(app);
            QueryLinkHandlerAsync handler = (turnContext, turnState, url, cancellationToken) =>
            {
                Assert.Equal("test-url", url);
                return Task.FromResult(messagingExtensionResultMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnAnonymousQueryLink(handler);
            });
            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnAnonymousQueryLink_NotHit()
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
                Name = "composeExtension/query",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var messagingExtensionResultMock = new Mock<MessagingExtensionResult>();
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
            });
            var extension = new TeamsAgentExtension(app);
            QueryLinkHandlerAsync handler = (turnContext, turnState, url, cancellationToken) =>
            {
                return Task.FromResult(messagingExtensionResultMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnAnonymousQueryLink(handler);
            });
            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnQueryUrlSetting()
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
                Name = "composeExtension/querySettingUrl",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var messagingExtensionResultMock = new Mock<MessagingExtensionResult>();
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = new MessagingExtensionActionResponse()
                {
                    ComposeExtension = messagingExtensionResultMock.Object
                }
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
            });
            var extension = new TeamsAgentExtension(app);
            QueryUrlSettingHandlerAsync handler = (turnContext, turnState, cancellationToken) =>
            {
                return Task.FromResult(messagingExtensionResultMock.Object);
            };
            
            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnQueryUrlSetting(handler);
            });
            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnQueryUrlSetting_NotHit()
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
                Name = "composeExtension/settings",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var messagingExtensionResultMock = new Mock<MessagingExtensionResult>();
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
            });
            var extension = new TeamsAgentExtension(app);
            QueryLinkHandlerAsync handler = (turnContext, turnState, url, cancellationToken) =>
            {
                return Task.FromResult(messagingExtensionResultMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnAnonymousQueryLink(handler);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnConfigureSettings()
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
                Name = "composeExtension/setting",
                Value = ProtocolJsonSerializer.ToJsonElements(new
                {
                    state = "test-state"
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
            });
            var extension = new TeamsAgentExtension(app);
            ConfigureSettingsHandler handler = (turnContext, turnState, settings, cancellationToken) =>
            {
                var obj = ProtocolJsonSerializer.ToJsonElements(settings);
                Assert.NotNull(obj);
                Assert.Equal("test-state", obj["state"].ToString());
                return Task.CompletedTask;
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnConfigureSettings(handler);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnConfigureSettings_NotHit()
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
                Name = "composeExtension/querySettingUrl",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
            });
            var extension = new TeamsAgentExtension(app);
            ConfigureSettingsHandler handler = (turnContext, turnState, settings, cancellationToken) =>
            {
                return Task.CompletedTask;
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnConfigureSettings(handler);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnCardButtonClicked()
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
                Name = "composeExtension/onCardButtonClicked",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
            });
            var extension = new TeamsAgentExtension(app);
            CardButtonClickedHandler handler = (turnContext, turnState, cardData, cancellationToken) =>
            {
                return Task.CompletedTask;
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnCardButtonClicked(handler);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnCardButtonClicked_NotHit()
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
                Name = "composeExtension/querySettingUrl",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
            });
            var extension = new TeamsAgentExtension(app);
            CardButtonClickedHandler handler = (turnContext, turnState, cardData, cancellationToken) =>
            {
                return Task.CompletedTask;
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnCardButtonClicked(handler);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Null(activitiesToSend);
        }

        private static T Cast<T>(object data)
        {
            Assert.NotNull(data);
            T result = ProtocolJsonSerializer.ToObject<T>(data);
            Assert.NotNull(result);
            return result;
        }

        private sealed class MessageExtensionActionData
        {
            public string Title { get; set; }

            public string Content { get; set; }
        }
    }
}
