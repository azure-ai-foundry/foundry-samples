// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Builder.UserAuth.TokenService;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Moq;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public class OAuthFlowTests
    {
        private readonly OAuthFlow _flow;

        public OAuthFlowTests()
        {
            _flow = new OAuthFlow(new OAuthSettings()
            {
                AzureBotOAuthConnectionName = "connection name",
                Title = "Test flow", 
                Text = "testing oauth flow", 
                Timeout = 1000
            });
        }

        [Fact]
        public async Task SignOutUserAsync_ShouldThrowOnNullUserTokenClient()
        {
            var context = new TurnContext(new SimpleAdapter(), new Activity());
            await Assert.ThrowsAsync<NotSupportedException>(async() => await _flow.SignOutUserAsync(context, CancellationToken.None));
        }
        
        [Fact]
        public async Task SignOutUserAsync_ShouldLogOutSuccessfully()
        {
            //Arrange
            var userId = "user-id";
            var channelId = "channel-id";
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                From = new ChannelAccount { Id = userId },
                ChannelId = channelId,
                Text = "logout",
            };
            var context = new TurnContext(new SimpleAdapter(), activity);
            var mockUserTokenClient = new Mock<IUserTokenClient>();
            mockUserTokenClient.Setup(
                x => x.SignOutUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ChannelId>(), It.IsAny<CancellationToken>()));

            context.Services.Set<IUserTokenClient>(mockUserTokenClient.Object);

            //Act
            await _flow.SignOutUserAsync(context, CancellationToken.None);

            // Assert
            mockUserTokenClient.Verify(
                x => x.SignOutUserAsync(It.Is<string>(s => s == userId), It.Is<string>(s => s == _flow.Settings.AzureBotOAuthConnectionName), It.Is<ChannelId>(s => s.Channel == channelId), It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task ContinueFlowAsync_ShouldSendMessageWithNotFoundStatus()
        {
            //Arrange
            bool responsesSent = false;
            void ValidateResponses(IActivity[] activities)
            {
                var activityValue = (InvokeResponse)activities[0].Value;
                Assert.Equal((int)HttpStatusCode.NotFound, activityValue.Status);
                responsesSent = true;
            }

            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name= SignInConstants.VerifyStateOperationName,
                From = new ChannelAccount { Id = "user-id" },
                ChannelId = "channel-id",
                Text = "invoke",
            };
            var context = new TurnContext(new SimpleAdapter(ValidateResponses), activity);
            var mockUserTokenClient = new Mock<IUserTokenClient>();
            mockUserTokenClient.Setup(
                x => x.GetUserTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ChannelId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((TokenResponse)null);

            context.Services.Set<IUserTokenClient>(mockUserTokenClient.Object);

            //Act
            var result = await _flow.ContinueFlowAsync(context, DateTime.UtcNow.AddHours(1), CancellationToken.None);

            // Assert
            Assert.Null(result);
            Assert.True(responsesSent);
        }

        [Fact]
        public async Task ContinueFlowAsync_ShouldSendMessageWithInternalServerErrorStatus()
        {
            //Arrange
            bool responsesSent = false;
            void ValidateResponses(IActivity[] activities)
            {
                var activityValue = (InvokeResponse)activities[0].Value;
                Assert.Equal((int)HttpStatusCode.InternalServerError, activityValue.Status);
                responsesSent = true;
            }

            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = SignInConstants.VerifyStateOperationName,
                From = new ChannelAccount { Id = "user-id" },
                ChannelId = "channel-id",
                Text = "invoke",
            };
            var context = new TurnContext(new SimpleAdapter(ValidateResponses), activity);

            //Act
            var result = await _flow.ContinueFlowAsync(context, DateTime.UtcNow.AddHours(1), CancellationToken.None);

            // Assert
            Assert.Null(result);
            Assert.True(responsesSent);
        }

        [Fact]
        public async Task ContinueFlowAsync_ShouldSendMessageWithPreconditionFailedStatus()
        {
            //Arrange
            bool responsesSent = false;
            void ValidateResponses(IActivity[] activities)
            {
                var activityValue = (InvokeResponse)activities[0].Value;
                var messageBody = (TokenExchangeInvokeResponse)activityValue.Body;
                Assert.Equal((int)HttpStatusCode.PreconditionFailed, activityValue.Status);
                Assert.Equal("The Agent is unable to exchange token. Proceed with regular login.", messageBody.FailureDetail);
                responsesSent = true;
            }

            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = SignInConstants.TokenExchangeOperationName,
                Value = new TokenResponse(Channels.Msteams, _flow.Settings.AzureBotOAuthConnectionName, "token", DateTime.Parse("Tuesday, April 15, 2025 6:03:20 PM")),
                From = new ChannelAccount { Id = "user-id" },
                ChannelId = "channel-id",
                Text = "invoke",
            };

            var context = new TurnContext(new SimpleAdapter(ValidateResponses), activity);

            var mockTokenClient = new Mock<IUserTokenClient>();
            mockTokenClient
                .Setup(c => c.ExchangeTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ChannelId>(), It.IsAny<TokenExchangeRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ErrorResponseException("exception") { Body = new ErrorResponse() { Error = new Error() { Code = Error.ConsentRequiredCode } } });

            context.Services.Set(mockTokenClient.Object);

            //Act
            await _flow.ContinueFlowAsync(context, DateTime.UtcNow.AddHours(1), CancellationToken.None);
            Assert.True(responsesSent);
        }
    }
}
