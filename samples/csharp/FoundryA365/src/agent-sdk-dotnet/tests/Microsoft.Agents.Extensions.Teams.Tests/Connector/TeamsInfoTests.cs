// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Compat;
using Microsoft.Agents.Extensions.Teams.Connector;
using Microsoft.Agents.Extensions.Teams.Models;
using Microsoft.Agents.Extensions.Teams.Tests.Model;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Connector
{
    public class TeamsInfoTests
    {
        private const string ExpectedActivityId = "activity-id";
        private const string ExpectedTeamsChannelId = "teams-channel-id";
        private const string Endpoint = "https://test.coffee";

        private readonly TestConnector _connectorClient;

        public TeamsInfoTests()
        {
            _connectorClient = new TestConnector(new Uri(Endpoint));
        }

        [Fact]
        public async Task SendMessageToTeamsChannelAsync_ShouldReturnConversationReferenceUsingAdapter()
        {
            var expectedConversationId = "conversation-id";
            var activity = CreateTestActivity("Test-SendMessageToTeamsChannelAsync");
            var adapter = new TestCreateConversationAdapter(ExpectedActivityId, expectedConversationId);
            var turnContext = new TurnContext(adapter, activity);
            turnContext.Services.Set<IConnectorClient>(_connectorClient);
            turnContext.Activity.ServiceUrl = "https://test.coffee";
            var handler = new TestTeamsActivityHandler();

            await handler.OnTurnAsync(turnContext);
        }

        [Fact]
        public async Task SendMessageToTeamsChannelAsync_ShouldReturnConversationReference()
        {
            var expectedAppId = "app-id";
            var expectedServiceUrl = "service-url";
            var expectedConversationId = "conversation-id";
            var requestActivity = new Activity { ServiceUrl = expectedServiceUrl };
            var adapter = new TestCreateConversationAdapter(ExpectedActivityId, expectedConversationId);

            var turnContextMock = new Mock<ITurnContext>();
            turnContextMock.Setup(tc => tc.Activity).Returns(requestActivity);
            turnContextMock.Setup(tc => tc.Adapter).Returns(adapter);

            var activity = CreateTestActivity("Test-SendMessageToTeamsChannelAsync");

            var reference = await TeamsInfo.SendMessageToTeamsChannelAsync(turnContextMock.Object, activity, ExpectedTeamsChannelId, expectedAppId, CancellationToken.None);

            Assert.Equal(expectedConversationId, reference.Item1.Conversation.Id);
            Assert.Equal(ExpectedActivityId, reference.Item2);
            Assert.Equal(expectedAppId, adapter.AppId);
            Assert.Equal(Channels.Msteams, adapter.ChannelId);
            Assert.Equal(expectedServiceUrl, adapter.ServiceUrl);
            Assert.Null(adapter.Audience);

            var adapterChannelData = adapter.ConversationParameters.ChannelData;
            var channel = adapterChannelData.GetType().GetProperty("Channel").GetValue(adapterChannelData, null);
            var id = channel.GetType().GetProperty("Id").GetValue(channel, null);

            Assert.Equal(ExpectedTeamsChannelId, id);
            Assert.Equal(adapter.ConversationParameters.Activity, activity);
        }

        [Fact]
        public async Task GetMeetingInfoAsync_ShouldReturnMeetingInfo()
        {
            var channelData = new TeamsChannelData
            {
                Meeting = new TeamsMeetingInfo
                {
                    Id = "meeting-id"
                }
            };
            var activity = CreateTestActivity("Test-GetMeetingInfoAsync", channelData);

            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);
            turnContext.Services.Set<IConnectorClient>(_connectorClient);
            var handler = new TestTeamsActivityHandler();

            await handler.OnTurnAsync(turnContext);
        }

        [Fact]
        public async Task GetTeamDetailsAsync_ShouldReturnTeamDetails()
        {
            var activity = CreateTestActivity("Test-GetTeamDetailsAsync");
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);
            turnContext.Services.Set<IConnectorClient>(_connectorClient);
            var handler = new TestTeamsActivityHandler();

            await handler.OnTurnAsync(turnContext);
        }

        [Fact]
        public async Task GetPagedTeamMembersAsync_ShouldReturnTeamMembersList()
        {
            var activity = CreateTestActivity("Test-TeamGetPagedTeamMembersAsync");
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);
            turnContext.Services.Set<IConnectorClient>(_connectorClient);
            var handler = new TestTeamsActivityHandler();

            await handler.OnTurnAsync(turnContext);
        }

        [Fact]
        public async Task GetPagedMembersAsync_ShouldReturnGroupChatMembersList()
        {
            var channelData = new TeamsChannelData
            {
                Team = new TeamInfo(),
            };
            var activity = CreateTestActivity("Test-GroupChat-GetPagedMembersAsync", channelData);
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);
            turnContext.Services.Set<IConnectorClient>(_connectorClient);
            var handler = new TestTeamsActivityHandler();

            await handler.OnTurnAsync(turnContext);
        }

        [Fact]
        public async Task GetChannelsAsync_ShouldReturnChannelsList()
        {
            var activity = CreateTestActivity("Test-GetChannelsAsync");
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);
            turnContext.Services.Set<IConnectorClient>(_connectorClient);
            var handler = new TestTeamsActivityHandler();

            await handler.OnTurnAsync(turnContext);
        }

        [Fact]
        public async Task GetMeetingParticipantAsync_ShouldReturnMeetingParticipantInfo()
        {
            var channelData = new TeamsChannelData
            {
                Meeting = new TeamsMeetingInfo { Id = "meetingId-1" },
                Tenant = new TenantInfo { Id = "tenantId-1" },
            };
            var activity = CreateTestActivity("Test-GetParticipantAsync", channelData);
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);
            turnContext.Services.Set<IConnectorClient>(_connectorClient);
            var handler = new TestTeamsActivityHandler();

            await handler.OnTurnAsync(turnContext);
        }

        [Fact]
        public async Task GetMemberAsync_ShouldReturnAccountInfo()
        {
            var activity = CreateTestActivity("Test-GetMemberAsync");
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);
            turnContext.Services.Set<IConnectorClient>(_connectorClient);
            var handler = new TestTeamsActivityHandler();

            await handler.OnTurnAsync(turnContext);
        }

        [Fact]
        public async Task GetMemberAsync_ShouldReturnAccountInfoWhenNoTeamId()
        {
            var channelData = new TeamsChannelData
            {
                Team = new TeamInfo(),
            };
            var activity = CreateTestActivity("Test-GetMemberAsync", channelData);
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);
            turnContext.Services.Set<IConnectorClient>(_connectorClient);
            var handler = new TestTeamsActivityHandler();

            await handler.OnTurnAsync(turnContext);
        }

        [Theory]
        [InlineData("202")]
        [InlineData("207")]
        [InlineData("400")]
        [InlineData("403")]
        public async Task SendMeetingNotificationAsync_ShouldReturnExpectedStatusCode(string statusCode)
        {
            //     202: accepted
            //     207: if the notifications are sent only to parital number of recipients because 
            //                 the validation on some recipients’ ids failed or some recipients were not found in the roster. 
            //             • In this case, SMBA will return the user MRIs of those failed recipients in a format that was given to a bot 
            //                 (ex: if a bot sent encrypted user MRIs, return encrypted one).

            //     400: when Meeting Notification request payload validation fails. For instance, 
            //         • Recipients: # of recipients is greater than what the API allows || all of recipients’ user ids were invalid
            //         • Surface: 
            //             o Surface list is empty or null 
            //             o Surface type is invalid 
            //             o Duplicative surface type exists in one payload
            //     403: if the bot is not allowed to send the notification.
            //         In this case, the payload should contain more detail error message.
            //         There can be many reasons: bot disabled by tenant admin, blocked during live site mitigation,
            //         the bot does not have a correct RSC permission for a specific surface type, etc
            var activity = new Activity
            {
                Type = "targetedMeetingNotification",
                Text = "Test-SendMeetingNotificationAsync",
                ChannelId = Channels.Msteams,
                ServiceUrl = "https://test.coffee",
                From = new ChannelAccount()
                {
                    Id = "id-1",

                    //Hack for test.use the Name field to pass expected status code to test code
                    Name = statusCode
                },
                Conversation = new ConversationAccount() { Id = "conversation-id" }
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);
            turnContext.Services.Set<IConnectorClient>(_connectorClient);
            var handler = new TestTeamsActivityHandler();

            await handler.OnTurnAsync(turnContext);
        }

        [Theory]
        [InlineData("201")]
        [InlineData("400")]
        [InlineData("403")]
        [InlineData("429")]
        public async Task SendMessageToListOfUsersAsync_ShouldReturnExpectedStatusCode(string statusCode)
        {
            // 201: created
            // 400: when send message to list of users request payload validation fails.
            // 403: if the bot is not allowed to send messages.
            // 429: too many requests for throttled requests.
            var activity = new Activity
            {
                Type = "message",
                Text = "Test-SendMessageToListOfUsersAsync",
                ChannelId = Channels.Msteams,
                ServiceUrl = "https://test.coffee",
                From = new ChannelAccount()
                {
                    Id = "id-1",

                    // Hack for test. use the Name field to pass expected status code to test code
                    Name = statusCode
                },
                Conversation = new ConversationAccount() { Id = "conversation-id" }
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);
            turnContext.Services.Set<IConnectorClient>(_connectorClient);
            var handler = new TestTeamsActivityHandler();

            await handler.OnTurnAsync(turnContext);
        }

        [Theory]
        [InlineData("201")]
        [InlineData("400")]
        [InlineData("403")]
        [InlineData("429")]
        public async Task SendMessageToAllUsersInTenantAsync_ShouldReturnExpectedStatusCode(string statusCode)
        {
            // 201: created
            // 400: when send message to list of users request payload validation fails.
            // 403: if the bot is not allowed to send messages.
            // 429: too many requests for throttled requests.
            var activity = new Activity
            {
                Type = "message",
                Text = "Test-SendMessageToAllUsersInTenantAsync",
                ChannelId = Channels.Msteams,
                ServiceUrl = "https://test.coffee",
                From = new ChannelAccount()
                {
                    Id = "id-1",

                    // Hack for test. use the Name field to pass expected status code to test code
                    Name = statusCode
                },
                Conversation = new ConversationAccount() { Id = "conversation-id" }
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);
            turnContext.Services.Set<IConnectorClient>(_connectorClient);
            var handler = new TestTeamsActivityHandler();

            await handler.OnTurnAsync(turnContext);
        }

        [Theory]
        [InlineData("201")]
        [InlineData("400")]
        [InlineData("403")]
        [InlineData("404")]
        [InlineData("429")]
        public async Task SendMessageToAllUsersInTeamAsync_ShouldReturnExpectedStatusCode(string statusCode)
        {
            // 201: created
            // 400: when send message to list of users request payload validation fails.
            // 403: if the bot is not allowed to send messages.
            // 404: when Team is not found.
            // 429: too many requests for throttled requests.
            var activity = new Activity
            {
                Type = "message",
                Text = "Test-SendMessageToAllUsersInTeamAsync",
                ChannelId = Channels.Msteams,
                ServiceUrl = "https://test.coffee",
                From = new ChannelAccount()
                {
                    Id = "id-1",

                    // Hack for test. use the Name field to pass expected status code to test code
                    Name = statusCode
                },
                Conversation = new ConversationAccount() { Id = "conversation-id" }
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);
            turnContext.Services.Set<IConnectorClient>(_connectorClient);
            var handler = new TestTeamsActivityHandler();

            await handler.OnTurnAsync(turnContext);
        }

        [Theory]
        [InlineData("201")]
        [InlineData("400")]
        [InlineData("403")]
        [InlineData("429")]
        public async Task SendMessageToListOfChannelsAsync_ShouldReturnExpectedStatusCode(string statusCode)
        {
            // 201: created
            // 400: when send message to list of channels request payload validation fails.
            // 403: if the bot is not allowed to send messages.
            // 429: too many requests for throttled requests.
            var activity = new Activity
            {
                Type = "message",
                Text = "Test-SendMessageToListOfChannelsAsync",
                ChannelId = Channels.Msteams,
                ServiceUrl = "https://test.coffee",
                From = new ChannelAccount()
                {
                    Id = "id-1",

                    // Hack for test. use the Name field to pass expected status code to test code
                    Name = statusCode
                },
                Conversation = new ConversationAccount() { Id = "conversation-id" }
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);
            turnContext.Services.Set<IConnectorClient>(_connectorClient);
            var handler = new TestTeamsActivityHandler();

            await handler.OnTurnAsync(turnContext);
        }

        [Theory]
        [InlineData("200")]
        [InlineData("400")]
        [InlineData("429")]
        public async Task GetOperationStateAsync_ShouldReturnExpectedStatusCode(string statusCode)
        {
            // 200: ok
            // 400: for requests with invalid operationId (Which should be of type GUID).
            // 429: too many requests for throttled requests.
            var activity = new Activity
            {
                Type = "message",
                Text = "Test-GetOperationStateAsync",
                ChannelId = Channels.Msteams,
                ServiceUrl = "https://test.coffee",
                From = new ChannelAccount()
                {
                    Id = "id-1",

                    // Hack for test. use the Name field to pass expected status code to test code
                    Name = statusCode
                },
                Conversation = new ConversationAccount() { Id = "conversation-id" }
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);
            turnContext.Services.Set<IConnectorClient>(_connectorClient);
            var handler = new TestTeamsActivityHandler();

            await handler.OnTurnAsync(turnContext);
        }

        [Theory]
        [InlineData("200")]
        [InlineData("400")]
        [InlineData("429")]
        public async Task GetPagedFailedEntriesAsync_ShouldReturnExpectedStatusCode(string statusCode)
        {
            // 200: ok
            // 400: for requests with invalid operationId (Which should be of type GUID).
            // 429: too many requests for throttled requests.
            var activity = new Activity
            {
                Type = "message",
                Text = "Test-GetPagedFailedEntriesAsync",
                ChannelId = Channels.Msteams,
                ServiceUrl = "https://test.coffee",
                From = new ChannelAccount()
                {
                    Id = "id-1",

                    // Hack for test. use the Name field to pass expected status code to test code
                    Name = statusCode
                },
                Conversation = new ConversationAccount() { Id = "conversation-id" }
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);
            turnContext.Services.Set<IConnectorClient>(_connectorClient);
            var handler = new TestTeamsActivityHandler();

            await handler.OnTurnAsync(turnContext);
        }

        [Theory]
        [InlineData("200")]
        [InlineData("400")]
        [InlineData("429")]
        public async Task CancelOperationAsync_ShouldReturnExpectedStatusCode(string statusCode)
        {
            // 200: Ok for successful cancelled operations (Operations in state completed, or failed will not change state to cancel but still return 200)
            // 400: for requests with invalid operationId (Which should be of type GUID).
            // 429: too many requests for throttled requests.
            var activity = new Activity
            {
                Type = "message",
                Text = "Test-CancelOperationAsync",
                ChannelId = Channels.Msteams,
                ServiceUrl = "https://test.coffee",
                From = new ChannelAccount()
                {
                    Id = "id-1",

                    // Hack for test. use the Name field to pass expected status code to test code
                    Name = statusCode
                },
                Conversation = new ConversationAccount() { Id = "conversation-id" }
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);
            turnContext.Services.Set<IConnectorClient>(_connectorClient);
            var handler = new TestTeamsActivityHandler();

            await handler.OnTurnAsync(turnContext);
        }

        private static Activity CreateTestActivity(string text, object channelData = null)
        {
            var activityChannelData = channelData;

            activityChannelData ??= new TeamsChannelData
            {
                Team = new TeamInfo { Id = "team-id" },
            };

            return new Activity
            {
                Type = "message",
                Text = text,
                ChannelId = Channels.Msteams,
                Conversation = new ConversationAccount { Id = "conversation-id" },
                From = new ChannelAccount { Id = "id-1", AadObjectId = "participantId-1" },
                ServiceUrl = "https://test.coffee",
                ChannelData = activityChannelData,
            };
        }

        private class TestTeamsActivityHandler : TeamsActivityHandler
        {
            public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
            {
                await base.OnTurnAsync(turnContext, cancellationToken);

                switch (turnContext.Activity.Text)
                {
                    case "Test-GetTeamDetailsAsync":
                        await CallGetTeamDetailsAsync(turnContext);
                        break;
                    case "Test-TeamGetPagedTeamMembersAsync":
                        await CallTeamGetPagedTeamMembersAsync(turnContext);
                        break;
                    case "Test-GroupChat-GetPagedMembersAsync":
                        await CallGroupChatGetPagedMembersAsync(turnContext);
                        break;
                    case "Test-GetChannelsAsync":
                        await CallGetChannelsAsync(turnContext);
                        break;
                    case "Test-SendMessageToTeamsChannelAsync":
                        await CallSendMessageToTeamsChannelAsync(turnContext);
                        break;
                    case "Test-GetMemberAsync":
                        await CallTeamGetMemberAsync(turnContext);
                        break;
                    case "Test-GetParticipantAsync":
                        await CallTeamsInfoGetParticipantAsync(turnContext);
                        break;
                    case "Test-GetMeetingInfoAsync":
                        await CallTeamsInfoGetMeetingInfoAsync(turnContext);
                        break;
                    case "Test-SendMeetingNotificationAsync":
                        await CallSendMeetingNotificationAsync(turnContext);
                        break;
                    case "Test-SendMessageToListOfUsersAsync":
                        await CallSendMessageToListOfUsersAsync(turnContext);
                        break;
                    case "Test-SendMessageToAllUsersInTenantAsync":
                        await CallSendMessageToAllUsersInTenantAsync(turnContext);
                        break;
                    case "Test-SendMessageToAllUsersInTeamAsync":
                        await CallSendMessageToAllUsersInTeamAsync(turnContext);
                        break;
                    case "Test-SendMessageToListOfChannelsAsync":
                        await CallSendMessageToListOfChannelsAsync(turnContext);
                        break;
                    case "Test-GetOperationStateAsync":
                        await CallGetOperationStateAsync(turnContext);
                        break;
                    case "Test-GetPagedFailedEntriesAsync":
                        await CallGetPagedFailedEntriesAsync(turnContext);
                        break;
                    case "Test-CancelOperationAsync":
                        await CallCancelOperationAsync(turnContext);
                        break;
                    default:
                        Assert.True(false);
                        break;
                }
            }

            private static async Task CallSendMessageToTeamsChannelAsync(ITurnContext turnContext)
            {
                var message = MessageFactory.Text("hi");
                var channelId = Channels.Msteams;
                var appId = "app-id";
                var cancelToken = new CancellationToken();

                var reference = await TeamsInfo.SendMessageToTeamsChannelAsync(turnContext, message, channelId, appId, cancelToken);

                Assert.Equal(ExpectedActivityId, reference.Item1.ActivityId);
                Assert.Equal(channelId, reference.Item1.ChannelId);
                Assert.Equal(turnContext.Activity.ServiceUrl, reference.Item1.ServiceUrl);
                Assert.Equal(ExpectedActivityId, reference.Item2);
            }

            private static async Task CallGetTeamDetailsAsync(ITurnContext turnContext)
            {
                var teamDetails = await TeamsInfo.GetTeamDetailsAsync(turnContext);

                Assert.Equal("team-id", teamDetails.Id);
                Assert.Equal("team-name", teamDetails.Name);
                Assert.Equal("team-aadgroupid", teamDetails.AadGroupId);
            }

            private static async Task CallTeamGetPagedTeamMembersAsync(ITurnContext turnContext)
            {
                var teamsMembers = await TeamsInfo.GetPagedTeamMembersAsync(turnContext);

                Assert.Equal("id-1", teamsMembers.Members[0].Id);
                Assert.Equal("name-1", teamsMembers.Members[0].Name);
                Assert.Equal("givenName-1", teamsMembers.Members[0].GivenName);
                Assert.Equal("surname-1", teamsMembers.Members[0].Surname);
                Assert.Equal("userPrincipalName-1", teamsMembers.Members[0].UserPrincipalName);

                Assert.Equal("id-2", teamsMembers.Members[1].Id);
                Assert.Equal("name-2", teamsMembers.Members[1].Name);
                Assert.Equal("givenName-2", teamsMembers.Members[1].GivenName);
                Assert.Equal("surname-2", teamsMembers.Members[1].Surname);
                Assert.Equal("userPrincipalName-2", teamsMembers.Members[1].UserPrincipalName);
            }

            private static async Task CallTeamGetMemberAsync(ITurnContext turnContext)
            {
                var member = await TeamsInfo.GetMemberAsync(turnContext, turnContext.Activity.From.Id);

                Assert.Equal("id-1", member.Id);
                Assert.Equal("name-1", member.Name);
                Assert.Equal("givenName-1", member.GivenName);
                Assert.Equal("surname-1", member.Surname);
                Assert.Equal("userPrincipalName-1", member.UserPrincipalName);
            }

            private static async Task CallTeamsInfoGetParticipantAsync(ITurnContext turnContext)
            {
                var participant = await TeamsInfo.GetMeetingParticipantAsync(turnContext);

                Assert.Equal("Organizer", participant.Meeting.Role);
                Assert.Equal("meetigConversationId-1", participant.Conversation.Id);
                Assert.Equal("userPrincipalName-1", participant.User.UserPrincipalName);
            }

            private static async Task CallTeamsInfoGetMeetingInfoAsync(ITurnContext turnContext)
            {
                var meeting = await TeamsInfo.GetMeetingInfoAsync(turnContext);

                Assert.Equal("meeting-id", meeting.Details.Id);
                Assert.Equal("organizer-id", meeting.Organizer.Id);
                Assert.Equal("meetingConversationId-1", meeting.Conversation.Id);
            }

            private static MeetingNotificationBase GetTargetedMeetingNotification(ChannelAccount from)
            {
                var recipients = new List<string> { from.Id };

                if (from.Name == "207")
                {
                    recipients.Add("failingid");
                }

                var meetingStageSurface = new MeetingStageSurface<TaskModuleContinueResponse>
                {
                    Content = new TaskModuleContinueResponse
                    {
                        Value = new TaskModuleTaskInfo
                        {
                            Title = "title here",
                            Height = 3,
                            Width = 2,
                        }
                    },
                    ContentType = ContentType.Task
                };

                var meetingTabIconSurface = new MeetingTabIconSurface
                {
                    TabEntityId = "test tab entity id"
                };

                var value = new TargetedMeetingNotificationValue
                {
                    Recipients = recipients,
                    Surfaces = [meetingStageSurface, meetingTabIconSurface]
                };

                var obo = new OnBehalfOf
                {
                    DisplayName = from.Name,
                    Mri = from.Id
                };

                var channelData = new MeetingNotificationChannelData
                {
                    OnBehalfOfList = [obo]
                };

                return new TargetedMeetingNotification
                {
                    Value = value,
                    ChannelData = channelData
                };
            }

            private async Task CallSendMeetingNotificationAsync(ITurnContext turnContext)
            {
                var from = turnContext.Activity.From;

                try
                {
                    var failedParticipants = await TeamsInfo.SendMeetingNotificationAsync(turnContext, GetTargetedMeetingNotification(from), "meeting-id").ConfigureAwait(false);

                    switch (from.Name)
                    {
                        case "207":
                            Assert.Equal("failingid", failedParticipants.RecipientsFailureInfo.First().RecipientMri);
                            break;
                        case "202":
                            Assert.Null(failedParticipants);
                            break;
                        default:
                            throw new InvalidOperationException($"Expected {nameof(ErrorResponseException)} with response status code {from.Name}.");
                    }
                }
                catch (ErrorResponseException ex)
                {
                    var errorResponse = ex.Body;

                    switch (from.Name)
                    {
                        case "400":
                            Assert.Equal("BadSyntax", ex.Body.Error.Code);
                            break;
                        case "403":
                            Assert.Equal("BotNotInConversationRoster", ex.Body.Error.Code);
                            break;
                        default:
                            throw new InvalidOperationException($"Expected {nameof(ErrorResponseException)} with response status code {from.Name}.");
                    }
                }
            }

            private static async Task CallGroupChatGetPagedMembersAsync(ITurnContext turnContext)
            {
                var teamsMembers = await TeamsInfo.GetPagedMembersAsync(turnContext);

                Assert.Equal("id-1", teamsMembers.Members[0].Id);
                Assert.Equal("name-1", teamsMembers.Members[0].Name);
                Assert.Equal("givenName-1", teamsMembers.Members[0].GivenName);
                Assert.Equal("surname-1", teamsMembers.Members[0].Surname);
                Assert.Equal("userPrincipalName-1", teamsMembers.Members[0].UserPrincipalName);

                Assert.Equal("id-2", teamsMembers.Members[1].Id);
                Assert.Equal("name-2", teamsMembers.Members[1].Name);
                Assert.Equal("givenName-2", teamsMembers.Members[1].GivenName);
                Assert.Equal("surname-2", teamsMembers.Members[1].Surname);
                Assert.Equal("userPrincipalName-2", teamsMembers.Members[1].UserPrincipalName);
            }

            private static async Task CallGetChannelsAsync(ITurnContext turnContext)
            {
                var channels = (await TeamsInfo.GetTeamChannelsAsync(turnContext)).ToArray();

                Assert.Equal("channel-id-1", channels[0].Id);
                Assert.Equal("channel-id-2", channels[1].Id);
                Assert.Equal("channel-name-2", channels[1].Name);
                Assert.Equal("channel-id-3", channels[2].Id);
                Assert.Equal("channel-name-3", channels[2].Name);
            }

            private async Task CallSendMessageToListOfUsersAsync(ITurnContext turnContext)
            {
                var from = turnContext.Activity.From;
                var members = new List<TeamMember>()
                {
                    new ("member-1"),
                    new ("member-2"),
                    new ("member-3"),
                };
                var tenantId = "tenant-id";

                try
                {
                    var operationId = await TeamsInfo.SendMessageToListOfUsersAsync(turnContext, turnContext.Activity, members, tenantId).ConfigureAwait(false);

                    switch (from.Name)
                    {
                        case "201":
                            Assert.Equal("operation-1", operationId);
                            break;
                        default:
                            throw new InvalidOperationException($"Expected {nameof(ErrorResponseException)} with response status code {from.Name}.");
                    }
                }
                catch (AggregateException ex)
                {
                    var firstException = ex.InnerExceptions.First();
                    ErrorResponseException httpException;
                    var errorResponse = new ErrorResponse();

                    switch (from.Name)
                    {
                        case "400":
                            Assert.Single(ex.InnerExceptions);
                            httpException = (ErrorResponseException)firstException;
                            errorResponse = httpException.Body;
                            Assert.Equal("BadSyntax", errorResponse.Error.Code);
                            break;
                        case "403":
                            Assert.Single(ex.InnerExceptions);
                            httpException = (ErrorResponseException)firstException;
                            errorResponse = httpException.Body;
                            Assert.Equal("Forbidden", errorResponse.Error.Code);
                            break;
                        case "429":
                            Assert.Equal(11, ex.InnerExceptions.Count);
                            break;
                        default:
                            throw new InvalidOperationException($"Expected {nameof(ErrorResponseException)} with response status code {from.Name}.");
                    }
                }
            }

            private async Task CallSendMessageToAllUsersInTenantAsync(ITurnContext turnContext)
            {
                var from = turnContext.Activity.From;
                var tenantId = "tenant-id";

                try
                {
                    var operationId = await TeamsInfo.SendMessageToAllUsersInTenantAsync(turnContext, turnContext.Activity, tenantId).ConfigureAwait(false);

                    switch (from.Name)
                    {
                        case "201":
                            Assert.Equal("operation-1", operationId);
                            break;
                        default:
                            throw new InvalidOperationException($"Expected {nameof(ErrorResponseException)} with response status code {from.Name}.");
                    }
                }
                catch (AggregateException ex)
                {
                    var firstException = ex.InnerExceptions.First();
                    ErrorResponseException httpException;
                    var errorResponse = new ErrorResponse();

                    switch (from.Name)
                    {
                        case "400":
                            Assert.Single(ex.InnerExceptions);
                            httpException = (ErrorResponseException)firstException;
                            errorResponse = httpException.Body;
                            Assert.Equal("BadSyntax", errorResponse.Error.Code);
                            break;
                        case "403":
                            Assert.Single(ex.InnerExceptions);
                            httpException = (ErrorResponseException)firstException;
                            errorResponse = httpException.Body;
                            Assert.Equal("Forbidden", errorResponse.Error.Code);
                            break;
                        case "429":
                            Assert.Equal(11, ex.InnerExceptions.Count);
                            break;
                        default:
                            throw new InvalidOperationException($"Expected {nameof(ErrorResponseException)} with response status code {from.Name}.");
                    }
                }
            }

            private async Task CallSendMessageToAllUsersInTeamAsync(ITurnContext turnContext)
            {
                var from = turnContext.Activity.From;
                var teamId = "team-id";
                var tenantId = "tenant-id";

                try
                {
                    var operationId = await TeamsInfo.SendMessageToAllUsersInTeamAsync(turnContext, turnContext.Activity, teamId, tenantId).ConfigureAwait(false);

                    switch (from.Name)
                    {
                        case "201":
                            Assert.Equal("operation-1", operationId);
                            break;
                        default:
                            throw new InvalidOperationException($"Expected {nameof(ErrorResponseException)} with response status code {from.Name}.");
                    }
                }
                catch (AggregateException ex)
                {
                    var firstException = ex.InnerExceptions.First();
                    ErrorResponseException httpException;
                    var errorResponse = new ErrorResponse();

                    switch (from.Name)
                    {
                        case "400":
                            Assert.Single(ex.InnerExceptions);
                            httpException = (ErrorResponseException)firstException;
                            errorResponse = httpException.Body;
                            Assert.Equal("BadSyntax", errorResponse.Error.Code);
                            break;
                        case "403":
                            Assert.Single(ex.InnerExceptions);
                            httpException = (ErrorResponseException)firstException;
                            errorResponse = httpException.Body;
                            Assert.Equal("Forbidden", errorResponse.Error.Code);
                            break;
                        case "404":
                            Assert.Single(ex.InnerExceptions);
                            httpException = (ErrorResponseException)firstException;
                            errorResponse = httpException.Body;
                            Assert.Equal("NotFound", errorResponse.Error.Code);
                            break;
                        case "429":
                            Assert.Equal(11, ex.InnerExceptions.Count);
                            break;
                        default:
                            throw new InvalidOperationException($"Expected {nameof(ErrorResponseException)} with response status code {from.Name}.");
                    }
                }
            }

            private async Task CallSendMessageToListOfChannelsAsync(ITurnContext turnContext)
            {
                var from = turnContext.Activity.From;
                var members = new List<TeamMember>()
                {
                    new ("channel-1"),
                    new ("channel-2"),
                    new ("channel-3"),
                };
                var tenantId = "tenant-id";

                try
                {
                    var operationId = await TeamsInfo.SendMessageToListOfChannelsAsync(turnContext, turnContext.Activity, members, tenantId).ConfigureAwait(false);

                    switch (from.Name)
                    {
                        case "201":
                            Assert.Equal("operation-1", operationId);
                            break;
                        default:
                            throw new InvalidOperationException($"Expected {nameof(ErrorResponseException)} with response status code {from.Name}.");
                    }
                }
                catch (AggregateException ex)
                {
                    var firstException = ex.InnerExceptions.First();
                    ErrorResponseException httpException;
                    var errorResponse = new ErrorResponse();

                    switch (from.Name)
                    {
                        case "400":
                            Assert.Single(ex.InnerExceptions);
                            httpException = (ErrorResponseException)firstException;
                            errorResponse = httpException.Body;
                            Assert.Equal("BadSyntax", errorResponse.Error.Code);
                            break;
                        case "403":
                            Assert.Single(ex.InnerExceptions);
                            httpException = (ErrorResponseException)firstException;
                            errorResponse = httpException.Body;
                            Assert.Equal("Forbidden", errorResponse.Error.Code);
                            break;
                        case "429":
                            Assert.Equal(11, ex.InnerExceptions.Count);
                            break;
                        default:
                            throw new InvalidOperationException($"Expected {nameof(ErrorResponseException)} with response status code {from.Name}.");
                    }
                }
            }

            private async Task CallGetOperationStateAsync(ITurnContext turnContext)
            {
                var from = turnContext.Activity.From.Name;
                var operationId = "operation-id*";
                var response = new BatchOperationState
                {
                    State = "state-1",
                    TotalEntriesCount = 1
                };
                response.StatusMap.Add(400, 1);

                try
                {
                    var operationResponse = await TeamsInfo.GetOperationStateAsync(turnContext, operationId + from).ConfigureAwait(false);

                    switch (from)
                    {
                        case "200":
                            Assert.Equal(response.ToString(), operationResponse.ToString());
                            break;
                        default:
                            throw new InvalidOperationException($"Expected {nameof(ErrorResponseException)} with response status code {from}.");
                    }
                }
                catch (AggregateException ex)
                {
                    var firstException = ex.InnerExceptions.First();
                    ErrorResponseException httpException;
                    var errorResponse = new ErrorResponse();

                    switch (from)
                    {
                        case "400":
                            Assert.Single(ex.InnerExceptions);
                            httpException = (ErrorResponseException)firstException;
                            errorResponse = httpException.Body;
                            Assert.Equal("BadSyntax", errorResponse.Error.Code);
                            break;
                        case "429":
                            Assert.Equal(11, ex.InnerExceptions.Count);
                            break;
                        default:
                            throw new InvalidOperationException($"Expected {nameof(ErrorResponseException)} with response status code {from}.");
                    }
                }
            }

            private async Task CallGetPagedFailedEntriesAsync(ITurnContext turnContext)
            {
                var from = turnContext.Activity.From.Name;
                var operationId = "operation-id*";
                var response = new BatchFailedEntriesResponse
                {
                    ContinuationToken = "continuation-token",
                };
                response.FailedEntries.Add(
                    new BatchFailedEntry
                    {
                        EntryId = "entry-1",
                        Error = "400 User not found"
                    });

                try
                {
                    var operationResponse = await TeamsInfo.GetPagedFailedEntriesAsync(turnContext, operationId + from).ConfigureAwait(false);

                    switch (from)
                    {
                        case "200":
                            Assert.Equal(response.ToString(), operationResponse.ToString());
                            break;
                        default:
                            throw new InvalidOperationException($"Expected {nameof(ErrorResponseException)} with response status code {from}.");
                    }
                }
                catch (AggregateException ex)
                {
                    var firstException = ex.InnerExceptions.First();
                    ErrorResponseException httpException;
                    var errorResponse = new ErrorResponse();
                    switch (from)
                    {
                        case "400":
                            Assert.Single(ex.InnerExceptions);
                            httpException = (ErrorResponseException)firstException;
                            errorResponse = httpException.Body;
                            Assert.Equal("BadSyntax", errorResponse.Error.Code);
                            break;
                        case "429":
                            Assert.Equal(11, ex.InnerExceptions.Count);
                            break;
                        default:
                            throw new InvalidOperationException($"Expected {nameof(ErrorResponseException)} with response status code {from}.");
                    }
                }
            }

            private async Task CallCancelOperationAsync(ITurnContext turnContext)
            {
                var from = turnContext.Activity.From.Name;
                var operationId = "operation-id*";
                AggregateException exception = null;

                try
                {
                    await TeamsInfo.CancelOperationAsync(turnContext, operationId + from).ConfigureAwait(false);

                    switch (from)
                    {
                        case "200":
                            Assert.Null(exception);
                            break;
                        default:
                            throw new InvalidOperationException($"Expected {nameof(ErrorResponseException)} with response status code {from}.");
                    }
                }
                catch (AggregateException ex)
                {
                    exception = ex;
                    var firstException = exception.InnerExceptions.First();
                    ErrorResponseException httpException;
                    var errorResponse = new ErrorResponse();
                    switch (from)
                    {
                        case "400":
                            Assert.Single(exception.InnerExceptions);
                            httpException = (ErrorResponseException)firstException;
                            errorResponse = httpException.Body;
                            Assert.Equal("BadSyntax", errorResponse.Error.Code);
                            break;
                        case "429":
                            Assert.Equal(11, exception.InnerExceptions.Count);
                            break;
                        default:
                            throw new InvalidOperationException($"Expected {nameof(ErrorResponseException)} with response status code {from}.");
                    }
                }
            }
        }

        private class RosterHttpMessageHandler : HttpMessageHandler
        {
            protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);

                // GetTeamDetails
                if (request.RequestUri.PathAndQuery.EndsWith("team-id"))
                {
                    var content = new
                    {
                        id = "team-id",
                        name = "team-name",
                        aadGroupId = "team-aadgroupid",
                    };
                    response.Content = new StringContent(ProtocolJsonSerializer.ToJson(content));
                }

                // SendMessageToThreadInTeams
                else if (request.RequestUri.PathAndQuery.EndsWith("v3/conversations"))
                {
                    var content = new
                    {
                        id = "id123",
                        serviceUrl = "https://serviceUrl/",
                        activityId = ExpectedActivityId,
                    };
                    response.Content = new StringContent(ProtocolJsonSerializer.ToJson(content));
                }

                // GetChannels
                else if (request.RequestUri.PathAndQuery.EndsWith("team-id/conversations"))
                {
                    var content = new ConversationList
                    {
                        Conversations =
                        [
                            new ChannelInfo { Id = "channel-id-1" },
                            new ChannelInfo { Id = "channel-id-2", Name = "channel-name-2" },
                            new ChannelInfo { Id = "channel-id-3", Name = "channel-name-3"  },
                        ],
                    };
                    response.Content = new StringContent(ProtocolJsonSerializer.ToJson(content));
                }

                // Get participant
                else if (request.RequestUri.PathAndQuery.EndsWith("v1/meetings/meetingId-1/participants/participantId-1?tenantId=tenantId-1"))
                {
                    var content = new
                    {
                        user = new { userPrincipalName = "userPrincipalName-1" },
                        meeting = new { role = "Organizer" },
                        conversation = new { Id = "meetigConversationId-1" },
                    };
                    response.Content = new StringContent(ProtocolJsonSerializer.ToJson(content));
                }

                // Get meeting details
                else if (request.RequestUri.PathAndQuery.EndsWith("v1/meetings/meeting-id"))
                {
                    var content = new
                    {
                        details = new { id = "meeting-id" },
                        organizer = new { id = "organizer-id" },
                        conversation = new { id = "meetingConversationId-1" },
                    };
                    response.Content = new StringContent(ProtocolJsonSerializer.ToJson(content));
                }

                // SendMeetingNotification
                else if (request.RequestUri.PathAndQuery.EndsWith("v1/meetings/meeting-id/notification"))
                {
                    var requestBody = await request.Content.ReadAsStringAsync().ConfigureAwait(false);

                    var payload = JsonDocument.Parse(requestBody);
                    JsonElement root = payload.RootElement;
                    var value = root.GetProperty("value");
                    var recipients = ProtocolJsonSerializer.ToObject<List<string>>(value.GetProperty("recipients"));
                    var channelData = ProtocolJsonSerializer.ToObject< MeetingNotificationChannelData>(root.GetProperty("channelData"));
                    var obo = channelData.OnBehalfOfList.First();

                    // hack displayname as expected status code, for testing
                    switch (obo.DisplayName)
                    {
                        case "207":
                            var failureInfo = new MeetingNotificationRecipientFailureInfo { RecipientMri = recipients.First(r => !r.Equals(obo.Mri, StringComparison.OrdinalIgnoreCase)) };
                            var infos = new MeetingNotificationResponse
                            {
                                RecipientsFailureInfo = new List<MeetingNotificationRecipientFailureInfo> { failureInfo }
                            };

                            response.Content = new StringContent(JsonSerializer.Serialize(infos));
#if !NETFRAMEWORK
                            response.StatusCode = HttpStatusCode.MultiStatus;
#else
                            response.StatusCode = (HttpStatusCode)207;
#endif
                            break;
                        case "403":
                            response.Content = new StringContent("{\"error\":{\"code\":\"BotNotInConversationRoster\"}}");
                            response.StatusCode = HttpStatusCode.Forbidden;
                            break;
                        case "400":
                            var error = new ErrorResponse(new Error("BadSyntax"));
                            response.Content = new StringContent(ProtocolJsonSerializer.ToJson(error));
                            response.StatusCode = HttpStatusCode.BadRequest;
                            break;
                        default:
                            response.StatusCode = HttpStatusCode.Accepted;
                            break;
                    }
                }

                // SendMessageToListOfUsers
                else if (request.RequestUri.PathAndQuery.EndsWith("v3/batch/conversation/users/"))
                {
                    var requestBody = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var payload = JsonDocument.Parse(requestBody);
                    JsonElement root = payload.RootElement;
                    var activity = ProtocolJsonSerializer.ToObject<Activity>(root.GetProperty("activity"));

                    // hack From.Name as expected status code, for testing
                    switch (activity.From.Name)
                    {
                        case "201":
                            response.Content = new StringContent("operation-1");
                            response.StatusCode = HttpStatusCode.Created;
                            break;
                        case "400":
                            response.Content = new StringContent("{\"error\":{\"code\":\"BadSyntax\"}}");
                            response.StatusCode = HttpStatusCode.BadRequest;
                            break;
                        case "403":
                            response.Content = new StringContent("{\"error\":{\"code\":\"Forbidden\"}}");
                            response.StatusCode = HttpStatusCode.Forbidden;
                            break;
                        case "429":
                            response.Content = new StringContent("{\"error\":{\"code\":\"TooManyRequests\"}}");
#if !NETFRAMEWORK
                            response.StatusCode = HttpStatusCode.TooManyRequests;
#else
                            response.StatusCode = (HttpStatusCode)429;
#endif
                            break;
                        default:
                            response.StatusCode = HttpStatusCode.Accepted;
                            break;
                    }
                }

                // SendMessageToAllUsersInTenant
                else if (request.RequestUri.PathAndQuery.EndsWith("v3/batch/conversation/tenant/"))
                {
                    var requestBody = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var payload = JsonDocument.Parse(requestBody);
                    JsonElement root = payload.RootElement;
                    var requestActivity = ProtocolJsonSerializer.ToObject<Activity>(root.GetProperty("activity"));

                    // hack From.Name as expected status code, for testing
                    switch (requestActivity.From.Name)
                    {
                        case "201":
                            response.Content = new StringContent("operation-1");
                            response.StatusCode = HttpStatusCode.Created;
                            break;
                        case "400":
                            response.Content = new StringContent("{\"error\":{\"code\":\"BadSyntax\"}}");
                            response.StatusCode = HttpStatusCode.BadRequest;
                            break;
                        case "403":
                            response.Content = new StringContent("{\"error\":{\"code\":\"Forbidden\"}}");
                            response.StatusCode = HttpStatusCode.Forbidden;
                            break;
                        case "429":
                            response.Content = new StringContent("{\"error\":{\"code\":\"TooManyRequests\"}}");
#if !NETFRAMEWORK
                            response.StatusCode = HttpStatusCode.TooManyRequests;
#else
                            response.StatusCode = (HttpStatusCode)429;
#endif
                            break;
                        default:
                            response.StatusCode = HttpStatusCode.Accepted;
                            break;
                    }
                }

                // SendMessageToAllUsersInTeam
                else if (request.RequestUri.PathAndQuery.EndsWith("v3/batch/conversation/team/"))
                {
                    var requestBody = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var payload = JsonDocument.Parse(requestBody);
                    JsonElement root = payload.RootElement;
                    var requestActivity = ProtocolJsonSerializer.ToObject<Activity>(root.GetProperty("activity"));

                    // hack From.Name as expected status code, for testing
                    switch (requestActivity.From.Name)
                    {
                        case "201":
                            response.Content = new StringContent("operation-1");
                            response.StatusCode = HttpStatusCode.Created;
                            break;
                        case "400":
                            response.Content = new StringContent("{\"error\":{\"code\":\"BadSyntax\"}}");
                            response.StatusCode = HttpStatusCode.BadRequest;
                            break;
                        case "403":
                            response.Content = new StringContent("{\"error\":{\"code\":\"Forbidden\"}}");
                            response.StatusCode = HttpStatusCode.Forbidden;
                            break;
                        case "404":
                            response.Content = new StringContent("{\"error\":{\"code\":\"NotFound\"}}");
                            response.StatusCode = HttpStatusCode.NotFound;
                            break;
                        case "429":
                            response.Content = new StringContent("{\"error\":{\"code\":\"TooManyRequests\"}}");
#if !NETFRAMEWORK
                            response.StatusCode = HttpStatusCode.TooManyRequests;
#else
                            response.StatusCode = (HttpStatusCode)429;
#endif
                            break;
                        default:
                            response.StatusCode = HttpStatusCode.Accepted;
                            break;
                    }
                }

                // SendMessageToListOfChannels
                else if (request.RequestUri.PathAndQuery.EndsWith("v3/batch/conversation/channels/"))
                {
                    var requestBody = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var payload = JsonDocument.Parse(requestBody);
                    JsonElement root = payload.RootElement;
                    var requestActivity = ProtocolJsonSerializer.ToObject<Activity>(root.GetProperty("activity"));

                    // hack From.Name as expected status code, for testing
                    switch (requestActivity.From.Name)
                    {
                        case "201":
                            response.Content = new StringContent("operation-1");
                            response.StatusCode = HttpStatusCode.Created;
                            break;
                        case "400":
                            response.Content = new StringContent("{\"error\":{\"code\":\"BadSyntax\"}}");
                            response.StatusCode = HttpStatusCode.BadRequest;
                            break;
                        case "403":
                            response.Content = new StringContent("{\"error\":{\"code\":\"Forbidden\"}}");
                            response.StatusCode = HttpStatusCode.Forbidden;
                            break;
                        case "429":
                            response.Content = new StringContent("{\"error\":{\"code\":\"TooManyRequests\"}}");
#if !NETFRAMEWORK
                            response.StatusCode = HttpStatusCode.TooManyRequests;
#else
                            response.StatusCode = (HttpStatusCode)429;
#endif
                            break;
                        default:
                            response.StatusCode = HttpStatusCode.Accepted;
                            break;
                    }
                }

                // GetOperationState
                else if (request.RequestUri.PathAndQuery.Contains("v3/batch/conversation/operation-id") && request.Method.ToString() == "GET")
                {
                    // get status from url for testing
                    var uri = request.RequestUri.PathAndQuery;
                    var status = uri[(uri.IndexOf("%2A") + 3)..];

                    switch (status)
                    {
                        case "200":
                            var content = new
                            {
                                State = "state-1",
                                Response = new { StatusMap = new { StatusMap = 1 } },
                                TotalEntriesCount = 1,
                            };
                            response.Content = new StringContent(ProtocolJsonSerializer.ToJson(content));
                            response.StatusCode = HttpStatusCode.OK;
                            break;
                        case "400":
                            response.Content = new StringContent("{\"error\":{\"code\":\"BadSyntax\"}}");
                            response.StatusCode = HttpStatusCode.BadRequest;
                            break;
                        case "429":
                            response.Content = new StringContent("{\"error\":{\"code\":\"TooManyRequests\"}}");
#if !NETFRAMEWORK
                            response.StatusCode = HttpStatusCode.TooManyRequests;
#else
                            response.StatusCode = (HttpStatusCode)429;
#endif
                            break;
                        default:
                            response.StatusCode = HttpStatusCode.Accepted;
                            break;
                    }
                }

                // GetPagedFailedEntries
                else if (request.RequestUri.PathAndQuery.Contains("v3/batch/conversation/failedentries/operation-id"))
                {
                    // Get status from url for testing
                    var uri = request.RequestUri.PathAndQuery;
                    var status = uri[(uri.IndexOf("%2A") + 3)..];

                    switch (status)
                    {
                        case "200":
                            var content = new BatchFailedEntriesResponse
                            {
                                ContinuationToken = "token-1",
                                FailedEntries = new List<BatchFailedEntry> { new BatchFailedEntry { EntryId = "entry-1", Error = "400 user not found" } },
                            };
                            response.Content = new StringContent(ProtocolJsonSerializer.ToJson(content));
                            response.StatusCode = HttpStatusCode.OK;
                            break;
                        case "400":
                            response.Content = new StringContent("{\"error\":{\"code\":\"BadSyntax\"}}");
                            response.StatusCode = HttpStatusCode.BadRequest;
                            break;
                        case "429":
                            response.Content = new StringContent("{\"error\":{\"code\":\"TooManyRequests\"}}");
#if !NETFRAMEWORK
                            response.StatusCode = HttpStatusCode.TooManyRequests;
#else
                            response.StatusCode = (HttpStatusCode)429;
#endif
                            break;
                        default:
                            response.StatusCode = HttpStatusCode.Accepted;
                            break;
                    }
                }

                // CancelOperation
                else if (request.RequestUri.PathAndQuery.Contains("v3/batch/conversation/operation-id") && request.Method.ToString() == "DELETE")
                {
                    // get status from url for testing
                    var uri = request.RequestUri.PathAndQuery;
                    var status = uri[(uri.IndexOf("%2A") + 3)..];

                    switch (status)
                    {
                        case "200":
                            response.StatusCode = HttpStatusCode.OK;
                            break;
                        case "400":
                            response.Content = new StringContent("{\"error\":{\"code\":\"BadSyntax\"}}");
                            response.StatusCode = HttpStatusCode.BadRequest;
                            break;
                        case "429":
                            response.Content = new StringContent("{\"error\":{\"code\":\"TooManyRequests\"}}");
#if !NETFRAMEWORK
                            response.StatusCode = HttpStatusCode.TooManyRequests;
#else
                            response.StatusCode = (HttpStatusCode)429;
#endif
                            break;
                        default:
                            response.StatusCode = HttpStatusCode.Accepted;
                            break;
                    }
                }

                return response;
            }
        }

        private class TestConnector(Uri endpoint) : IConnectorClient, IRestTransport
        {
            private readonly Uri _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            
            public Uri BaseUri => throw new NotImplementedException();

            public IAttachments Attachments => throw new NotImplementedException();

            public IConversations Conversations => getMockedConversations();

            public Uri Endpoint => _endpoint;

            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public Task<HttpClient> GetHttpClientAsync()
            {
                return Task.FromResult(new HttpClient(new RosterHttpMessageHandler()));
            }

            private IConversations getMockedConversations()
            {
                var result = new PagedMembersResult
                {
                    ContinuationToken = "",
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
                var member = new TeamsChannelAccount()

                {
                    Id = "id-1",
                    Name = "name-1",
                    GivenName = "givenName-1",
                    Surname = "surname-1",
                    Email = "email-1",
                    UserPrincipalName = "userPrincipalName-1",
                    UserRole = "userRole-1",
                    TenantId = "tenantId-1",
                };
                var conversations = new Mock<IConversations>();
                conversations.Setup(x => x.GetConversationMemberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChannelAccount { Id = "id-1" });
                conversations.Setup(x => x.GetConversationPagedMembersAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);
                conversations.Setup(x => x.GetConversationMemberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(member);

                return conversations.Object;
            }
        }

        private class TestCreateConversationAdapter(string activityId, string conversationId) : IChannelAdapter
        {
            private readonly string _activityId = activityId;

            private readonly string _conversationId = conversationId;

            public string AppId { get; set; }

            public string ChannelId { get; set; }

            public string ServiceUrl { get; set; }

            public string Audience { get; set; }

            public ConversationParameters ConversationParameters { get; set; }
            public Func<ITurnContext, Exception, Task> OnTurnError { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public IMiddlewareSet MiddlewareSet => throw new NotImplementedException();

            public ILogger Logger { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, AgentCallbackHandler callback, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, string audience, AgentCallbackHandler callback, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, ConversationReference reference, AgentCallbackHandler callback, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task ContinueConversationAsync(ClaimsIdentity claimsIdentity, ConversationReference reference, string audience, AgentCallbackHandler callback, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task ContinueConversationAsync(string botId, IActivity continuationActivity, AgentCallbackHandler callback, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task ContinueConversationAsync(string botId, ConversationReference reference, AgentCallbackHandler callback, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task CreateConversationAsync(string botAppId, string channelId, string serviceUrl, string scope, ConversationParameters conversationParameters, AgentCallbackHandler callback, CancellationToken cancellationToken)
            {
                AppId = botAppId;
                ChannelId = channelId;
                ServiceUrl = serviceUrl;
                Audience = scope;
                ConversationParameters = conversationParameters;

                var activity = new Activity { Id = _activityId, ChannelId = channelId, ServiceUrl = serviceUrl, Conversation = new ConversationAccount { Id = _conversationId } };

                var mockTurnContext = new Mock<ITurnContext>();
                mockTurnContext.Setup(tc => tc.Activity).Returns(activity);

                callback(mockTurnContext.Object, cancellationToken);
                return Task.CompletedTask;
            }

            public Task<ConversationReference> CreateConversationAsync(ClaimsIdentity identity, string channelId, string serviceUrl, string scope, ConversationParameters parameters, AgentCallbackHandler callback, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task DeleteActivityAsync(ITurnContext turnContext, ConversationReference reference, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<InvokeResponse> ProcessActivityAsync(ClaimsIdentity claimsIdentity, IActivity activity, AgentCallbackHandler callback, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task ProcessProactiveAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, string audience, AgentCallbackHandler callback, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task ProcessProactiveAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, IAgent bot, CancellationToken cancellationToken, string audience = null)
            {
                throw new NotImplementedException();
            }

            public Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<ResourceResponse> UpdateActivityAsync(ITurnContext turnContext, IActivity activity, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public IChannelAdapter Use(IMiddleware middleware)
            {
                throw new NotImplementedException();
            }
        }
    }
}
