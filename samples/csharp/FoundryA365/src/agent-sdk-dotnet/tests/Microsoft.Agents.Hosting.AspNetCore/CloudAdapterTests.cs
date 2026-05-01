// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.Compat;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Connector.Types;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.Tests
{
    [Collection("CloudAdapter Collection")]
    public class CloudAdapterTests
    {
        private const string TestServiceUrl = "https://test.serviceurl.com/";

        [Fact]
        public void Constructor_ShouldThrowWithNullActivityTaskQueue()
        {
            var factory = new Mock<IChannelServiceClientFactory>();

            Assert.Throws<ArgumentNullException>(() => new CloudAdapter(factory.Object, null));
        }

        [Fact]
        public async Task OnTurnError_ShouldSendExceptionActivity()
        {
            var record = UseRecord(null);
            var context = new Mock<ITurnContext>();
            var exception = new ErrorResponseException("test") { Body = new ErrorResponse() };

            context.Setup(e => e.SendActivityAsync(It.IsAny<Activity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse())
                .Verifiable(Times.Once);
            context.Setup(e => e.TraceActivityAsync(
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse())
                .Verifiable(Times.Once);

            await record.Adapter.OnTurnError(context.Object, exception);

            Mock.Verify(context);
            record.VerifyMocks();
        }

        [Fact]
        public async Task ProcessAsync_ShouldThrowWithNullHttpRequest()
        {
            var record = UseRecord(null);
            var context = new DefaultHttpContext();
            var bot = new ActivityHandler();

            await Assert.ThrowsAsync<ArgumentNullException>(() => record.Adapter.ProcessAsync(null, context.Response, bot, CancellationToken.None));
        }

        [Fact]
        public async Task ProcessAsync_ShouldThrowWithNullHttpResponse()
        {
            var record = UseRecord(null);
            var context = new DefaultHttpContext();
            var bot = new ActivityHandler();

            await Assert.ThrowsAsync<ArgumentNullException>(() => record.Adapter.ProcessAsync(context.Request, null, bot, CancellationToken.None));
        }

        [Fact]
        public async Task ProcessAsync_ShouldThrowWithNullBot()
        {
            var record = UseRecord(null);
            var context = new DefaultHttpContext();

            await Assert.ThrowsAsync<ArgumentNullException>(() => record.Adapter.ProcessAsync(context.Request, context.Response, null, CancellationToken.None));
        }

        [Fact]
        public async Task ProcessAsync_ShouldSetMethodNotAllowedStatus()
        {
            var record = UseRecord(null);
            var context = new DefaultHttpContext();
            var bot = new ActivityHandler();

            await record.Adapter.ProcessAsync(context.Request, context.Response, bot, CancellationToken.None);

            Assert.Equal(StatusCodes.Status405MethodNotAllowed, context.Response.StatusCode);
            record.VerifyMocks();
        }

        [Fact]
        public async Task ProcessAsync_ShouldSetBadRequestStatus()
        {
            var bot = new ActivityHandler();
            var record = UseRecord((record) => bot);
            var context = CreateHttpContext(new());  // no Activity == bad request

            await record.Adapter.ProcessAsync(context.Request, context.Response, bot, CancellationToken.None);

            Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
            record.VerifyMocks();
        }

        [Fact]
        public async Task ProcessAsync_ShouldSetInvokeResponseNotImplemented()
        {
            var record = UseRecord((record) => new ActivityHandler());
            var context = CreateHttpContext(CreateInvokeActivity());

            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);

            // ActivityHandler returns 501 for unnamed Invokes by default
            Assert.Equal(StatusCodes.Status501NotImplemented, context.Response.StatusCode);
        }

        [Fact]
        public async Task ProcessAsync_InvokeShouldSetExpectedReplies()
        {
            // Returns an ExpectedReplies with one Activity and a Body of TokenResponse
            var record = UseRecord((record) => new RespondingActivityHandler());
            var activity = CreateInvokeActivity();
            var context = CreateHttpContext(activity);

            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

            var expectedReplies = ReadExpectedReplies(context);
            Assert.NotNull(expectedReplies);
            Assert.NotEmpty(expectedReplies.Activities);
            Assert.NotNull(expectedReplies.Body);

            var tokenResponse = ProtocolJsonSerializer.ToObject<TokenResponse>(expectedReplies.Body);
            Assert.NotNull(tokenResponse);
            Assert.Equal($"token:{activity.Conversation.Id}", tokenResponse.Token);
        }

        [Fact]
        public async Task ProcessAsync_DeliveryModeNormalShouldSetInvokeResponse()
        {
            var record = UseRecord((record) => new RespondingActivityHandler());
            var activity = CreateInvokeActivity(DeliveryModes.Normal);
            var context = CreateHttpContext(activity);
            SetupConnectorClient(record);

            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var tokenResponse = ProtocolJsonSerializer.ToObject<TokenResponse>(new StreamReader(context.Response.Body).ReadToEnd());
            Assert.NotNull(tokenResponse);
            Assert.Equal($"token:{activity.Conversation.Id}", tokenResponse.Token);
        }

        [Fact]
        public async Task ProcessAsync_DeliveryModeNormalMessage()
        {
            var record = UseRecord((record) => new RespondingActivityHandler());
            var activity = CreateMessageActivity(DeliveryModes.Normal, activityId: Guid.NewGuid().ToString());
            var context = CreateHttpContext(activity);
            var turnStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sentActivities = new List<IActivity>();
            SetupConnectorClient(record, sentActivities, firstActivitySent: turnStarted);

            await record.Service.StartAsync(CancellationToken.None);
            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);

            var timedOut = await Task.WhenAny(turnStarted.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.True(timedOut == turnStarted.Task, "Background turn did not complete within timeout");
            await record.Service.StopAsync(CancellationToken.None);

            Assert.Equal(StatusCodes.Status202Accepted, context.Response.StatusCode);
            Assert.Equal(3, sentActivities.Count);
            Assert.Equal($"Response {activity.Conversation.Id}:{activity.Id}:0", sentActivities[0].Text);
            Assert.Equal($"Response {activity.Conversation.Id}:{activity.Id}:1", sentActivities[1].Text);
            Assert.Equal($"Response {activity.Conversation.Id}:{activity.Id}:2", sentActivities[2].Text);
        }

        [Fact]
        public async Task ProcessAsync_DeliveryModeNormalMessageWithThrow()
        {
            var record = UseRecord((record) => new RespondingActivityHandler());
            var activity = CreateMessageActivity(DeliveryModes.Normal, text: "throw");
            var context = CreateHttpContext(activity);

            var turnStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sentActivities = new List<IActivity>();
            SetupConnectorClient(record, sentActivities, firstActivitySent: turnStarted);

            await record.Service.StartAsync(CancellationToken.None);
            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);

            var timedOut = await Task.WhenAny(turnStarted.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.True(timedOut == turnStarted.Task, "Background turn did not complete within timeout");
            await record.Service.StopAsync(CancellationToken.None);

            Assert.Equal(StatusCodes.Status202Accepted, context.Response.StatusCode);
            Assert.Single(sentActivities);
            Assert.Equal("Test exception", sentActivities[0].Text);
        }

        [Fact]
        public async Task ProcessAsync_ExpectReplies()
        {
            var record = UseRecord((record) => new RespondingActivityHandler());
            var convoId = Guid.NewGuid().ToString();
            var activity = CreateMessageActivity(DeliveryModes.ExpectReplies, conversationId: convoId, text: convoId, activityId: convoId);
            var context = CreateHttpContext(activity);

            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);

            var expectedReplies = ReadExpectedReplies(context);
            Assert.NotNull(expectedReplies);
            Assert.Equal(3, expectedReplies.Activities.Count);
            Assert.Equal($"Response {convoId}:{convoId}:0", expectedReplies.Activities[0].Text);
            Assert.Equal($"Response {convoId}:{convoId}:1", expectedReplies.Activities[1].Text);
            Assert.Equal($"Response {convoId}:{convoId}:2", expectedReplies.Activities[2].Text);
        }

        [Fact]
        public async Task ProcessAsync_ShouldStreamResponses()
        {
            var record = UseRecord((record) => new RespondingActivityHandler());
            var context = CreateHttpContext(CreateInvokeActivity(DeliveryModes.Stream, activityId: Guid.NewGuid().ToString()));

            // Test
            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var reader = new StreamReader(context.Response.Body);

            string conversationId = null;
            int lineNumber = 0;
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (lineNumber == 0)
                {
                    Assert.StartsWith("event: activity", line);
                }
                else if (lineNumber == 1)
                {
                    Assert.StartsWith("data: ", line);
                    var activity = ProtocolJsonSerializer.ToObject<Activity>(line.Substring(6));
                    Assert.NotNull(activity);
                    Assert.Equal("Test Response", activity.Text);
                    conversationId = activity.Conversation.Id;
                    Assert.NotNull(conversationId);
                }
                else if (lineNumber == 2)
                {
                    Assert.Equal(0, line.Length);
                }
                else if (lineNumber == 3)
                {
                    Assert.StartsWith("event: invokeResponse", line);
                }
                else if (lineNumber == 4)
                {
                    Assert.StartsWith("data: ", line);
                    var invokeResponse = ProtocolJsonSerializer.ToObject<InvokeResponse>(line.Substring(6));
                    Assert.NotNull(invokeResponse);

                    var tokenResponse = ProtocolJsonSerializer.ToObject<TokenResponse>(invokeResponse.Body);
                    Assert.NotNull(tokenResponse);
                    Assert.Equal($"token:{conversationId}", tokenResponse.Token);
                }

                lineNumber++;
            }

            Assert.Equal(6, lineNumber);
            Assert.NotNull(conversationId);
        }

        [Fact]
        public async Task ProcessAsync_ExpectRepliesWithContinueConversation()
        {
            var fromId = Guid.NewGuid().ToString();
            var record = UseRecord((record) =>
            {
                var agent = new TestApplication(new TestApplicationOptions(record.Storage));
                agent.OnActivity(ActivityTypes.Message, async (context, state, ct) =>
                {
                    await context.SendActivityAsync($"Outer: {context.Activity.Text}", cancellationToken: ct);
                    await context.Adapter.ContinueConversationAsync(
                        context.Identity,
                        context.Activity.GetConversationReference(),
                        async (innerContext, innerCt) =>
                        {
                            await Task.Delay(1000);
                            await innerContext.SendActivityAsync($"Inner: {context.Activity.Text}", cancellationToken: innerCt);
                        },
                        ct);
                });
                return agent;
            });

            var activity = CreateMessageActivity(DeliveryModes.ExpectReplies, text: "user message", activityId: "1", fromId: fromId);
            var context = CreateHttpContext(activity);

            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

            var expectedReplies = ReadExpectedReplies(context);
            Assert.NotNull(expectedReplies);
            Assert.Equal(2, expectedReplies.Activities.Count);

            Assert.Equal("Outer: user message", expectedReplies.Activities[0].Text);
            Assert.Equal(activity.Conversation.Id, expectedReplies.Activities[0].Conversation.Id);
            Assert.Equal(fromId, expectedReplies.Activities[0].Recipient.Id);
            Assert.Equal("1", expectedReplies.Activities[0].ReplyToId);

            // Inner turn has same conversation info as incoming
            Assert.Equal("Inner: user message", expectedReplies.Activities[1].Text);
            Assert.Equal(activity.Conversation.Id, expectedReplies.Activities[1].Conversation.Id);
            Assert.Equal(fromId, expectedReplies.Activities[1].Recipient.Id);
            Assert.Equal("1", expectedReplies.Activities[1].ReplyToId);
        }

        [Fact]
        public async Task ProcessAsync_Proactive()
        {
            var proactiveReference = new ConversationReference()
            {
                ServiceUrl = "https://madeup.com",
                Conversation = new(id: Guid.NewGuid().ToString()),
                ActivityId = Guid.NewGuid().ToString(),
                User = new ChannelAccount(id: Guid.NewGuid().ToString(), role: RoleTypes.User),
                Agent = new(id: "recipientId", role: RoleTypes.Agent),
            };

            var fromId = Guid.NewGuid().ToString();
            var record = UseRecord((record) =>
            {
                var agent = new TestApplication(new TestApplicationOptions(record.Storage));
                agent.OnActivity(ActivityTypes.Message, async (context, state, ct) =>
                {
                    await context.SendActivityAsync($"Outer: {context.Activity.Text}", cancellationToken: ct);
                    await context.Adapter.ContinueConversationAsync(
                        context.Identity,
                        proactiveReference,
                        async (innerContext, innerCt) =>
                        {
                            await innerContext.SendActivityAsync($"Proactive: {context.Activity.Text}", cancellationToken: innerCt);
                        },
                        ct);
                });
                return agent;
            });

            // Capture Connector calls. Proactive is always via Connector; outer (ExpectReplies) response is not.
            var proactiveActivities = new List<IActivity>();
            SetupConnectorClient(record, proactiveActivities);

            // ExpectReplies outer delivery isolates the outer response from the proactive one for easier assertions
            var activity = CreateMessageActivity(DeliveryModes.ExpectReplies, text: "user message", activityId: "1", fromId: fromId);
            var context = CreateHttpContext(activity);

            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

            var expectedReplies = ReadExpectedReplies(context);
            Assert.NotNull(expectedReplies);
            Assert.Single(expectedReplies.Activities);
            Assert.Equal("Outer: user message", expectedReplies.Activities[0].Text);
            Assert.Equal(activity.Conversation.Id, expectedReplies.Activities[0].Conversation.Id);
            Assert.Equal(fromId, expectedReplies.Activities[0].Recipient.Id);
            Assert.Equal("1", expectedReplies.Activities[0].ReplyToId);

            Assert.Single(proactiveActivities);
            Assert.Equal("Proactive: user message", proactiveActivities[0].Text);
            Assert.Equal(proactiveReference.Conversation.Id, proactiveActivities[0].Conversation.Id);
            Assert.Equal(proactiveReference.User.Id, proactiveActivities[0].Recipient.Id);
            Assert.Equal(proactiveReference.ActivityId, proactiveActivities[0].ReplyToId);
        }

        [Fact]
        public async Task ProcessAsync_CreateConversationNormalDelivery()
        {
            var turnDone = new EventWaitHandle(false, EventResetMode.AutoReset);
            var origConversationId = Guid.NewGuid().ToString();
            var newConversationId = Guid.NewGuid().ToString();
            const string serviceUrl = "https://service.com";

            var record = UseRecord((record) =>
            {
                var agent = new TestApplication(new TestApplicationOptions(record.Storage));
                agent.OnActivity(ActivityTypes.Message, async (context, state, ct) =>
                {
                    await context.SendActivityAsync($"Original Conversation: {context.Activity.Text}", cancellationToken: ct);
                    await context.Adapter.CreateConversationAsync(
                        "appid",
                        Channels.Test,
                        serviceUrl,
                        context.Identity.GetOutgoingAudience(),
                        new ConversationParameters() { Agent = context.Activity.Recipient, Members = [context.Activity.From] },
                        async (innerContext, innerCt) =>
                        {
                            Assert.Equal("appid", innerContext.Activity.Recipient.Id);
                            Assert.Equal("userid", innerContext.Activity.From.Id);

                            // TurnState isn't provided in the continuation lambda - load it manually
                            var turnState = agent.Options.TurnStateFactory();
                            await turnState.LoadStateAsync(innerContext, cancellationToken: innerCt);
                            turnState.Conversation.SetValue("lastConvoMessage", context.Activity.Text);
                            turnState.User.SetValue("lastConvoMessage", context.Activity.Text);
                            await innerContext.SendActivityAsync($"New Conversation: {context.Activity.Text}", cancellationToken: innerCt);
                            await turnState.SaveStateAsync(innerContext, cancellationToken: innerCt);
                        },
                        ct);
                    turnDone.Set();
                });
                return agent;
            });

            var responses = new List<IActivity>();
            SetupConnectorClient(record, responses, newConversationId);

            var activity = CreateMessageActivity(DeliveryModes.Normal, conversationId: origConversationId, text: "user message", activityId: "1", fromId: "userid", serviceUrl: serviceUrl);
            var context = CreateHttpContext(activity);

            await record.Service.StartAsync(CancellationToken.None);
            await Task.Run(async () =>
            {
                await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);
                Assert.Equal(StatusCodes.Status202Accepted, context.Response.StatusCode);
                Assert.Equal(0, context.Response.Body.Length);
            });

            Assert.True(turnDone.WaitOne(TimeSpan.FromSeconds(10)), "Turn did not complete within timeout");

            Assert.Equal(2, responses.Count);
            Assert.Equal("Original Conversation: user message", responses[0].Text);
            Assert.Equal(origConversationId, responses[0].Conversation.Id);
            Assert.Equal("1", responses[0].ReplyToId);
            Assert.Equal("New Conversation: user message", responses[1].Text);
            Assert.Equal(newConversationId, responses[1].Conversation.Id);
            Assert.Null(responses[1].ReplyToId);

            var items = await record.Storage.ReadAsync<IDictionary<string, object>>([$"{responses[1].ChannelId}/conversations/{responses[1].Conversation.Id}"]);
            Assert.True(items.First().Value.ContainsKey("lastConvoMessage"));
            items = await record.Storage.ReadAsync<IDictionary<string, object>>([$"{responses[1].ChannelId}/users/{responses[1].Recipient.Id}"]);
            Assert.True(items.First().Value.ContainsKey("lastConvoMessage"));

            await record.Service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task ProcessAsync_ContinueConversationNormalDelivery()
        {
            var turnDone = new EventWaitHandle(false, EventResetMode.AutoReset);
            var initialConversationId = Guid.NewGuid().ToString();
            var proactiveConversationId = Guid.NewGuid().ToString();
            const string serviceUrl = "https://service.com";

            var proactiveReference = new ConversationReference()
            {
                ServiceUrl = serviceUrl,
                Conversation = new(id: proactiveConversationId),
                ActivityId = Guid.NewGuid().ToString(),
                User = new ChannelAccount(id: Guid.NewGuid().ToString(), role: RoleTypes.User),
                Agent = new(id: "recipientId", role: RoleTypes.Agent),
                ChannelId = Channels.Test
            };

            var record = UseRecord((record) =>
            {
                var agent = new TestApplication(new TestApplicationOptions(record.Storage));
                agent.OnActivity(ActivityTypes.Message, async (context, state, ct) =>
                {
                    await context.SendActivityAsync($"Original Conversation: {context.Activity.Text}", cancellationToken: ct);
                    await context.Adapter.ContinueConversationAsync(
                        context.Identity,
                        proactiveReference,
                        async (innerContext, innerCt) =>
                        {
                            // TurnState isn't provided in the continuation lambda - load it manually
                            var turnState = agent.Options.TurnStateFactory();
                            await turnState.LoadStateAsync(innerContext, cancellationToken: innerCt);
                            turnState.Conversation.SetValue("lastConvoMessage", context.Activity.Text);
                            await innerContext.SendActivityAsync($"Proactive Conversation: {context.Activity.Text}", cancellationToken: innerCt);
                            await turnState.SaveStateAsync(innerContext, cancellationToken: innerCt);
                        },
                        ct);
                    turnDone.Set();
                });
                return agent;
            });

            // Proactive replies use ReplyToActivity (has replyToId); SendToConversation should never be called
            var responses = new List<IActivity>();
            var handler = SetupConnectorClient(record, responses);

            var activity = CreateMessageActivity(DeliveryModes.Normal, conversationId: initialConversationId, text: "user message", activityId: "1", serviceUrl: serviceUrl);
            var context = CreateHttpContext(activity);

            await record.Service.StartAsync(CancellationToken.None);
            await Task.Run(async () =>
            {
                await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);
                Assert.Equal(StatusCodes.Status202Accepted, context.Response.StatusCode);
                Assert.Equal(0, context.Response.Body.Length);
            });

            Assert.True(turnDone.WaitOne(TimeSpan.FromSeconds(10)), "Turn did not complete within timeout");

            Assert.False(handler.SendToConversationCalled, "SendToConversation should not be called; all replies have a replyToId");
            Assert.Equal(2, responses.Count);
            Assert.Equal("Original Conversation: user message", responses[0].Text);
            Assert.Equal(initialConversationId, responses[0].Conversation.Id);
            Assert.Equal("1", responses[0].ReplyToId);
            Assert.Equal("Proactive Conversation: user message", responses[1].Text);
            Assert.Equal(proactiveConversationId, responses[1].Conversation.Id);
            Assert.Equal(proactiveReference.ActivityId, responses[1].ReplyToId);

            var items = await record.Storage.ReadAsync<IDictionary<string, object>>([$"{responses[1].ChannelId}/conversations/{responses[1].Conversation.Id}"]);
            Assert.True(items.First().Value.ContainsKey("lastConvoMessage"));

            await record.Service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task ProcessAsync_OAuthExpectReplies()
        {
            int attempt = 0;
            var mockGraph = new Mock<IUserAuthorization>();
            mockGraph
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(() => attempt++ == 0
                    ? Task.FromResult((TokenResponse)null)
                    : Task.FromResult(new TokenResponse() { Token = "GraphToken", Expiration = DateTime.UtcNow + TimeSpan.FromMinutes(30) }));
            mockGraph.Setup(e => e.Name).Returns("graph");

            var record = UseRecord((record) =>
            {
                var options = new TestApplicationOptions(record.Storage)
                {
                    UserAuthorization = new UserAuthorizationOptions(NullLoggerFactory.Instance, record.Storage, new Mock<IConnections>().Object, mockGraph.Object) { AutoSignIn = UserAuthorizationOptions.AutoSignInOff }
                };
                var agent = new TestApplication(options);
                agent.OnMessage("-signin", async (context, state, ct) =>
                {
                    var token = await agent.UserAuthorization.GetTurnTokenAsync(context, cancellationToken: ct);
                    Assert.Equal("GraphToken", token);
                    await context.SendActivityAsync(token, cancellationToken: ct);
                }, autoSignInHandlers: ["graph"]);
                return agent;
            });

            await record.Service.StartAsync(CancellationToken.None);

            // First request: start sign-in (returns empty ExpectedReplies while waiting for code)
            var fromId = Guid.NewGuid().ToString();
            var activity = CreateMessageActivity(DeliveryModes.ExpectReplies, text: "-signin", activityId: "1", fromId: fromId);
            var context = CreateHttpContext(activity);

            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            var expectedReplies = ReadExpectedReplies(context);
            Assert.NotNull(expectedReplies);
            Assert.Empty(expectedReplies.Activities);

            // Second request: submit auth code (response should include the token)
            var activity2 = CreateMessageActivity(DeliveryModes.ExpectReplies, conversationId: activity.Conversation.Id, text: "123456", activityId: "2", fromId: fromId);
            context = CreateHttpContext(activity2);

            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            expectedReplies = ReadExpectedReplies(context);
            Assert.NotNull(expectedReplies);
            Assert.NotEmpty(expectedReplies.Activities);

            // Response is to the original "-signin" message (replyToId = "1")
            Assert.Equal("GraphToken", expectedReplies.Activities[0].Text);
            Assert.Equal(activity.Conversation.Id, expectedReplies.Activities[0].Conversation.Id);
            Assert.Equal(fromId, expectedReplies.Activities[0].Recipient.Id);
            Assert.Equal("1", expectedReplies.Activities[0].ReplyToId);
        }

        [Fact]
        public async Task ProcessAsync_ShouldLogMissingConversationId()
        {
            var record = UseRecord((record) => new ActivityHandler());
            var context = CreateHttpContext(new(ActivityTypes.Message));

            /*
            record.QueueLogger.Setup(e => e.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((e, _) => e.ToString().StartsWith("BadRequest: Missing Conversation.Id")),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()))
                .Verifiable(Times.Once);
            */

            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);

            Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
            record.VerifyMocks();
        }

        [Fact]
        public async Task ProcessAsync_ShouldLogMissingActivity()
        {
            var record = UseRecord((record) => new ActivityHandler());
            var context = CreateHttpContext();

            /*
            record.QueueLogger.Setup(e => e.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((e, _) => e.ToString().StartsWith("BadRequest: Missing activity")),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()))
                .Verifiable(Times.Once);
            */

            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);

            Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
            record.VerifyMocks();
        }
        
        [Fact]
        public async Task ProcessAsync_CancellationDuringStream_ShouldNotThrow()
        {
            var agentStarted = new TaskCompletionSource<bool>();
            var record = UseRecord((_) => new DelayedActivityHandler(agentStarted));
            var context = CreateHttpContext(CreateInvokeActivity(DeliveryModes.Stream));

            using var cts = new CancellationTokenSource();

            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, cts.Token);
            await agentStarted.Task;
            cts.Cancel();

            Assert.NotEqual(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        }

        [Fact]
        public async Task ProcessAsync_ShouldTreatNullDeliveryModeAsNormal()
        {
            // Null DeliveryMode should be treated the same as Normal: queued for background processing, returns 202
            var record = UseRecord((record) => new RespondingActivityHandler());
            var activity = CreateMessageActivity(deliveryMode: null);
            var context = CreateHttpContext(activity);
            SetupConnectorClient(record);

            await record.Service.StartAsync(CancellationToken.None);
            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);
            await record.Service.StopAsync(CancellationToken.None);

            Assert.Equal(StatusCodes.Status202Accepted, context.Response.StatusCode);
        }

        [Fact]
        public async Task ProcessAsync_NormalMessage_ConnectorNetworkError_ShouldReturn202()
        {
            var record = UseRecord((record) => new RespondingActivityHandler());
            var activity = CreateMessageActivity(DeliveryModes.Normal);
            var context = CreateHttpContext(activity);
            var handler = SetupConnectorClient(record);
            handler.Override = _ => throw new HttpRequestException("Simulated network error");

            await record.Service.StartAsync(CancellationToken.None);
            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);
            await record.Service.StopAsync(CancellationToken.None);

            Assert.Equal(StatusCodes.Status202Accepted, context.Response.StatusCode);
        }

        [Fact]
        public async Task ProcessAsync_NormalMessage_ConnectorHttpError_ShouldReturn202()
        {
            var record = UseRecord((record) => new RespondingActivityHandler());
            var activity = CreateMessageActivity(DeliveryModes.Normal);
            var context = CreateHttpContext(activity);
            var handler = SetupConnectorClient(record);
            handler.Override = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);

            await record.Service.StartAsync(CancellationToken.None);
            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);
            await record.Service.StopAsync(CancellationToken.None);

            Assert.Equal(StatusCodes.Status202Accepted, context.Response.StatusCode);
        }

        [Fact]
        public async Task ProcessAsync_NormalMessage_ConnectorNetworkError_ShouldCallOnTurnError()
        {
            // OnTurnError is called with the raw HttpRequestException from the broken connector
            var errorCaptured = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            var record = UseRecord((record) => new RespondingActivityHandler());
            record.Adapter.OnTurnError = (_, ex) => { errorCaptured.TrySetResult(ex); return Task.CompletedTask; };
            var activity = CreateMessageActivity(DeliveryModes.Normal);
            var context = CreateHttpContext(activity);
            var handler = SetupConnectorClient(record);
            handler.Override = _ => throw new HttpRequestException("Simulated network error");

            await record.Service.StartAsync(CancellationToken.None);
            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);

            var timedOut = await Task.WhenAny(errorCaptured.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.True(timedOut == errorCaptured.Task, "OnTurnError was not called within timeout");
            await record.Service.StopAsync(CancellationToken.None);

            var captured = await errorCaptured.Task;
            Assert.IsType<HttpRequestException>(captured);
            Assert.Equal("Simulated network error", captured.Message);
        }

        [Fact]
        public async Task ProcessAsync_NormalMessage_ConnectorHttpError_ShouldCallOnTurnError()
        {
            // A non-2xx HTTP response from the connector produces an ErrorResponseException in OnTurnError
            var errorCaptured = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            var record = UseRecord((record) => new RespondingActivityHandler());
            record.Adapter.OnTurnError = (_, ex) => { errorCaptured.TrySetResult(ex); return Task.CompletedTask; };
            var activity = CreateMessageActivity(DeliveryModes.Normal);
            var context = CreateHttpContext(activity);
            var handler = SetupConnectorClient(record);
            handler.Override = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);

            await record.Service.StartAsync(CancellationToken.None);
            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);

            var timedOut = await Task.WhenAny(errorCaptured.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.True(timedOut == errorCaptured.Task, "OnTurnError was not called within timeout");
            await record.Service.StopAsync(CancellationToken.None);

            Assert.IsType<ErrorResponseException>(await errorCaptured.Task);
        }

        [Fact]
        public async Task ProcessAsync_NormalMessage_ConnectorUnauthorized_ShouldCallOnTurnError()
        {
            // A 401 response is special-cased by the connector: it produces OperationCanceledException (invalid token)
            var errorCaptured = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            var record = UseRecord((record) => new RespondingActivityHandler());
            record.Adapter.OnTurnError = (_, ex) => { errorCaptured.TrySetResult(ex); return Task.CompletedTask; };
            var activity = CreateMessageActivity(DeliveryModes.Normal);
            var context = CreateHttpContext(activity);
            var handler = SetupConnectorClient(record);
            handler.Override = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized);

            await record.Service.StartAsync(CancellationToken.None);
            await record.Adapter.ProcessAsync(context.Request, context.Response, record.Agent, CancellationToken.None);

            var timedOut = await Task.WhenAny(errorCaptured.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.True(timedOut == errorCaptured.Task, "OnTurnError was not called within timeout");
            await record.Service.StopAsync(CancellationToken.None);

            var captured = await errorCaptured.Task;
            Assert.IsType<OperationCanceledException>(captured);
            Assert.IsType<ErrorResponseException>(captured.InnerException);
        }

        private static Activity CreateMessageActivity(
            string deliveryMode = DeliveryModes.Normal,
            string conversationId = null,
            string text = null,
            string activityId = null,
            string fromId = "userId",
            string serviceUrl = null)
        {
            var activity = new Activity
            {
                ChannelId = Channels.Test,
                Type = ActivityTypes.Message,
                DeliveryMode = deliveryMode,
                Conversation = new(id: conversationId ?? Guid.NewGuid().ToString()),
                Recipient = new(id: "recipientId", role: RoleTypes.Agent),
                From = new(id: fromId, role: RoleTypes.User)
            };
            if (activityId != null) activity.Id = activityId;
            if (text != null) activity.Text = text;
            if (serviceUrl != null) activity.ServiceUrl = serviceUrl;
            return activity;
        }

        private static Activity CreateInvokeActivity(
            string deliveryMode = DeliveryModes.ExpectReplies,
            string conversationId = null,
            string activityId = null)
        {
            var activity = new Activity
            {
                ChannelId = Channels.Test,
                Type = ActivityTypes.Invoke,
                Name = "invoke",
                DeliveryMode = deliveryMode,
                Conversation = new(id: conversationId ?? Guid.NewGuid().ToString()),
                Recipient = new(id: "recipientId", role: RoleTypes.Agent),
                From = new(id: "fromId", role: RoleTypes.User)
            };
            if (activityId != null) activity.Id = activityId;
            return activity;
        }

        private static ExpectedReplies ReadExpectedReplies(DefaultHttpContext context)
        {
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            return ProtocolJsonSerializer.ToObject<ExpectedReplies>(new StreamReader(context.Response.Body).ReadToEnd());
        }

        /// <summary>
        /// Sets up the connector client factory to return a real RestConnectorClient backed by a TestHttpHandler,
        /// covering the full HTTP stack including serialization/deserialization.
        /// </summary>
        private static TestHttpHandler SetupConnectorClient(
            Record record,
            List<IActivity> captured = null,
            string newConversationId = null,
            TaskCompletionSource<bool> firstActivitySent = null)
        {
            var handler = new TestHttpHandler(captured, newConversationId, firstActivitySent);
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(() => new HttpClient(handler, disposeHandler: false));

            record.Factory
                .Setup(c => c.CreateConnectorClientAsync(It.IsAny<ITurnContext>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns<ITurnContext, string, IList<string>, bool, CancellationToken>((ctx, _, _, _, _) =>
                {
                    var serviceUrl = string.IsNullOrEmpty(ctx.Activity.ServiceUrl) ? TestServiceUrl : ctx.Activity.ServiceUrl;
                    return Task.FromResult<IConnectorClient>(
                        new RestConnectorClient(new Uri(serviceUrl), httpFactory.Object, () => Task.FromResult("test-token")));
                });

            return handler;
        }

        private static DefaultHttpContext CreateHttpContext(Activity activity = null)
        {
            var content = activity == null ? "" : ProtocolJsonSerializer.ToJson(activity);
            var context = new DefaultHttpContext();
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(content));
            context.Request.Method = HttpMethods.Post;
            context.Response.StatusCode = 0;
            context.Response.Body = new MemoryStream();
            return context;
        }

        private static Record UseRecord(Func<Record, IAgent> createAgent, Builder.IMiddleware[] middleware = null)
        {
            var factory = new Mock<IChannelServiceClientFactory>();
            var adapterLogger = new Mock<ILogger<CloudAdapter>>();
            var serviceLogger = new Mock<ILogger<HostedActivityService>>();

            var sp = new Mock<IServiceProvider>();
            var queue = new ActivityTaskQueue();
            var adapter = new CloudAdapter(factory.Object, queue, adapterLogger.Object, middlewares: middleware);
            var service = new HostedActivityService(sp.Object, new ConfigurationBuilder().Build(), queue, serviceLogger.Object);

            var record = new Record(null, adapter, factory, service, queue, adapterLogger, serviceLogger);

            if (createAgent != null)
            {
                record.Agent = createAgent(record);
            }

            sp.Setup(s => s.GetService(It.IsAny<Type>())).Returns(record.Agent);

            return record;
        }

        private record Record(
            IAgent Agent,
            CloudAdapter Adapter,
            Mock<IChannelServiceClientFactory> Factory,
            HostedActivityService Service,
            IActivityTaskQueue Queue,
            Mock<ILogger<CloudAdapter>> AdapterLogger,
            Mock<ILogger<HostedActivityService>> HostedServiceLogger)
        {
            public void VerifyMocks()
            {
                Mock.Verify(Factory, AdapterLogger, HostedServiceLogger);
            }

            public IStorage Storage { get; } = new MemoryStorage();

            public IAgent Agent { get; set; } = Agent;
        }

        private class DelayedActivityHandler : ActivityHandler
        {
            private readonly TaskCompletionSource<bool> _started;

            public DelayedActivityHandler(TaskCompletionSource<bool> started)
            {
                _started = started;
            }

            protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
            {
                // Signal that the handler has started
                _started.TrySetResult(true);

                // Delay long enough for the test to cancel
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);

                await turnContext.SendActivityAsync("Should not reach here", cancellationToken: cancellationToken);
            }

            protected override async Task<InvokeResponse> OnInvokeActivityAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
            {
                // Signal that the handler has started
                _started.TrySetResult(true);

                // Delay long enough for the test to cancel
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);

                return new InvokeResponse()
                {
                    Status = (int)HttpStatusCode.OK,
                    Body = new TokenResponse() { Token = "should-not-reach" }
                };
            }
        }

        private class RespondingActivityHandler : ActivityHandler
        {
            protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
            {
                if (turnContext.Activity.Text == "throw")
                {
                    throw new Exception("Test exception");
                }

                for (var i = 0; i < 3; i++)
                {
                    await Task.Delay(50, cancellationToken);

                    var message = $"Response {turnContext.Activity.Conversation.Id}:{turnContext.Activity.Id}:{i}";
                    await turnContext.SendActivityAsync(message, cancellationToken: cancellationToken);
                }
            }

            protected override async Task<InvokeResponse> OnInvokeActivityAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
            {
                await turnContext.SendActivityAsync("Test Response", cancellationToken: cancellationToken);
                return new InvokeResponse()
                {
                    Status = (int)HttpStatusCode.OK,
                    Body = new TokenResponse() { Token = $"token:{turnContext.Activity.Conversation.Id}" }
                };
            }
        }

        /// <summary>
        /// An <see cref="HttpMessageHandler"/> that intercepts connector HTTP calls, enabling tests to cover
        /// the full <see cref="RestConnectorClient"/> stack without real network I/O.
        /// </summary>
        private class TestHttpHandler : HttpMessageHandler
        {
            private readonly List<IActivity> _captured;
            private readonly string _newConversationId;
            private readonly TaskCompletionSource<bool> _firstActivitySent;

            /// <summary>Gets whether <c>SendToConversation</c> was called (path ends with <c>/activities</c>).</summary>
            public bool SendToConversationCalled { get; private set; }

            /// <summary>When set, called instead of the default routing logic — use to inject network errors or HTTP error codes.</summary>
            public Func<HttpRequestMessage, HttpResponseMessage> Override { get; set; }

            public TestHttpHandler(List<IActivity> captured = null, string newConversationId = null, TaskCompletionSource<bool> firstActivitySent = null)
            {
                _captured = captured;
                _newConversationId = newConversationId;
                _firstActivitySent = firstActivitySent;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (Override != null)
                    return Override(request);

                var path = request.RequestUri.AbsolutePath;
                var body = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : string.Empty;

                // ReplyToActivity: /v3/conversations/{convId}/activities/{actId}
                // SendToConversation: /v3/conversations/{convId}/activities
                if (path.Contains("/activities"))
                {
                    if (path.EndsWith("/activities"))
                        SendToConversationCalled = true;
                    if (_captured != null && !string.IsNullOrEmpty(body))
                        _captured.Add(ProtocolJsonSerializer.ToObject<Activity>(body));
                    _firstActivitySent?.TrySetResult(true);
                    return OkJson(ProtocolJsonSerializer.ToJson(new ResourceResponse("replyResourceId")));
                }

                // CreateConversation: /v3/conversations
                if (path == "/v3/conversations")
                    return OkJson(ProtocolJsonSerializer.ToJson(new ConversationResourceResponse { Id = _newConversationId ?? "new-convo-id" }));

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            private static HttpResponseMessage OkJson(string json) =>
                new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}