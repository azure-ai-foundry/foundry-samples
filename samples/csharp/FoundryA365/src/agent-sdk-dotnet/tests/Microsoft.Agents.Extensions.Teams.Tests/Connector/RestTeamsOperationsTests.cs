// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Connector;
using Microsoft.Agents.Connector.Types;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Errors;

namespace Microsoft.Agents.Extensions.Teams.Tests
{
    public class RestTeamsOperationsTests
    {
        private const string ServiceUrl = "https://test.botframework.com";
        private const string Audience = "test-audience";
        private const string TeamId = "test-team";
        private const string MeetingId = "test-meeting";
        private const string ParticipantId = "test-participant";
        private const string TenantId = "test-tenant";
        private readonly Mock<HttpClient> MockHttpClient;
        private readonly Activity TestActivity = new Activity();

        private readonly List<TeamMember> Members =
        [
            new TeamMember(TeamId)
        ];

        public RestTeamsOperationsTests()
        {
            MockHttpClient = new Mock<HttpClient>();
        }

        [Fact]
        public void Constructor_ShouldInstantiateCorrectly()
        {
            var teamsOperations = UseTeamsOperations();
            Assert.NotNull(teamsOperations);
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullConnector()
        {
            Assert.Throws<ArgumentNullException>(() => new RestTeamsOperations(null));
        }

        [Fact]
        public async Task FetchChannelListAsync_ShouldThrowOnNullTeamId()
        {
            var teamsOperations = UseTeamsOperations();
            await Assert.ThrowsAsync<ArgumentNullException>(() => teamsOperations.FetchChannelListAsync(null, []));
        }

        [Fact]
        public async Task FetchChannelListAsync_ShouldReturnConversationList()
        {
            var conversations = new ConversationList()
            {
                Conversations = new List<ChannelInfo>
                {
                    new ChannelInfo("channel-1"),
                    new ChannelInfo("channel-2"),
                }
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(conversations))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);
            var teamsOperations = UseTeamsOperations();
            var result = await teamsOperations.FetchChannelListAsync(TeamId, []);

            Assert.Equal(conversations.Conversations.First().Id, result.Conversations.First().Id);
            Assert.Equal(conversations.Conversations[1].Id, result.Conversations[1].Id);
        }

        [Fact]
        public async Task FetchTeamDetailsAsync_ShouldThrowOnNullTeamId()
        {
            var teamsOperations = UseTeamsOperations();
            await Assert.ThrowsAsync<ArgumentNullException>(() => teamsOperations.FetchTeamDetailsAsync(null, []));
        }

        [Fact]
        public async Task FetchTeamDetailsAsync_ShouldReturnTeamDetails()
        {
            var details = new TeamDetails
            {
                Id = TeamId,
                Name = "test-name",
                AadGroupId = "test-aad"
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(details))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);
            var teamsOperations = UseTeamsOperations();
            var result = await teamsOperations.FetchTeamDetailsAsync(TeamId, []);

            Assert.Equal(details.Id, result.Id);
            Assert.Equal(details.Name, result.Name);
            Assert.Equal(details.AadGroupId, result.AadGroupId);
        }

        [Fact]
        public async Task FetchMeetingInfoAsync_ShouldThrowOnNullMeetingId()
        {
            var teamsOperations = UseTeamsOperations();
            await Assert.ThrowsAsync<ArgumentNullException>(() => teamsOperations.FetchMeetingInfoAsync(null, []));
        }

        [Fact]
        public async Task FetchMeetingInfoAsync_ShouldReturnMeetingInfo()
        {
            var info = new MeetingInfo
            {
                Details = new MeetingDetails(TeamId),
                Conversation = new ConversationAccount(id: "test-conversation"),
                Organizer = new TeamsChannelAccount(id: TeamId, userRole: "admin")
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(info))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);
            var teamsOperations = UseTeamsOperations();
            var result = await teamsOperations.FetchMeetingInfoAsync(MeetingId, []);

            Assert.Equal(info.Details.Id, result.Details.Id);
            Assert.Equal(info.Conversation.Id, result.Conversation.Id);
            Assert.Equal(info.Organizer.Id, result.Organizer.Id);
            Assert.Equal(info.Organizer.UserRole, result.Organizer.UserRole);
        }

        [Fact]
        public async Task FetchParticipantAsync_ShouldThrowOnNullMeetingId()
        {
            var teamsOperations = UseTeamsOperations();
            await Assert.ThrowsAsync<ArgumentNullException>(() => teamsOperations.FetchParticipantAsync(null, ParticipantId, TenantId, []));
        }

        [Fact]
        public async Task FetchParticipantAsync_ShouldThrowOnNullParticipantId()
        {
            var teamsOperations = UseTeamsOperations();
            await Assert.ThrowsAsync<ArgumentNullException>(() => teamsOperations.FetchParticipantAsync(MeetingId, null, TenantId, []));
        }

        [Fact]
        public async Task FetchParticipantAsync_ShouldThrowOnNullTenantId()
        {
            var teamsOperations = UseTeamsOperations();
            await Assert.ThrowsAsync<ArgumentNullException>(() => teamsOperations.FetchParticipantAsync(MeetingId, ParticipantId, null, []));
        }

        [Fact]
        public async Task FetchParticipantAsync_ShouldReturnParticipantDetails()
        {
            var meetingParticipant = new TeamsMeetingParticipant
            {
                User = new TeamsChannelAccount(id: TeamId, userRole: "admin"),
                Meeting = new MeetingParticipantInfo("admin"),
                Conversation = new ConversationAccount(id: "test-conversation"),
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(meetingParticipant))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);
            var teamsOperations = UseTeamsOperations();
            var result = await teamsOperations.FetchParticipantAsync(TeamId, ParticipantId, TenantId, []);

            Assert.Equal(meetingParticipant.User.Id, result.User.Id);
            Assert.Equal(meetingParticipant.Meeting.Role, result.Meeting.Role);
            Assert.Equal(meetingParticipant.Conversation.Id, result.Conversation.Id);
        }

        [Fact]
        public async Task SendMeetingNotificationAsync_ShouldThrowOnNullMeetingId()
        {
            var teamsOperations = UseTeamsOperations();
            await Assert.ThrowsAsync<ArgumentNullException>(() => teamsOperations.SendMeetingNotificationAsync(null, null));
        }

        [Fact]
        public async Task SendMeetingNotificationAsync_ShouldReturnNotificationResponse()
        {
            var info = new MeetingNotificationRecipientFailureInfo
            {
                ErrorCode = "404",
                FailureReason = "Not Found"
            };
            
            var notification = new MeetingNotificationResponse
            {
                RecipientsFailureInfo = [info]
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(notification))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);
            var teamsOperations = UseTeamsOperations();
            var result = await teamsOperations.SendMeetingNotificationAsync(MeetingId, null);

            Assert.Single(result.RecipientsFailureInfo);
            Assert.Equal(notification.RecipientsFailureInfo[0].ErrorCode, result.RecipientsFailureInfo[0].ErrorCode);
            Assert.Equal(notification.RecipientsFailureInfo[0].FailureReason, result.RecipientsFailureInfo[0].FailureReason);
        }

        [Fact]
        public async Task SendMessageToListOfUsersAsync_ShouldThrowOnNullActivity()
        {
            var teamsOperations = UseTeamsOperations();
            await Assert.ThrowsAsync<ArgumentNullException>(() => teamsOperations.SendMessageToListOfUsersAsync(null, Members, TenantId));
        }

        [Fact]
        public async Task SendMessageToListOfUsersAsync_ShouldThrowOnNullTenantId()
        {
            var teamsOperations = UseTeamsOperations();
            await Assert.ThrowsAsync<ArgumentNullException>(() => teamsOperations.SendMessageToListOfUsersAsync(TestActivity, Members, null));
        }

        [Fact]
        public async Task SendMessageToListOfUsersAsync_ShouldThrowOnEmptyMemberList()
        {
            var teamsOperations = UseTeamsOperations();
            await Assert.ThrowsAsync<ArgumentNullException>(() => teamsOperations.SendMessageToListOfUsersAsync(TestActivity, [], TenantId));
        }

        [Fact]
        public async Task SendMessageToListOfUsersAsync_ShouldReturnOperationId()
        {
            var operationId = "test-operation";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(operationId)
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);
            var teamsOperations = UseTeamsOperations();
            var result = await teamsOperations.SendMessageToListOfUsersAsync(TestActivity, Members, TenantId);

            Assert.Equal(operationId, result);
        }

        [Fact]
        public async Task SendMessageToAllUsersInTenantAsync_ShouldThrowOnNullActivity()
        {
            var teamsOperations = UseTeamsOperations();
            await Assert.ThrowsAsync<ArgumentNullException>(() => teamsOperations.SendMessageToAllUsersInTenantAsync(null, TenantId));
        }

        [Fact]
        public async Task SendMessageToAllUsersInTenantAsync_ShouldThrowOnNullTenantId()
        {
            var teamsOperations = UseTeamsOperations();
            await Assert.ThrowsAsync<ArgumentNullException>(() => teamsOperations.SendMessageToAllUsersInTenantAsync(TestActivity, null));
        }

        [Fact]
        public async Task SendMessageToAllUsersInTenantAsync_ShouldReturnOperationId()
        {
            var operationId = "operationId";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(operationId)
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);
            var teamsOperations = UseTeamsOperations();
            var result = await teamsOperations.SendMessageToAllUsersInTenantAsync(TestActivity, TenantId);

            Assert.Equal(operationId, result);
        }

        [Fact]
        public async Task SendMessageToAllUsersInTeamAsync_ShouldThrowOnNullActivity()
        {
            var teamsOperations = UseTeamsOperations();
            await Assert.ThrowsAsync<ArgumentNullException>(() => teamsOperations.SendMessageToAllUsersInTeamAsync(null, TeamId, TenantId));
        }

        [Fact]
        public async Task SendMessageToAllUsersInTeamAsync_ShouldThrowOnNullTeamId()
        {
            var teamsOperations = UseTeamsOperations();
            await Assert.ThrowsAsync<ArgumentNullException>(() => teamsOperations.SendMessageToAllUsersInTeamAsync(TestActivity, null, TenantId));
        }

        [Fact]
        public async Task SendMessageToAllUsersInTeamAsync_ShouldThrowOnNullTenantId()
        {
            var teamsOperations = UseTeamsOperations();
            await Assert.ThrowsAsync<ArgumentNullException>(() => teamsOperations.SendMessageToAllUsersInTeamAsync(TestActivity, TeamId, null));
        }

        [Fact]
        public async Task SendMessageToAllUsersInTeamAsync_ShouldReturnOperationId()
        {
            var operationId = "operationId";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(operationId)
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);
            var teamsOperations = UseTeamsOperations();
            var result = await teamsOperations.SendMessageToAllUsersInTeamAsync(TestActivity, TeamId, TenantId);

            Assert.Equal(operationId, result);
        }

        [Fact]
        public async Task SendMessageToListOfChannelsAsync_ShouldThrowOnNullActivity()
        {
            var teamsOperations = UseTeamsOperations();
            await Assert.ThrowsAsync<ArgumentNullException>(() => teamsOperations.SendMessageToListOfChannelsAsync(null, Members, TenantId));
        }

        [Fact]
        public async Task SendMessageToListOfChannelsAsync_ShouldThrowOnEmptyChannelsList()
        {
            var teamsOperations = UseTeamsOperations();
            await Assert.ThrowsAsync<ArgumentNullException>(() => teamsOperations.SendMessageToListOfChannelsAsync(TestActivity, [], TenantId));
        }

        [Fact]
        public async Task SendMessageToListOfChannelsAsync_ShouldThrowOnNullTenantId()
        {
            var teamsOperations = UseTeamsOperations();
            await Assert.ThrowsAsync<ArgumentNullException>(() => teamsOperations.SendMessageToListOfChannelsAsync(TestActivity, Members, null));
        }

        [Fact]
        public async Task SendMessageToListOfChannelsAsync_ShouldReturnOperationId()
        {
            var operationId = "operationId";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(operationId)
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);
            var teamsOperations = UseTeamsOperations();
            var result = await teamsOperations.SendMessageToListOfChannelsAsync(TestActivity, Members, TenantId);

            Assert.Equal(operationId, result);
        }

        [Fact]
        public async Task GetOperationStateAsync_ShouldThrowOnNullOperationId()
        {
            var teamsOperations = UseTeamsOperations();
            await Assert.ThrowsAsync<ArgumentNullException>(() => teamsOperations.GetOperationStateAsync(null));
        }

        [Fact]
        public async Task GetOperationStateAsync_ShouldReturnOperationState()
        {
            var state = new BatchOperationState { State = "Completed" };
            
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(state))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);
            var teamsOperations = UseTeamsOperations();
            var result = await teamsOperations.GetOperationStateAsync("operationId");

            Assert.Equal(state.State, result.State);
        }

        [Fact]
        public async Task GetPagedFailedEntriesAsync_ShouldThrowOnNullOperationId()
        {
            var teamsOperations = UseTeamsOperations();
            await Assert.ThrowsAsync<ArgumentNullException>(() => teamsOperations.GetPagedFailedEntriesAsync(null));
        }

        [Fact]
        public async Task GetPagedFailedEntriesAsync_ShouldReturnFailedEntries()
        {
            var batchResponse = new BatchFailedEntriesResponse { FailedEntries = [new BatchFailedEntry { EntryId = "1", Error = "Id Not Found" }]};

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(batchResponse))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);
            var teamsOperations = UseTeamsOperations();
            var result = await teamsOperations.GetPagedFailedEntriesAsync("operationId");

            Assert.Single(result.FailedEntries);
            Assert.Equal(batchResponse.FailedEntries.First().EntryId, result.FailedEntries.First().EntryId);
            Assert.Equal(batchResponse.FailedEntries.First().Error, result.FailedEntries.First().Error);
        }

        [Fact]
        public async Task CancelOperationAsync_ShouldThrowOnNullOperationId()
        {
            var teamsOperations = UseTeamsOperations();
            await Assert.ThrowsAsync<ArgumentNullException>(() => teamsOperations.CancelOperationAsync(null));
        }

        [Fact]
        public async Task CancelOperationAsync_ShouldCallCancelOperation()
        {
            var state = new BatchOperationState { State = "Cancelled" };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(state))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);
            var teamsOperations = UseTeamsOperations();
            await teamsOperations.CancelOperationAsync("operationId");

            MockHttpClient.Verify(service => service.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetResponseAsync_ShouldThrowWithErrorBody()
        {
            var NotFoundError = new Error
            {
                Code = "404",
                Message = "Not Found",
                InnerHttpError = new InnerHttpError { StatusCode = 404 }
            };

            var NotFoundResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound,
                Content = new StringContent(JsonSerializer.Serialize(new ErrorResponse(NotFoundError)))
            };

            var teamsOperations = UseTeamsOperations();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(NotFoundResponse);

            try
            {
                await teamsOperations.CancelOperationAsync("operationId");
            }
            catch (AggregateException ex)
            {
                Assert.Single(ex.InnerExceptions);
                Assert.IsType<ErrorResponseException>(ex.InnerExceptions.First());

                var returnedEx = (ErrorResponseException)ex.InnerExceptions.First();

                Assert.Equal(NotFoundError.Code, returnedEx.Body.Error.Code);
                Assert.Equal(NotFoundError.Message, returnedEx.Body.Error.Message);
            }
        }

        [Fact]
        public async Task GetResponseAsync_ShouldThrowWithoutErrorBody()
        {
            var InternalErrorResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent(JsonSerializer.Serialize("Internal Error"))
            };
            
            var teamsOperations = UseTeamsOperations();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(InternalErrorResponse);

            var exMessage = $"CancelOperation operation returned an invalid status code '{InternalErrorResponse.StatusCode}'";

            try
            {
                await teamsOperations.CancelOperationAsync("operationId");
            }
            catch (AggregateException ex)
            {
                Assert.Single(ex.InnerExceptions);
                Assert.IsType<ErrorResponseException>(ex.InnerExceptions.First());

                var returnedEx = (ErrorResponseException)ex.InnerExceptions.First();

                Assert.Null(returnedEx.Body);
                Assert.Equal(exMessage, returnedEx.Message);
            }
        }

        [Fact]
        public async Task GetResponseAsync_ShouldThrowThrottleExceptionsAfterRetry()
        {
            var ThrottleErrorResponse = new HttpResponseMessage
            {
#if !NETFRAMEWORK
                StatusCode = HttpStatusCode.TooManyRequests
#else
                StatusCode = (HttpStatusCode)429
#endif
            };

            var teamsOperations = UseTeamsOperations();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(ThrottleErrorResponse);

            try
            {
                await teamsOperations.CancelOperationAsync("operationId");
            }
            catch (AggregateException ex)
            {
                Assert.Equal(11, ex.InnerExceptions.Count);
                Assert.IsType<ThrottleException>(ex.InnerExceptions.First());
            }
        }

        private RestTeamsOperations UseTeamsOperations()
        {
            var transport = new Mock<IRestTransport>();
            transport.Setup(x => x.Endpoint)
                .Returns(new Uri(ServiceUrl));
            transport.Setup(a => a.GetHttpClientAsync())
                .Returns(Task.FromResult(MockHttpClient.Object));

            return new RestTeamsOperations(transport.Object);
        }
    }
}
