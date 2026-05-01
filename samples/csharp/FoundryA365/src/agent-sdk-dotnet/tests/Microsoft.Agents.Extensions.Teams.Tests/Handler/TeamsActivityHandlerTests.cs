// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using Microsoft.Agents.Core.Models;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Agents.Extensions.Teams.Models;
using System;
using System.Globalization;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Extensions.Teams.Tests.Model;
using Microsoft.Agents.Connector;
using Moq;
using System.Net.Http;
using System.Threading;
using System.Net;
using Microsoft.Agents.Builder.Tests;

namespace Microsoft.Agents.Extensions.Teams.Tests.Handler
{
    public class TeamsActivityHandlerTests
    {
        IActivity[] _activitiesToSend = null;

        public TeamsActivityHandlerTests()
        {
            // called between each test, and resets state to prevent leakage. 
            _activitiesToSend = null;
        }
        void CaptureSend(IActivity[] arg)
        {
            _activitiesToSend = arg;
        }

        [Fact]
        public async Task TestConversationUpdateBotTeamsMemberAdded()
        {
            // Arrange            
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                MembersAdded =
                [
                    new ChannelAccount { Id = "bot" },
                ],
                Recipient = new ChannelAccount { Id = "bot" },
                ChannelData = new TeamsChannelData
                {
                    EventType = "teamMemberAdded",
                    Team = new TeamInfo
                    {
                        Id = "team-id",
                    },
                },
                ChannelId = Channels.Msteams,
            };

            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMembersAddedAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestConversationUpdateTeamsMemberAddedNoTeam()
        {
            // Arrange 
            var conversationsMock = new Mock<IConversations>();
            conversationsMock.Setup(x => x.GetConversationMemberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChannelAccount { Id = "id-1" });

            var connectorMock = new Mock<IConnectorClient>();
            connectorMock.Setup(x => x.Conversations).Returns(conversationsMock.Object);

            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                MembersAdded =
                [
                    new ChannelAccount { Id = "id-1" },
                ],
                Recipient = new ChannelAccount { Id = "bot" },
                Conversation = new ConversationAccount { Id = "conversation-id" },
                ChannelId = Channels.Msteams,
            };

            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);
            turnContext.Services.Set(connectorMock.Object);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMembersAddedAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestConversationUpdateTeamsMemberAddedFullDetailsInEvent()
        {
            var conversationsMock = new Mock<IConversations>();
            conversationsMock.Setup(x => x.GetConversationMemberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChannelAccount { Id = "id-1" });

            var connectorMock = new Mock<IConnectorClient>();
            connectorMock.Setup(x => x.Conversations).Returns(conversationsMock.Object);

            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                MembersAdded =
                [
                    new TeamsChannelAccount
                    {
                        Id = "id-1",
                        Name = "name-1",
                        AadObjectId = "aadobject-1",
                        Email = "test@microsoft.com",
                        GivenName = "given-1",
                        Surname = "surname-1",
                        UserPrincipalName = "t@microsoft.com",
                    },
                ],
                Recipient = new ChannelAccount { Id = "b" },
                ChannelData = new TeamsChannelData
                {
                    EventType = "teamMemberAdded",
                    Team = new TeamInfo
                    {
                        Id = "team-id",
                    },
                },
                ChannelId = Channels.Msteams,
            };

            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);
            turnContext.Services.Set(connectorMock.Object);

            var bot = new TestActivityHandler();
            await ((IAgent)bot).OnTurnAsync(turnContext);

            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMembersAddedAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestConversationUpdateTeamsMemberRemoved()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                MembersRemoved =
                [
                    new ChannelAccount { Id = "a" },
                ],
                Recipient = new ChannelAccount { Id = "b" },
                ChannelData = new TeamsChannelData { EventType = "teamMemberRemoved" },
                ChannelId = Channels.Msteams,
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMembersRemovedAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestConversationUpdateTeamsChannelCreated()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelData = new TeamsChannelData { EventType = "channelCreated" },
                ChannelId = Channels.Msteams,
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsChannelCreatedAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestConversationUpdateTeamsChannelDeleted()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelData = new TeamsChannelData { EventType = "channelDeleted" },
                ChannelId = Channels.Msteams,
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsChannelDeletedAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestConversationUpdateTeamsChannelRenamed()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelData = new TeamsChannelData { EventType = "channelRenamed" },
                ChannelId = Channels.Msteams,
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsChannelRenamedAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestConversationUpdateTeamsChannelRestored()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelData = new TeamsChannelData { EventType = "channelRestored" },
                ChannelId = Channels.Msteams,
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsChannelRestoredAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestConversationUpdateTeamsTeamArchived()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelData = new TeamsChannelData { EventType = "teamArchived" },
                ChannelId = Channels.Msteams,
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsTeamArchivedAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestConversationUpdateTeamsTeamDeleted()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelData = new TeamsChannelData { EventType = "teamDeleted" },
                ChannelId = Channels.Msteams,
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsTeamDeletedAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestConversationUpdateTeamsTeamHardDeleted()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelData = new TeamsChannelData { EventType = "teamHardDeleted" },
                ChannelId = Channels.Msteams,
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsTeamHardDeletedAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestConversationUpdateTeamsTeamRenamed()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelData = new TeamsChannelData { EventType = "teamRenamed" },
                ChannelId = Channels.Msteams,
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsTeamRenamedAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestConversationUpdateTeamsTeamRestored()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelData = new TeamsChannelData { EventType = "teamRestored" },
                ChannelId = Channels.Msteams,
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsTeamRestoredAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestConversationUpdateTeamsTeamUnarchived()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelData = new TeamsChannelData { EventType = "teamUnarchived" },
                ChannelId = Channels.Msteams,
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsTeamUnarchivedAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestFileConsentAccept()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "fileConsent/invoke",
                Value = JsonSerializer.SerializeToElement(new FileConsentCardResponse
                {
                    Action = "accept",
                    UploadInfo = new FileUploadInfo
                    {
                        UniqueId = "uniqueId",
                        FileType = "fileType",
                        UploadUrl = "uploadUrl",
                    },
                }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(3, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsFileConsentAsync", bot.Record[1]);
            Assert.Equal("OnTeamsFileConsentAcceptAsync", bot.Record[2]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestFileConsentDecline()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "fileConsent/invoke",
                Value = JsonSerializer.SerializeToElement(new FileConsentCardResponse
                {
                    Action = "decline",
                    UploadInfo = new FileUploadInfo
                    {
                        UniqueId = "uniqueId",
                        FileType = "fileType",
                        UploadUrl = "uploadUrl",
                    },
                }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(3, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsFileConsentAsync", bot.Record[1]);
            Assert.Equal("OnTeamsFileConsentDeclineAsync", bot.Record[2]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestActionableMessageExecuteAction()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "actionableMessage/executeAction",
                Value = JsonSerializer.SerializeToElement(new O365ConnectorCardActionQuery()),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsO365ConnectorCardActionAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestComposeExtensionQueryLink()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/queryLink",
                Value = JsonSerializer.SerializeToElement(new AppBasedLinkQuery()),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsAppBasedLinkQueryAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestComposeExtensionAnonymousQueryLink()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/anonymousQueryLink",
                Value = JsonSerializer.SerializeToElement(new AppBasedLinkQuery()),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsAnonymousAppBasedLinkQueryAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestComposeExtensionQuery()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/query",
                Value = JsonSerializer.SerializeToElement(new MessagingExtensionQuery()),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await ((IAgent)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMessagingExtensionQueryAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestMessagingExtensionSelectItemAsync()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/selectItem",
                Value = new JsonElement(),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMessagingExtensionSelectItemAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestMessagingExtensionSubmitAction()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/submitAction",
                Value = JsonSerializer.SerializeToElement(new MessagingExtensionQuery()),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(3, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMessagingExtensionSubmitActionDispatchAsync", bot.Record[1]);
            Assert.Equal("OnTeamsMessagingExtensionSubmitActionAsync", bot.Record[2]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestMessagingExtensionSubmitActionPreviewActionEdit()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/submitAction",
                Value = JsonSerializer.SerializeToElement(new MessagingExtensionAction
                {
                    BotMessagePreviewAction = "edit",
                }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(3, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMessagingExtensionSubmitActionDispatchAsync", bot.Record[1]);
            Assert.Equal("OnTeamsMessagingExtensionAgentMessagePreviewEditAsync", bot.Record[2]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestMessagingExtensionSubmitActionPreviewActionSend()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/submitAction",
                Value = JsonSerializer.SerializeToElement(new MessagingExtensionAction
                {
                    BotMessagePreviewAction = "send",
                }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(3, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMessagingExtensionSubmitActionDispatchAsync", bot.Record[1]);
            Assert.Equal("OnTeamsMessagingExtensionAgentMessagePreviewSendAsync", bot.Record[2]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestMessagingExtensionFetchTask()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/fetchTask",
                Value = JsonSerializer.SerializeToElement(new { commandId = "testCommand" }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMessagingExtensionFetchTaskAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestMessagingExtensionConfigurationQuerySettingUrl()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/querySettingUrl",
                Value = JsonSerializer.SerializeToElement(new { commandId = "testCommand" }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMessagingExtensionConfigurationQuerySettingUrlAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestMessagingExtensionConfigurationSetting()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/setting",
                Value = JsonSerializer.SerializeToElement(new { commandId = "testCommand" }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMessagingExtensionConfigurationSettingAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestTaskModuleFetch()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "task/fetch",
                Value = JsonSerializer.SerializeToElement(new
                {
                    data = new
                    {
                        key = "value",
                        type = "task / fetch",
                    },
                    context = new
                    {
                        theme = "default"
                    }
                }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await ((IAgent)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsTaskModuleFetchAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestTaskModuleSubmit()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "task/submit",
                Value = JsonSerializer.SerializeToElement(new
                {
                    data = new
                    {
                        key = "value",
                        type = "task / fetch",
                    },
                    context = new
                    {
                        theme = "default"
                    }
                }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsTaskModuleSubmitAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestTabFetch()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "tab/fetch",
                Value = JsonSerializer.SerializeToElement(new
                {
                    data = new
                    {
                        key = "value",
                        type = "task / fetch",
                    },
                    context = new
                    {
                        theme = "default"
                    }
                }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await ((IAgent)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsTabFetchAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestTabSubmit()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "tab/submit",
                Value = JsonSerializer.SerializeToElement(new
                {
                    data = new
                    {
                        key = "value",
                        type = "tab / submit",
                    },
                    context = new
                    {
                        theme = "default"
                    }
                }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsTabSubmitAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestConfigFetch()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "config/fetch",
                Value = JsonSerializer.SerializeToElement(new
                {
                    data = new
                    {
                        key = "value",
                        type = "config / fetch",
                    },
                    context = new
                    {
                        theme = "default"
                    }
                }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsConfigFetchAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestConfigSubmit()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "config/submit",
                Value = JsonSerializer.SerializeToElement(new
                {
                    data = new
                    {
                        key = "value",
                        type = "config / submit",
                    },
                    context = new
                    {
                        theme = "default"
                    }
                }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsConfigSubmitAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestSigninVerifyState()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "signin/verifyState",
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsSigninVerifyStateAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestOnEventActivity()
        {
            // Arrange
            var activity = new Activity
            {
                ChannelId = Channels.Directline,
                Type = ActivityTypes.Event
            };

            var turnContext = new TurnContext(new SimpleAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Single(bot.Record);
            Assert.Equal("OnEventActivityAsync", bot.Record[0]);
        }

        [Fact]
        public async Task TestMeetingStartEvent()
        {
            // Arrange
            var startTimeBase = new DateTime(2024, 6, 5, 0, 1, 2);
            var activity = new Activity
            {
                ChannelId = Channels.Msteams,
                Type = ActivityTypes.Event,
                Name = "application/vnd.microsoft.meetingStart",
                Value = JsonSerializer.SerializeToElement(new
                {
                    StartTime = startTimeBase.ToString("o", CultureInfo.InvariantCulture) // "2025-06-05T00:01:02.0Z"
                }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnEventActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMeetingStartAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.Contains(startTimeBase.ToString(CultureInfo.InvariantCulture), _activitiesToSend[0].Text); // Date format differs between OSs, so we just Assert.Contains instead of Assert.Equals
        }

        [Fact]
        public async Task TestMeetingEndEvent()
        {
            // Arrange
            var endTimeBase = new DateTime(2024, 6, 5, 0, 1, 2);
            var activity = new Activity
            {
                ChannelId = Channels.Msteams,
                Type = ActivityTypes.Event,
                Name = "application/vnd.microsoft.meetingEnd",
                Value = JsonSerializer.SerializeToElement(new
                {
                    EndTime = endTimeBase.ToString("o", CultureInfo.InvariantCulture) //"2021-06-05T01:02:03.0Z"
                }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnEventActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMeetingEndAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.Contains(endTimeBase.ToString(CultureInfo.InvariantCulture), _activitiesToSend[0].Text); // Date format differs between OSs, so we just Assert.Contains instead of Assert.Equals
        }

        [Fact]
        public async Task TeamsReadReceiptEvent()
        {
            // Arrange
            var activity = new Activity
            {
                ChannelId = Channels.Msteams,
                Type = ActivityTypes.Event,
                Name = "application/vnd.microsoft.readReceipt",
                Value = JsonSerializer.SerializeToElement(new
                {
                    lastReadMessageId = "10101010"
                }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnEventActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsReadReceiptAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.Equal("10101010", _activitiesToSend[0].Text);
        }

        [Fact]
        public async Task TestMeetingParticipantsJoinEvent()
        {
            // Arrange
            var activity = new Activity
            {
                ChannelId = Channels.Msteams,
                Type = ActivityTypes.Event,
                Name = "application/vnd.microsoft.meetingParticipantJoin",
                Value = JsonSerializer.SerializeToElement(
                    new MeetingParticipantsEventDetails
                    {
                        Members =
                        [
                            new TeamsMeetingMember(
                                new TeamsChannelAccount { Id = "id", Name = "name"},
                                new UserMeetingDetails { Role = "role", InMeeting = true }
                            )
                        ]
                    }
                ),
            };

            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await ((IAgent)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnEventActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMeetingParticipantsJoinAsync", bot.Record[1]);
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("id", activitiesToSend[0].Text);
        }

        [Fact]
        public async Task TestMeetingParticipantsLeaveEvent()
        {
            // Arrange
            var activity = new Activity
            {
                ChannelId = Channels.Msteams,
                Type = ActivityTypes.Event,
                Name = "application/vnd.microsoft.meetingParticipantLeave",
                Value = JsonSerializer.SerializeToElement(
                    new MeetingParticipantsEventDetails
                    {
                        Members =
                        [
                            new TeamsMeetingMember(
                                new TeamsChannelAccount { Id = "id", Name = "name"},
                                new UserMeetingDetails { Role = "role", InMeeting = true }
                            )
                        ]
                    }
                ),
            };

            _activitiesToSend = null;

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await ((IAgent)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnEventActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMeetingParticipantsLeaveAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.Equal("id", _activitiesToSend[0].Text);
        }

        [Fact]
        public async Task TestMessageUpdateActivityTeamsMessageEdit()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.MessageUpdate,
                ChannelData = new TeamsChannelData { EventType = "editMessage" },
                ChannelId = Channels.Msteams,
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await ((IAgent)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnMessageUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMessageEditAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestMessageUpdateActivityTeamsMessageUndelete()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.MessageUpdate,
                ChannelData = new TeamsChannelData { EventType = "undeleteMessage" },
                ChannelId = Channels.Msteams,
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await ((IAgent)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnMessageUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMessageUndeleteAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestMessageUpdateActivityTeamsMessageUndelete_NoMsteams()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.MessageUpdate,
                ChannelData = new TeamsChannelData { EventType = "undeleteMessage" },
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await ((IAgent)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.Single(bot.Record);
            Assert.Equal("OnMessageUpdateActivityAsync", bot.Record[0]);
        }

        [Fact]
        public async Task TestMessageUpdateActivityTeams_NoChannelData()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.MessageUpdate,
                ChannelId = Channels.Msteams,
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await ((IAgent)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.Single(bot.Record);
            Assert.Equal("OnMessageUpdateActivityAsync", bot.Record[0]);
        }

        [Fact]
        public async Task TestMessageDeleteActivityTeamsMessageSoftDelete()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.MessageDelete,
                ChannelData = new TeamsChannelData { EventType = "softDeleteMessage" },
                ChannelId = Channels.Msteams
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await ((IAgent)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnMessageDeleteActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMessageSoftDeleteAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestMessageDeleteActivityTeamsMessageSoftDelete_NoMsteams()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.MessageDelete,
                ChannelData = new TeamsChannelData { EventType = "softMessage" }
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await ((IAgent)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.Single(bot.Record);
            Assert.Equal("OnMessageDeleteActivityAsync", bot.Record[0]);
        }

        [Fact]
        public async Task TestMessageDeleteActivityTeams_NoChannelData()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.MessageDelete,
                ChannelId = Channels.Msteams,
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await ((IAgent)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.Single(bot.Record);
            Assert.Equal("OnMessageDeleteActivityAsync", bot.Record[0]);
        }

        private class RosterHttpMessageHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);

                // GetMembers (Team)
                if (request.RequestUri.PathAndQuery.EndsWith("team-id/members"))
                {
                    var content = new TeamsPagedMembersResult
                    {
                        Members =
                        [
                            new TeamsChannelAccount
                            {
                                Id = "id-1",
                                Name = "name-1",
                                GivenName = "givenName-1",
                                Surname = "surname-1",
                                Email = "email-1",
                                UserPrincipalName = "userPrincipalName-1",
                                UserRole = "userRole-1",
                                TenantId = "tenantId-1",
                            },
                            new TeamsChannelAccount
                            {
                                Id = "id-2",
                                Name = "name-2",
                                GivenName = "givenName-2",
                                Surname = "surname-2",
                                Email = "email-2",
                                UserPrincipalName = "userPrincipalName-2",
                                UserRole = "userRole-2",
                                TenantId = "tenantId-2",
                            },
                        ]
                    };
                    response.Content = new StringContent(JsonSerializer.Serialize(content));
                }

                // GetMembers (Group Chat)
                else if (request.RequestUri.PathAndQuery.EndsWith("conversation-id/members"))
                {
                    var content = new TeamsPagedMembersResult
                    {
                        Members =
                        [
                            new TeamsChannelAccount
                            {
                                Id = "id-3",
                                Name = "name-3",
                                GivenName = "givenName-3",
                                Surname = "surname-3",
                                Email = "email-3",
                                UserPrincipalName = "userPrincipalName-3",
                                UserRole = "userRole-3",
                                TenantId = "tenantId-3",
                            },
                            new TeamsChannelAccount
                            {
                                Id = "id-4",
                                Name = "name-4",
                                GivenName = "givenName-4",
                                Surname = "surname-4",
                                Email = "email-4",
                                UserPrincipalName = "userPrincipalName-4",
                                UserRole = "userRole-4",
                                TenantId = "tenantId-4",
                            },
                        ]
                    };
                    response.Content = new StringContent(JsonSerializer.Serialize(content));
                }
                else if (request.RequestUri.PathAndQuery.EndsWith("team-id/members/id-1") || request.RequestUri.PathAndQuery.EndsWith("conversation-id/members/id-1"))
                {
                    var content = new
                    {
                        Id = "id-1",
                        ObjectId = "objectId-1",
                        Name = "name-1",
                        GivenName = "givenName-1",
                        Surname = "surname-1",
                        Email = "email-1",
                        UserPrincipalName = "userPrincipalName-1",
                        TenantId = "tenantId-1",
                    };
                    response.Content = new StringContent(JsonSerializer.Serialize(content));
                }

                return Task.FromResult(response);
            }
        }
    }
}
