// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Connector.RestClients;
using Microsoft.Agents.Connector.Types;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Connector.Tests
{
    public class ConversationsRestClientTests
    {
        private const string AttachmentId = "test-attachment";
        private const string ConversationId = "test-conversation";
        private const string UserId = "test-user";
        private const string MemberId = "member-id";
        private const int PageSize = 3;
        private readonly Uri UriEndpoint = new Uri("http://localhost");
        private readonly Mock<HttpClient> MockHttpClient;

        private readonly Activity TestActivity = new Activity
        {
            Id = "test-id",
            Conversation = new ConversationAccount { Id = "conversation-id" },
            ReplyToId = "reply-id"
        };

        private static readonly Error NotFoundError = new Error
        {
            Code = "404",
            Message = "Not Found",
            InnerHttpError = new InnerHttpError { StatusCode = 404 }
        };

        private readonly HttpResponseMessage NotFoundResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.NotFound,
            Content = new StringContent(JsonSerializer.Serialize(new ErrorResponse(NotFoundError)))
        };

        private readonly HttpResponseMessage InternalErrorResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.InternalServerError,
            Content = new StringContent(JsonSerializer.Serialize("Internal Error"))
        };

        public ConversationsRestClientTests()
        {
            MockHttpClient = new Mock<HttpClient>();
        }

        [Fact]
        public void Constructor_ShouldInstantiateCorrectly()
        {
            Assert.NotNull(UseConversation());
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullTransport()
        {
            Assert.Throws<ArgumentNullException>(() => new ConversationsRestClient(null));
        }

        [Fact]
        public async Task GetConversationsAsync_ShouldReturnConversations()
        {
            var conversationsClient = UseConversation();

            var conversations = new List<ConversationMembers>
            {
                new ConversationMembers() { Id = "test-member" }
            };

            var conversationsResult = new ConversationsResult
            {
                Conversations = conversations
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(conversationsResult))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

            var result = await conversationsClient.GetConversationsAsync();

            Assert.Single(result.Conversations);
            Assert.Equal(conversations[0].Id, result.Conversations[0].Id);
        }

        [Fact]
        public async Task GetConversationsAsync_ShouldThrowWithErrorBody()
        {
            var conversationsClient = UseConversation();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(NotFoundResponse);

            try
            {
                _ = await conversationsClient.GetConversationsAsync();
            }
            catch (ErrorResponseException ex)
            {
                Assert.Equal(NotFoundError.Code, ex.Body.Error.Code);
                Assert.Equal(NotFoundError.Message, ex.Body.Error.Message);
                Assert.Equal(NotFoundError.InnerHttpError.StatusCode, ex.Body.Error.InnerHttpError.StatusCode);
            }
        }

        [Fact]
        public async Task GetConversationsAsync_ShouldThrowWithoutErrorBody()
        {
            var conversationsClient = UseConversation();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(InternalErrorResponse);

            var exMessage = $"GetConversations operation returned an invalid status code '(500) {InternalErrorResponse.StatusCode}'";

            try
            {
                _ = await conversationsClient.GetConversationsAsync(AttachmentId, CancellationToken.None);
            }
            catch (ErrorResponseException ex)
            {
                Assert.Null(ex.Body);
                Assert.Equal(exMessage, ex.Message);
            }
        }

        [Fact]
        public async Task CreateConversationAsync_ShouldReturnConversationResource()
        {
            var conversationsClient = UseConversation();

            var resourceResponse = new ConversationResourceResponse { 
                ActivityId = "test-activity",
                ServiceUrl = "http://localHost",
                Id = "test-id"
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(resourceResponse))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

            var result = await conversationsClient.CreateConversationAsync(new ConversationParameters { IsGroup = true});

            Assert.Equal(resourceResponse.ActivityId, result.ActivityId);
            Assert.Equal(resourceResponse.ServiceUrl, result.ServiceUrl);
            Assert.Equal(resourceResponse.Id, result.Id);
        }

        [Fact]
        public async Task CreateConversationAsync_ShouldThrowWithErrorBody()
        {
            var conversationsClient = UseConversation();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(NotFoundResponse);

            try
            {
                _ = await conversationsClient.CreateConversationAsync();
            }
            catch (ErrorResponseException ex)
            {
                Assert.Equal(NotFoundError.Code, ex.Body.Error.Code);
                Assert.Equal(NotFoundError.Message, ex.Body.Error.Message);
                Assert.Equal(NotFoundError.InnerHttpError.StatusCode, ex.Body.Error.InnerHttpError.StatusCode);
            }
        }

        [Fact]
        public async Task CreateConversationAsync_ShouldThrowWithoutErrorBody()
        {
            var conversationsClient = UseConversation();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(InternalErrorResponse);

            var exMessage = $"CreateConversation operation returned an invalid status code '(500) {InternalErrorResponse.StatusCode}'";

            try
            {
                _ = await conversationsClient.CreateConversationAsync();
            }
            catch (ErrorResponseException ex)
            {
                Assert.Null(ex.Body);
                Assert.Equal(exMessage, ex.Message);
            }
        }

        [Fact]
        public async Task SendToConversationAsync_ShouldThrowOnNullConversationId()
        {
            var conversationsClient = UseConversation();
            var activity = new Activity
            {
                Conversation = new ConversationAccount { Id = null }
            };

            await Assert.ThrowsAsync<ArgumentNullException>(() => conversationsClient.SendToConversationAsync(activity));
        }

        [Fact]
        public async Task SendToConversationAsync_ShouldReturnResourceResponse()
        {
            var conversationsClient = UseConversation();

            var resourceResponse = new ResourceResponse { Id = "resource-id" };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(resourceResponse))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);


            var result = await conversationsClient.SendToConversationAsync(TestActivity);

            Assert.Equal(resourceResponse.Id, result.Id);
        }

        [Fact]
        public async Task SendToConversationAsync_ShouldThrowWithErrorBody()
        {
            var conversationsClient = UseConversation();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(NotFoundResponse);

            try
            {
                _ = await conversationsClient.SendToConversationAsync(TestActivity);
            }
            catch (ErrorResponseException ex)
            {
                Assert.Equal(NotFoundError.Code, ex.Body.Error.Code);
                Assert.Equal(NotFoundError.Message, ex.Body.Error.Message);
                Assert.Equal(NotFoundError.InnerHttpError.StatusCode, ex.Body.Error.InnerHttpError.StatusCode);
            }
        }

        [Fact]
        public async Task SendToConversationAsync_ShouldThrowWithoutErrorBody()
        {
            var conversationsClient = UseConversation();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(InternalErrorResponse);

            var exMessage = $"SendToConversation operation returned an invalid status code '(500) {InternalErrorResponse.StatusCode}'";

            try
            {
                _ = await conversationsClient.SendToConversationAsync(TestActivity);
            }
            catch (ErrorResponseException ex)
            {
                Assert.Null(ex.Body);
                Assert.Equal(exMessage, ex.Message);
            }
        }

        [Fact]
        public async Task SendConversationHistoryAsync_ShouldThrowOnNullConversationId()
        {
            var conversationsClient = UseConversation();

            await Assert.ThrowsAsync<ArgumentNullException>(() => conversationsClient.SendConversationHistoryAsync(null));
        }

        [Fact]
        public async Task SendConversationHistoryAsync_ShouldReturnResourceResponse()
        {
            var conversationsClient = UseConversation();

            var transcript = new Transcript(new List<Activity> { TestActivity });

            var resourceResponse = new ResourceResponse { Id = "resource-id" };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(resourceResponse))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);


            var result = await conversationsClient.SendConversationHistoryAsync(ConversationId, transcript);

            Assert.Equal(resourceResponse.Id, result.Id);
        }

        [Fact]
        public async Task SendConversationHistoryAsync_ShouldThrowWithErrorBody()
        {
            var conversationsClient = UseConversation();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(NotFoundResponse);

            try
            {
                _ = await conversationsClient.SendConversationHistoryAsync(ConversationId);
            }
            catch (ErrorResponseException ex)
            {
                Assert.Equal(NotFoundError.Code, ex.Body.Error.Code);
                Assert.Equal(NotFoundError.Message, ex.Body.Error.Message);
                Assert.Equal(NotFoundError.InnerHttpError.StatusCode, ex.Body.Error.InnerHttpError.StatusCode);
            }
        }

        [Fact]
        public async Task SendConversationHistoryAsync_ShouldThrowWithoutErrorBody()
        {
            var conversationsClient = UseConversation();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(InternalErrorResponse);

            var exMessage = $"SendConversationHistory operation returned an invalid status code '(500) {InternalErrorResponse.StatusCode}'";

            try
            {
                _ = await conversationsClient.SendConversationHistoryAsync(ConversationId);
            }
            catch (ErrorResponseException ex)
            {
                Assert.Null(ex.Body);
                Assert.Equal(exMessage, ex.Message);
            }
        }

        [Fact]
        public async Task UpdateActivityAsync_ShouldThrowOnNullConversationId()
        {
            var conversationsClient = UseConversation();
            var activity = new Activity
            {
                Id = "test-id",
                Conversation = new ConversationAccount { Id = null }
            };

            await Assert.ThrowsAsync<ArgumentNullException>(() => conversationsClient.UpdateActivityAsync(activity));
        }

        [Fact]
        public async Task UpdateActivityAsync_ShouldThrowOnNullActivityId()
        {
            var conversationsClient = UseConversation();
            var activity = new Activity
            {
                Id = null,
                Conversation = new ConversationAccount { Id = "conversation-id" }
            };

            await Assert.ThrowsAsync<ArgumentNullException>(() => conversationsClient.UpdateActivityAsync(activity));
        }

        [Fact]
        public async Task UpdateActivityAsync_ShouldReturnResourceResponse()
        {
            var conversationsClient = UseConversation();

            var resourceResponse = new ResourceResponse { Id = "resource-id" };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(resourceResponse))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);


            var result = await conversationsClient.UpdateActivityAsync(TestActivity);

            Assert.Equal(resourceResponse.Id, result.Id);
        }

        [Fact]
        public async Task UpdateActivityAsync_ShouldThrowWithErrorBody()
        {
            var conversationsClient = UseConversation();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(NotFoundResponse);

            try
            {
                _ = await conversationsClient.UpdateActivityAsync(TestActivity);
            }
            catch (ErrorResponseException ex)
            {
                Assert.Equal(NotFoundError.Code, ex.Body.Error.Code);
                Assert.Equal(NotFoundError.Message, ex.Body.Error.Message);
                Assert.Equal(NotFoundError.InnerHttpError.StatusCode, ex.Body.Error.InnerHttpError.StatusCode);
            }
        }

        [Fact]
        public async Task UpdateActivityAsync_ShouldThrowWithoutErrorBody()
        {
            var conversationsClient = UseConversation();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(InternalErrorResponse);

            var exMessage = $"UpdateActivity operation returned an invalid status code '(500) {InternalErrorResponse.StatusCode}'";

            try
            {
                _ = await conversationsClient.UpdateActivityAsync(TestActivity);
            }
            catch (ErrorResponseException ex)
            {
                Assert.Null(ex.Body);
                Assert.Equal(exMessage, ex.Message);
            }
        }

        [Fact]
        public async Task ReplyTooActivityAsync_ShouldThrowOnNullConversationId()
        {
            var conversationsClient = UseConversation();
            var activity = new Activity
            {
                Id = "test-id",
                Conversation = new ConversationAccount { Id = null }
            };

            await Assert.ThrowsAsync<ArgumentNullException>(() => conversationsClient.ReplyToActivityAsync(activity));
        }

        [Fact]
        public async Task ReplyToActivityAsync_ShouldThrowOnNullActivity()
        {
            var conversationsClient = UseConversation();

            await Assert.ThrowsAsync<ArgumentNullException>(() => conversationsClient.ReplyToActivityAsync(null));
        }

        [Fact]
        public async Task ReplyToActivityAsync_ShouldThrowOnNullReplyId()
        {
            var conversationsClient = UseConversation();
            var activity = new Activity
            {
                Id = "test-id",
                Conversation = new ConversationAccount { Id = "conversation-id" },
                ReplyToId = null
            };

            await Assert.ThrowsAsync<ArgumentNullException>(() => conversationsClient.ReplyToActivityAsync(activity));
        }

        [Fact]
        public async Task ReplyToActivityAsync_ShouldReturnResourceResponse()
        {
            var conversationsClient = UseConversation();

            var resourceResponse = new ResourceResponse { Id = "resource-id" };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(resourceResponse))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);


            var result = await conversationsClient.ReplyToActivityAsync(TestActivity);

            Assert.Equal(resourceResponse.Id, result.Id);
        }

        [Fact]
        public async Task ReplyToActivityAsync_ShouldReturnEmptyResponse()
        {
            var conversationsClient = UseConversation();

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty)
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);


            var result = await conversationsClient.ReplyToActivityAsync(TestActivity);

            Assert.Empty(result.Id);
        }

        [Fact]
        public async Task ReplyToActivityAsync_ShouldThrowWithErrorBody()
        {
            var conversationsClient = UseConversation();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(NotFoundResponse);

            try
            {
                _ = await conversationsClient.ReplyToActivityAsync(TestActivity);
            }
            catch (ErrorResponseException ex)
            {
                Assert.Equal(NotFoundError.Code, ex.Body.Error.Code);
                Assert.Equal(NotFoundError.Message, ex.Body.Error.Message);
                Assert.Equal(NotFoundError.InnerHttpError.StatusCode, ex.Body.Error.InnerHttpError.StatusCode);
            }
        }

        [Fact]
        public async Task ReplyToActivityAsync_ShouldThrowWithoutErrorBody()
        {
            var conversationsClient = UseConversation();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(InternalErrorResponse);

            var exMessage = $"ReplyToActivity operation returned an invalid status code '(500) {InternalErrorResponse.StatusCode}'";

            try
            {
                _ = await conversationsClient.ReplyToActivityAsync(TestActivity);
            }
            catch (ErrorResponseException ex)
            {
                Assert.Null(ex.Body);
                Assert.Equal(exMessage, ex.Message);
            }
        }

        [Fact]
        public async Task DeleteActivityAsync_ShouldThrowOnNullConversationId()
        {
            var conversationsClient = UseConversation();
            var activity = new Activity
            {
                Id = "test-id",
                Conversation = new ConversationAccount { Id = null }
            };

            await Assert.ThrowsAsync<ArgumentNullException>(() => conversationsClient.DeleteActivityAsync(null, activity.Id));
        }

        [Fact]
        public async Task DeleteActivityAsync_ShouldThrowOnNullActivityId()
        {
            var conversationsClient = UseConversation();
            var activity = new Activity
            {
                Id = null,
                Conversation = new ConversationAccount { Id = "conversation-id" }
            };

            await Assert.ThrowsAsync<ArgumentNullException>(() => conversationsClient.DeleteActivityAsync(ConversationId, null));
        }

        [Fact]
        public async Task DeleteActivityAsync_ShouldCallSendRequest()
        {
            var conversationsClient = UseConversation();

            var resourceResponse = new ResourceResponse { Id = "resource-id" };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(resourceResponse))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);


            await conversationsClient.DeleteActivityAsync(TestActivity.Conversation.Id, TestActivity.Id);

            MockHttpClient.Verify(service => service.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteActivityAsync_ShouldThrowWithErrorBody()
        {
            var conversationsClient = UseConversation();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(NotFoundResponse);

            try
            {
                await conversationsClient.DeleteActivityAsync(TestActivity.Conversation.Id, TestActivity.Id);
            }
            catch (ErrorResponseException ex)
            {
                Assert.Equal(NotFoundError.Code, ex.Body.Error.Code);
                Assert.Equal(NotFoundError.Message, ex.Body.Error.Message);
                Assert.Equal(NotFoundError.InnerHttpError.StatusCode, ex.Body.Error.InnerHttpError.StatusCode);
            }
        }

        [Fact]
        public async Task DeleteActivityAsync_ShouldThrowWithoutErrorBody()
        {
            var conversationsClient = UseConversation();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(InternalErrorResponse);

            var exMessage = $"DeleteActivity operation returned an invalid status code '(500) {InternalErrorResponse.StatusCode}'";

            try
            {
                await conversationsClient.DeleteActivityAsync(TestActivity.Conversation.Id, TestActivity.Id);
            }
            catch (ErrorResponseException ex)
            {
                Assert.Null(ex.Body);
                Assert.Equal(exMessage, ex.Message);
            }
        }

        [Fact]
        public async Task GetConversationMembersAsync_ShouldThrowOnNullConversationId()
        {
            var conversationsClient = UseConversation();

            await Assert.ThrowsAsync<ArgumentNullException>(() => conversationsClient.GetConversationMembersAsync(null));
        }

        [Fact]
        public async Task GetConversationMembersAsync_ShouldReturnChannelAccounts()
        {
            var conversationsClient = UseConversation();

            var accounts = new List<ChannelAccount> { new ChannelAccount { Id = "account-id" } };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(accounts))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);


            var result = await conversationsClient.GetConversationMembersAsync(TestActivity.Conversation.Id);

            Assert.Equal(accounts[0].Id, result[0].Id);
        }

        [Fact]
        public async Task GetConversationMembersAsync_ShouldThrowWithErrorBody()
        {
            var conversationsClient = UseConversation();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(NotFoundResponse);

            try
            {
                _ = await conversationsClient.GetConversationMembersAsync(TestActivity.Conversation.Id);
            }
            catch (ErrorResponseException ex)
            {
                Assert.Equal(NotFoundError.Code, ex.Body.Error.Code);
                Assert.Equal(NotFoundError.Message, ex.Body.Error.Message);
                Assert.Equal(NotFoundError.InnerHttpError.StatusCode, ex.Body.Error.InnerHttpError.StatusCode);
            }
        }

        [Fact]
        public async Task GetConversationMembersAsync_ShouldThrowWithoutErrorBody()
        {
            var conversationsClient = UseConversation();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(InternalErrorResponse);

            var exMessage = $"GetConversationMembers operation returned an invalid status code '(500) {InternalErrorResponse.StatusCode}'";

            try
            {
                _ = await conversationsClient.GetConversationMembersAsync(TestActivity.Conversation.Id);
            }
            catch (ErrorResponseException ex)
            {
                Assert.Null(ex.Body);
                Assert.Equal(exMessage, ex.Message);
            }
        }

        [Fact]
        public async Task GetConversationMemberAsync_ShouldThrowOnNullConversationId()
        {
            var conversationsClient = UseConversation();
            var activity = new Activity
            {
                Id = null,
                Conversation = new ConversationAccount { Id = "conversation-id" }
            };

            await Assert.ThrowsAsync<ArgumentNullException>(() => conversationsClient.GetConversationMemberAsync(UserId, null));
        }

        [Fact]
        public async Task GetConversationMemberAsync_ShouldThrowOnNullUserId()
        {
            var conversationsClient = UseConversation();

            await Assert.ThrowsAsync<ArgumentNullException>(() => conversationsClient.GetConversationMemberAsync(null, TestActivity.Conversation.Id));
        }

        [Fact]
        public async Task GetConversationMemberAsync_ShouldReturnChannelAccount()
        {
            var conversationsClient = UseConversation();

            var account = new ChannelAccount { Id = "account-id" };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(account))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

            var result = await conversationsClient.GetConversationMemberAsync(UserId, TestActivity.Conversation.Id);

            Assert.Equal(account.Id, result.Id);
        }

        [Fact]
        public async Task GetConversationMemberAsync_ShouldThrowWithErrorBody()
        {
            var conversationsClient = UseConversation();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(NotFoundResponse);

            try
            {
                _ = await conversationsClient.GetConversationMemberAsync(UserId, TestActivity.Conversation.Id);
            }
            catch (ErrorResponseException ex)
            {
                Assert.Equal(NotFoundError.Code, ex.Body.Error.Code);
                Assert.Equal(NotFoundError.Message, ex.Body.Error.Message);
                Assert.Equal(NotFoundError.InnerHttpError.StatusCode, ex.Body.Error.InnerHttpError.StatusCode);
            }
        }

        [Fact]
        public async Task GetConversationMemberAsync_ShouldThrowWithoutErrorBody()
        {
            var conversationsClient = UseConversation();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(InternalErrorResponse);

            var exMessage = $"GetConversationMember operation returned an invalid status code '(500) {InternalErrorResponse.StatusCode}'";

            try
            {
                _ = await conversationsClient.GetConversationMemberAsync(UserId, TestActivity.Conversation.Id);
            }
            catch (ErrorResponseException ex)
            {
                Assert.Null(ex.Body);
                Assert.Equal(exMessage, ex.Message);
            }
        }

        [Fact]
        public async Task DeleteConversationMemberAsync_ShouldThrowOnNullConversationId()
        {
            var conversationsClient = UseConversation();

            await Assert.ThrowsAsync<ArgumentNullException>(() => conversationsClient.DeleteConversationMemberAsync(null, MemberId));
        }

        [Fact]
        public async Task DeleteConversationMemberAsync_ShouldThrowOnNullMemberId()
        {
            var conversationsClient = UseConversation();

            await Assert.ThrowsAsync<ArgumentNullException>(() => conversationsClient.DeleteConversationMemberAsync(TestActivity.Conversation.Id, null));
        }

        [Fact]
        public async Task DeleteConversationMemberAsync_ShouldCallSendRequest()
        {
            var conversationsClient = UseConversation();

            var response = new HttpResponseMessage(HttpStatusCode.OK);

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

            await conversationsClient.DeleteConversationMemberAsync(TestActivity.Conversation.Id, MemberId);

            MockHttpClient.Verify(service => service.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteConversationMemberAsync_ShouldThrowWithErrorBody()
        {
            var conversationsClient = UseConversation();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(NotFoundResponse);

            try
            {
                await conversationsClient.DeleteConversationMemberAsync(TestActivity.Conversation.Id, MemberId);
            }
            catch (ErrorResponseException ex)
            {
                Assert.Equal(NotFoundError.Code, ex.Body.Error.Code);
                Assert.Equal(NotFoundError.Message, ex.Body.Error.Message);
                Assert.Equal(NotFoundError.InnerHttpError.StatusCode, ex.Body.Error.InnerHttpError.StatusCode);
            }
        }

        [Fact]
        public async Task DeleteConversationMemberAsync_ShouldThrowWithoutErrorBody()
        {
            var conversationsClient = UseConversation();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(InternalErrorResponse);

            var exMessage = $"DeleteConversationMember operation returned an invalid status code '(500) {InternalErrorResponse.StatusCode}'";

            try
            {
                await conversationsClient.DeleteConversationMemberAsync(TestActivity.Conversation.Id, MemberId);
            }
            catch (ErrorResponseException ex)
            {
                Assert.Null(ex.Body);
                Assert.Equal(exMessage, ex.Message);
            }
        }

        [Fact]
        public async Task GetConversationPagedMembersAsync_ShouldThrowOnNullConversationId()
        {
            var conversationsClient = UseConversation();

            await Assert.ThrowsAsync<ArgumentNullException>(() => conversationsClient.GetConversationPagedMembersAsync(null, PageSize));
        }

        [Fact]
        public async Task GetConversationPagedMembersAsync_ShouldReturnPagedMembers()
        {
            var conversationsClient = UseConversation();

            var accounts = new List<ChannelAccount> { new ChannelAccount { Id = "member-1" } };

            var pagedMembers = new PagedMembersResult { Members = accounts };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(pagedMembers))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

            var result = await conversationsClient.GetConversationPagedMembersAsync(TestActivity.Conversation.Id, PageSize);

            Assert.Equal(pagedMembers.Members[0].Id, result.Members[0].Id);
        }

        [Fact]
        public async Task GetConversationPagedMembersAsync_ShouldThrowWithErrorBody()
        {
            var conversationsClient = UseConversation();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(NotFoundResponse);

            try
            {
                _ = await conversationsClient.GetConversationPagedMembersAsync(TestActivity.Conversation.Id, PageSize);
            }
            catch (ErrorResponseException ex)
            {
                Assert.Equal(NotFoundError.Code, ex.Body.Error.Code);
                Assert.Equal(NotFoundError.Message, ex.Body.Error.Message);
                Assert.Equal(NotFoundError.InnerHttpError.StatusCode, ex.Body.Error.InnerHttpError.StatusCode);
            }
        }

        [Fact]
        public async Task GetConversationPagedMembersAsync_ShouldThrowWithoutErrorBody()
        {
            var conversationsClient = UseConversation();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(InternalErrorResponse);

            var exMessage = $"GetConversationPagedMembers operation returned an invalid status code '(500) {InternalErrorResponse.StatusCode}'";

            try
            {
                _ = await conversationsClient.GetConversationPagedMembersAsync(TestActivity.Conversation.Id, PageSize);
            }
            catch (ErrorResponseException ex)
            {
                Assert.Null(ex.Body);
                Assert.Equal(exMessage, ex.Message);
            }
        }

        [Fact]
        public async Task GetActivityMembersAsync_ShouldThrowOnNullConversationId()
        {
            var conversationsClient = UseConversation();

            await Assert.ThrowsAsync<ArgumentNullException>(() => conversationsClient.GetActivityMembersAsync(null, TestActivity.Id));
        }

        [Fact]
        public async Task GetActivityMembersAsync_ShouldReturnChannelAccounts()
        {
            var conversationsClient = UseConversation();

            var accounts = new List<ChannelAccount> { new ChannelAccount { Id = "account-id" } };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(accounts))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);


            var result = await conversationsClient.GetActivityMembersAsync(TestActivity.Conversation.Id, TestActivity.Id);

            Assert.Equal(accounts[0].Id, result[0].Id);
        }

        [Fact]
        public async Task GetActivityMembersAsync_ShouldThrowWithErrorBody()
        {
            var conversationsClient = UseConversation();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(NotFoundResponse);

            try
            {
                _ = await conversationsClient.GetActivityMembersAsync(TestActivity.Conversation.Id, TestActivity.Id);
            }
            catch (ErrorResponseException ex)
            {
                Assert.Equal(NotFoundError.Code, ex.Body.Error.Code);
                Assert.Equal(NotFoundError.Message, ex.Body.Error.Message);
                Assert.Equal(NotFoundError.InnerHttpError.StatusCode, ex.Body.Error.InnerHttpError.StatusCode);
            }
        }

        [Fact]
        public async Task GetActivityMembersAsync_ShouldThrowWithoutErrorBody()
        {
            var conversationsClient = UseConversation();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(InternalErrorResponse);

            var exMessage = $"GetActivityMembers operation returned an invalid status code '(500) {InternalErrorResponse.StatusCode}'";

            try
            {
                _ = await conversationsClient.GetActivityMembersAsync(TestActivity.Conversation.Id, TestActivity.Id);
            }
            catch (ErrorResponseException ex)
            {
                Assert.Null(ex.Body);
                Assert.Equal(exMessage, ex.Message);
            }
        }

        [Fact]
        public async Task UploadAttachmentAsync_ShouldThrowOnNullConversationId()
        {
            var conversationsClient = UseConversation();

            await Assert.ThrowsAsync<ArgumentNullException>(() => conversationsClient.UploadAttachmentAsync(null));
        }

        [Fact]
        public async Task UploadAttachmentAsync_ShouldReturnResourceResponse()
        {
            var conversationsClient = UseConversation();

            var data = new AttachmentData("att-type", "att-name");

            var resourceResponse = new ResourceResponse { Id = "resource-id" };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(resourceResponse))
            };

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);


            var result = await conversationsClient.UploadAttachmentAsync(TestActivity.Conversation.Id, data);

            Assert.Equal(resourceResponse.Id, result.Id);
        }

        [Fact]
        public async Task UploadAttachmentAsync_ShouldThrowWithErrorBody()
        {
            var conversationsClient = UseConversation();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(NotFoundResponse);

            try
            {
                _ = await conversationsClient.UploadAttachmentAsync(TestActivity.Conversation.Id);
            }
            catch (ErrorResponseException ex)
            {
                Assert.Equal(NotFoundError.Code, ex.Body.Error.Code);
                Assert.Equal(NotFoundError.Message, ex.Body.Error.Message);
                Assert.Equal(NotFoundError.InnerHttpError.StatusCode, ex.Body.Error.InnerHttpError.StatusCode);
            }
        }

        [Fact]
        public async Task UploadAttachmentAsync_ShouldThrowWithoutErrorBody()
        {
            var conversationsClient = UseConversation();

            MockHttpClient.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(InternalErrorResponse);

            var exMessage = $"UploadAttachment operation returned an invalid status code '(500) {InternalErrorResponse.StatusCode}'";

            try
            {
                _ = await conversationsClient.UploadAttachmentAsync(TestActivity.Conversation.Id);
            }
            catch (ErrorResponseException ex)
            {
                Assert.Null(ex.Body);
                Assert.Equal(exMessage, ex.Message);
            }
        }

        private ConversationsRestClient UseConversation()
        {
            var transport = new Mock<IRestTransport>();
            transport.Setup(x => x.Endpoint)
                .Returns(UriEndpoint);
            transport.Setup(a => a.GetHttpClientAsync())
                .Returns(Task.FromResult(MockHttpClient.Object));

            return new ConversationsRestClient(transport.Object);
        }
    }
}
