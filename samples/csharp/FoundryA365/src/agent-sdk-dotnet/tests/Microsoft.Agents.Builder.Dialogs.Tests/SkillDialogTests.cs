// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.Testing;
using Moq;
using Xunit;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Client;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Compat;
using Microsoft.Agents.Authentication;
using System.Security.Claims;
using System.Net;

namespace Microsoft.Agents.Builder.Dialogs.Tests
{
    public class SkillDialogTests
    {
        static readonly Mock<IAccessTokenProvider> _accessTokenProvider;
        static readonly string _hostAppId = Guid.NewGuid().ToString();
        static readonly Mock<IConnections> _connections;

        static SkillDialogTests()
        {
            _accessTokenProvider = new Mock<IAccessTokenProvider>();
            _accessTokenProvider
                .Setup(p => p.GetAccessTokenAsync(It.IsAny<string>(), It.IsAny<IList<String>>(), It.IsAny<bool>()))
                .Returns(Task.FromResult("token"));

            _connections = new Mock<IConnections>();
            _connections
                .Setup(c => c.GetConnection(It.IsAny<string>()))
                .Returns(_accessTokenProvider.Object);

            IAccessTokenProvider provider = _accessTokenProvider.Object;
            _connections
                .Setup(c => c.TryGetConnection(It.IsAny<string>(), out provider))
                .Returns(true);
        }

        private readonly Mock<ITurnContext> _context = new();

        private readonly DialogState _dialogState = new([
            new DialogInstance {
                Id = "A",
                State = new Dictionary<string, object> {
                    { "deliverymode", DeliveryModes.ExpectReplies},
                    { "Microsoft.Agents.Builder.Dialogs.SkillDialog.SkillConversationId", "conversationId"}
                }
            }
        ]);
        private readonly Mock<DialogContext> _dialogContext;
        private readonly MockSkillDialog _dialog;

        private readonly ClaimsIdentity _claimsIdentity = new ClaimsIdentity(
            [
                new (AuthenticationConstants.AudienceClaim, _hostAppId),
                new (AuthenticationConstants.AppIdClaim, _hostAppId),
            ]);

        private readonly TestHttpMessageHandler _httpMessageHandler;
        private readonly Mock<IHttpClientFactory> _httpFactory;
        private readonly HttpAgentClientSettings _httpAgentChannelSettings;
        private readonly IAgentHost _agentHost;
        private readonly IStorage _storage = new MemoryStorage();

        public SkillDialogTests()
        {
            var state = new ConversationState(_storage);

            _httpMessageHandler = new TestHttpMessageHandler()
            {
                HttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK),
            };

            _httpFactory = new Mock<IHttpClientFactory>();
            _httpFactory
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient(_httpMessageHandler));

            _httpAgentChannelSettings = new HttpAgentClientSettings();
            _httpAgentChannelSettings.ConnectionSettings.ClientId = Guid.NewGuid().ToString();
            _httpAgentChannelSettings.ConnectionSettings.Endpoint = new Uri("http://testskill.contoso.com/api/messages");
            _httpAgentChannelSettings.ConnectionSettings.TokenProvider = "ServiceConnection";

            _agentHost = new ConfigurationAgentHost(
                new Mock<IServiceProvider>().Object,
                _storage,
                _connections.Object,
                _httpFactory.Object,
                new Dictionary<string, HttpAgentClientSettings> { { "test", _httpAgentChannelSettings } },
                "https://localhost",
                _hostAppId);

            _dialogContext = new(new DialogSet(), _context.Object, _dialogState);
            _dialog = new(new SkillDialogOptions()
            {
                Skill = "test",
                ConversationState = state,
                AgentHost = _agentHost
            });
        }

        [Fact]
        public void ConstructorValidationTests()
        {
            Assert.Throws<ArgumentNullException>(() => { new SkillDialog(null); });
        }

        [Fact]
        public async Task BeginDialogOptionsValidation()
        {
            var dialogOptions = new SkillDialogOptions();
            var sut = new SkillDialog(dialogOptions);
            var client = new DialogTestClient(Channels.Test, sut);
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await client.SendActivityAsync<Activity>("irrelevant"));

            client = new DialogTestClient(Channels.Test, sut, new Dictionary<string, string>());
            await Assert.ThrowsAsync<ArgumentException>(async () => await client.SendActivityAsync<Activity>("irrelevant"));

            client = new DialogTestClient(Channels.Test, sut, new BeginSkillDialogOptions());
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await client.SendActivityAsync<Activity>("irrelevant"));
        }

        [Theory(Skip = "Need full IChannetHost.SendToChannel Mock")]
        [InlineData(null)]
        [InlineData(DeliveryModes.ExpectReplies)]
        public async Task BeginDialogCallsSkill(string deliveryMode)
        {
            // Use Memory for conversation state
            var conversationState = new ConversationState(_storage);   //DialogTestClient loads this via AutoSaveStateMiddleware
            var dialogOptions = CreateSkillDialogOptions(_agentHost, conversationState);

            // Create the SkillDialogInstance and the activity to send.
            var sut = new SkillDialog(dialogOptions);
            
            var activityToSend = (Activity)Activity.CreateMessageActivity();
            activityToSend.DeliveryMode = deliveryMode;
            activityToSend.Text = Guid.NewGuid().ToString();

            var client = new DialogTestClient(Channels.Test, sut, new BeginSkillDialogOptions { Activity = activityToSend }, conversationState: conversationState, contextClaims: _claimsIdentity);

            _httpMessageHandler.SendAssert = (activitySent, count) =>
            {
                if (count == 1)
                {
                    Assert.Equal(activitySent.Text, activitySent.Text);
                }
                else if (count == 2)
                {
                    Assert.Equal("Second message", activitySent.Text);
                }
            };

            // Send first message to the dialog to start it
            _httpMessageHandler.HttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ProtocolJsonSerializer.ToJson(new ExpectedReplies([new Activity()])), System.Text.Encoding.UTF8, "application/json")
            };
            await client.SendActivityAsync<Activity>("irrelevant");

            // Assert results and data sent to the SkillClient for fist turn
            Assert.Equal(DialogTurnStatus.Waiting, client.DialogTurnResult.Status);
            Assert.NotNull(await _agentHost.GetConversation(_context.Object, "test"));

            // Send a second message to continue the dialog
            _httpMessageHandler.HttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ProtocolJsonSerializer.ToJson(new ExpectedReplies([new Activity()])), System.Text.Encoding.UTF8, "application/json")
            };
            await client.SendActivityAsync<Activity>("Second message");

            // Assert results for second turn
            Assert.Equal(DialogTurnStatus.Waiting, client.DialogTurnResult.Status);

            // Send EndOfConversation to the dialog
            var eoc = new Activity()
            {
                Type = ActivityTypes.EndOfConversation
            };
            _httpMessageHandler.HttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ProtocolJsonSerializer.ToJson(new ExpectedReplies([eoc])), System.Text.Encoding.UTF8, "application/json")
            };
            await client.SendActivityAsync<Activity>(eoc);

            // Assert we are done.
            Assert.Equal(DialogTurnStatus.Complete, client.DialogTurnResult.Status);
            Assert.Null(await _agentHost.GetConversation(_context.Object, "test"));
        }

        [Fact(Skip = "Need full IChannetHost.SendToChannel Mock")]
        public async Task ShouldHandleInvokeActivities()
        {
            IActivity activitySent = null;
            string toConversationId = null;

            // Callback to capture the parameters sent to the skill
            void CaptureAction(string conversationId, IActivity activity, IActivity relatesTo, CancellationToken cancellationToken)
            {
                // Capture values sent to the skill so we can assert the right parameters were used.
                toConversationId = conversationId;
                activitySent = activity;
            }

            // Create a mock skill client to intercept calls and capture what is sent.
            var mockSkillClient = CreateMockSkillClient(CaptureAction);

            // Use Memory for conversation state
            var conversationState = new ConversationState(new MemoryStorage());
            var dialogOptions = CreateSkillDialogOptions(_agentHost, conversationState, mockSkillClient);

            // Create the SkillDialogInstance and the activity to send.
            var activityToSend = Activity.CreateInvokeActivity();
            activityToSend.Name = Guid.NewGuid().ToString();
            var sut = new SkillDialog(dialogOptions);
            var client = new DialogTestClient(Channels.Test, sut, new BeginSkillDialogOptions { Activity = activityToSend }, conversationState: conversationState, contextClaims: _claimsIdentity);

            // Send something to the dialog to start it
            await client.SendActivityAsync<Activity>("irrelevant");

            // Assert results and data sent to the SkillClient for fist turn
            Assert.Equal(activityToSend.Name, activitySent.Name);
            Assert.Equal(DeliveryModes.ExpectReplies, activitySent.DeliveryMode);
            Assert.Equal(DialogTurnStatus.Waiting, client.DialogTurnResult.Status);

            // Send a second message to continue the dialog
            await client.SendActivityAsync<Activity>("Second message");

            // Assert results for second turn
            Assert.Equal("Second message", activitySent.Text);
            Assert.Equal(DialogTurnStatus.Waiting, client.DialogTurnResult.Status);

            // Send EndOfConversation to the dialog
            await client.SendActivityAsync<Activity>((Activity)Activity.CreateEndOfConversationActivity());

            // Assert we are done.
            Assert.Equal(DialogTurnStatus.Complete, client.DialogTurnResult.Status);
        }

        [Fact(Skip = "Need full IChannetHost.SendToChannel Mock")]
        public async Task CancelDialogSendsEoC()
        {
            IActivity activitySent = null;

            // Callback to capture the parameters sent to the skill
            void CaptureAction(string conversationId, IActivity activity, IActivity relatesTo, CancellationToken cancellationToken)
            {
                // Capture values sent to the skill so we can assert the right parameters were used.
                activitySent = activity;
            }

            // Create a mock skill client to intercept calls and capture what is sent.
            var mockSkillClient = CreateMockSkillClient(CaptureAction);

            /*
            _httpMessageHandler.HttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ProtocolJsonSerializer.ToJson(new ExpectedReplies([activity])), System.Text.Encoding.UTF8, "application/json")
            };
            */


            // Use Memory for conversation state
            var conversationState = new ConversationState(_storage);
            var dialogOptions = CreateSkillDialogOptions(_agentHost, conversationState, mockSkillClient);

            // Create the SkillDialogInstance and the activity to send.
            var sut = new SkillDialog(dialogOptions);
            var activityToSend = (Activity)Activity.CreateMessageActivity();
            activityToSend.Text = Guid.NewGuid().ToString();
            var client = new DialogTestClient(Channels.Test, sut, new BeginSkillDialogOptions { Activity = activityToSend }, conversationState: conversationState, contextClaims: _claimsIdentity);

            // Send something to the dialog to start it
            await client.SendActivityAsync<Activity>("irrelevant");

            // Cancel the dialog so it sends an EoC to the skill
            await client.DialogContext.CancelAllDialogsAsync(CancellationToken.None);

            Assert.Equal(ActivityTypes.EndOfConversation, activitySent.Type);
        }

        [Fact]
        public async Task ShouldThrowHttpExceptionOnPostFailure()
        {
            _httpMessageHandler.HttpResponseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError);

            // Use Memory for conversation state
            var conversationState = new ConversationState(_storage);
            var dialogOptions = CreateSkillDialogOptions(_agentHost, conversationState);

            // Create the SkillDialogInstance and the activity to send.
            var sut = new SkillDialog(dialogOptions);
            var activityToSend = (Activity)Activity.CreateMessageActivity();
            activityToSend.Text = Guid.NewGuid().ToString();
            var client = new DialogTestClient(Channels.Test, sut, new BeginSkillDialogOptions { Activity = activityToSend }, conversationState: conversationState, contextClaims: _claimsIdentity);

            // Send something to the dialog 
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await client.SendActivityAsync<Activity>("irrelevant"));
        }

        [Fact(Skip = "Need full IChannetHost.SendToChannel Mock")]
        public async Task ShouldInterceptOAuthCardsForSso()
        {
            var connectionName = "connectionName";
            var firstResponse = new ExpectedReplies(new List<IActivity> { CreateOAuthCardAttachmentActivity("https://test") });
            var mockSkillClient = new Mock<IAgentClient>();
            mockSkillClient
                .SetupSequence(x => x.SendActivityAsync<ExpectedReplies>(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<IActivity>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new InvokeResponse<ExpectedReplies>
                {
                    Status = 200,
                    Body = firstResponse
                }))
                .Returns(Task.FromResult(new InvokeResponse<ExpectedReplies> { Status = 200 }));

            var mockUserTokenClient = new Mock<IUserTokenClient>();
            mockUserTokenClient
                .SetupSequence(x => x.ExchangeTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TokenExchangeRequest>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new TokenResponse()
                {
                    ChannelId = Channels.Test,
                    ConnectionName = connectionName,
                    Token = "https://test1"
                }));

            var conversationState = new ConversationState(_storage);
            var testAdapter = new TestAdapter(Channels.Test)
                .Use(new AutoSaveStateMiddleware(conversationState));

            var dialogOptions = CreateSkillDialogOptions(_agentHost, conversationState, mockSkillClient, connectionName);
            var sut = new SkillDialog(dialogOptions);
            var activityToSend = CreateSendActivity();
            var client = new DialogTestClient(testAdapter, sut, new BeginSkillDialogOptions { Activity = activityToSend }, conversationState: conversationState, contextClaims: _claimsIdentity);
            testAdapter.AddExchangeableToken(connectionName, Channels.Test, "user1", "https://test", "https://test1");
            var finalActivity = await client.SendActivityAsync<Activity>("irrelevant");
            Assert.Null(finalActivity);
        }

        [Fact(Skip = "Need full IChannetHost.SendToChannel Mock")]
        public async Task ShouldNotInterceptOAuthCardsForEmptyConnectionName()
        {
            var connectionName = "connectionName";
            var firstResponse = new ExpectedReplies(new List<IActivity> { CreateOAuthCardAttachmentActivity("https://test") });
            var mockSkillClient = new Mock<IAgentClient>();
            mockSkillClient
                .Setup(x => x.SendActivityAsync<ExpectedReplies>(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<IActivity>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new InvokeResponse<ExpectedReplies>
                {
                    Status = 200,
                    Body = firstResponse
                }));

            var conversationState = new ConversationState(_storage);
            var dialogOptions = CreateSkillDialogOptions(_agentHost, conversationState, mockSkillClient);

            var sut = new SkillDialog(dialogOptions);
            var activityToSend = CreateSendActivity();
            var testAdapter = new TestAdapter(Channels.Test)
                .Use(new AutoSaveStateMiddleware(conversationState));
            var client = new DialogTestClient(testAdapter, sut, new BeginSkillDialogOptions { Activity = activityToSend }, conversationState: conversationState, contextClaims: _claimsIdentity);
            testAdapter.AddExchangeableToken(connectionName, Channels.Test, "user1", "https://test", "https://test1");
            var finalActivity = await client.SendActivityAsync<Activity>("irrelevant");
            Assert.NotNull(finalActivity);
            Assert.Single(finalActivity.Attachments);
        }

        [Fact(Skip = "Need full IChannetHost.SendToChannel Mock")]
        public async Task ShouldNotInterceptOAuthCardsForEmptyToken()
        {
            var firstResponse = new ExpectedReplies(new List<IActivity> { CreateOAuthCardAttachmentActivity("https://test") });
            var mockSkillClient = new Mock<IAgentClient>();
            mockSkillClient
                .Setup(x => x.SendActivityAsync<ExpectedReplies>(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<IActivity>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new InvokeResponse<ExpectedReplies>
                {
                    Status = 200,
                    Body = firstResponse
                }));

            var conversationState = new ConversationState(_storage);
            var dialogOptions = CreateSkillDialogOptions(_agentHost, conversationState, mockSkillClient);

            var sut = new SkillDialog(dialogOptions);
            var activityToSend = CreateSendActivity();
            var testAdapter = new TestAdapter(Channels.Test)
                .Use(new AutoSaveStateMiddleware(conversationState));
            var client = new DialogTestClient(testAdapter, sut, new BeginSkillDialogOptions { Activity = activityToSend }, conversationState: conversationState, contextClaims: _claimsIdentity);

            // Don't add exchangeable token to test adapter
            var finalActivity = await client.SendActivityAsync<Activity>("irrelevant");
            Assert.NotNull(finalActivity);
            Assert.Single(finalActivity.Attachments);
        }

        [Fact(Skip = "Need full IChannetHost.SendToChannel Mock")]
        public async Task ShouldNotInterceptOAuthCardsForTokenException()
        {
            var connectionName = "connectionName";
            var firstResponse = new ExpectedReplies(new List<IActivity> { CreateOAuthCardAttachmentActivity("https://test") });
            var mockSkillClient = new Mock<IAgentClient>();
            mockSkillClient
                .Setup(x => x.SendActivityAsync<ExpectedReplies>(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<IActivity>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new InvokeResponse<ExpectedReplies>
                {
                    Status = 200,
                    Body = firstResponse
                }));

            var conversationState = new ConversationState(_storage);
            var dialogOptions = CreateSkillDialogOptions(_agentHost, conversationState, mockSkillClient, connectionName);

            var sut = new SkillDialog(dialogOptions);
            var activityToSend = CreateSendActivity();
            var testAdapter = new TestAdapter(Channels.Test)
                .Use(new AutoSaveStateMiddleware(conversationState));
            var initialDialogOptions = new BeginSkillDialogOptions { Activity = activityToSend };
            var client = new DialogTestClient(testAdapter, sut, initialDialogOptions, conversationState: conversationState, contextClaims: _claimsIdentity);
            testAdapter.ThrowOnExchangeRequest(connectionName, Channels.Test, "user1", "https://test");
            var finalActivity = await client.SendActivityAsync<Activity>("irrelevant");
            Assert.NotNull(finalActivity);
            Assert.Single(finalActivity.Attachments);
        }

        [Fact(Skip = "Need full IChannetHost.SendToChannel Mock")]
        public async Task ShouldNotInterceptOAuthCardsForBadRequest()
        {
            var connectionName = "connectionName";
            var firstResponse = new ExpectedReplies(new List<IActivity> { CreateOAuthCardAttachmentActivity("https://test") });
            var mockSkillClient = new Mock<IAgentClient>();
            mockSkillClient
                .SetupSequence(x => x.SendActivityAsync<ExpectedReplies>(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<IActivity>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new InvokeResponse<ExpectedReplies>
                {
                    Status = 200,
                    Body = firstResponse
                }))
                .Returns(Task.FromResult(new InvokeResponse<ExpectedReplies> { Status = 409 }));

            var conversationState = new ConversationState(_storage);
            var dialogOptions = CreateSkillDialogOptions(_agentHost, conversationState, mockSkillClient, connectionName);

            var sut = new SkillDialog(dialogOptions);
            var activityToSend = CreateSendActivity();
            var testAdapter = new TestAdapter(Channels.Test)
                .Use(new AutoSaveStateMiddleware(conversationState));
            var client = new DialogTestClient(testAdapter, sut, new BeginSkillDialogOptions { Activity = activityToSend }, conversationState: conversationState, contextClaims: _claimsIdentity);
            testAdapter.AddExchangeableToken(connectionName, Channels.Test, "user1", "https://test", "https://test1");
            var finalActivity = await client.SendActivityAsync<Activity>("irrelevant");
            Assert.NotNull(finalActivity);
            Assert.Single(finalActivity.Attachments);
        }

        [Fact(Skip = "Need full IChannetHost.SendToChannel Mock")]
        public async Task EndOfConversationFromExpectRepliesCallsDeleteConversationReferenceAsync()
        {
            IActivity activitySent = null;

            // Callback to capture the parameters sent to the skill
            void CaptureAction(string conversationId, IActivity activity, IActivity relatesTo, CancellationToken cancellationToken)
            {
                // Capture values sent to the skill so we can assert the right parameters were used.
                activitySent = activity;
            }

            // Create a mock skill client to intercept calls and capture what is sent.
            var mockSkillClientx = CreateMockSkillClient(CaptureAction);

            var eoc = Activity.CreateEndOfConversationActivity() as Activity;
            var expectedReplies = new List<IActivity>();
            expectedReplies.Add(eoc);

            // Create a mock skill client to intercept calls and capture what is sent.
            var mockSkillClient = CreateMockSkillClient(CaptureAction, expectedReplies: expectedReplies);

            // Use Memory for conversation state
            var conversationState = new ConversationState(_storage);
            var dialogOptions = CreateSkillDialogOptions(_agentHost, conversationState, mockSkillClient);

            // Create the SkillDialogInstance and the activity to send.
            var sut = new SkillDialog(dialogOptions);
            var activityToSend = (Activity)Activity.CreateMessageActivity();
            activityToSend.DeliveryMode = DeliveryModes.ExpectReplies;
            activityToSend.Text = Guid.NewGuid().ToString();
            var client = new DialogTestClient(Channels.Test, sut, new BeginSkillDialogOptions { Activity = activityToSend }, conversationState: conversationState, contextClaims: _claimsIdentity);

            // Send something to the dialog to start it
            await client.SendActivityAsync<Activity>("hello");

            //!!! Assert.Empty((dialogOptions.ConversationIdFactory as SimpleConversationIdFactory).ConversationRefs);
            //!!! Assert.Equal(1, (dialogOptions.ConversationIdFactory as SimpleConversationIdFactory).CreateCount);
        }

        [Fact]
        public async Task ContinueDialogAsync_ShouldReturnEndOfTurnOnValidateActivity()
        {
            _context.SetupGet(e => e.Activity)
                .Returns(new Activity { Text = "shouldNotValidate" })
                .Verifiable(Times.Exactly(1));

            var result = await _dialog.ContinueDialogAsync(_dialogContext.Object);

            Assert.Equal(Dialog.EndOfTurn, result);
            Mock.Verify(_context);
        }

        [Fact]
        public async Task ContinueDialogAsync_ShouldSendInvokeActivity()
        {
            var resultInvoke = new InvokeResponse { Status = 200, Body = "testing" };
            var activity = new Activity { Type = ActivityTypes.InvokeResponse, Value = resultInvoke, ChannelId = Channels.Test, Conversation = new ConversationAccount() { Id = "1" } };

            _context.SetupGet(e => e.Activity)
                .Returns(activity)
                .Verifiable(Times.Exactly(7));
            _context.SetupGet(e => e.Services)
                .Returns([])
                .Verifiable(Times.Exactly(1));
            _context.SetupGet(e => e.StackState)
                .Returns([]);

            MockSendToSkillAsync(activity);

            await _dialog.State.LoadAsync(_context.Object);
            var result = await _dialog.ContinueDialogAsync(_dialogContext.Object);

            Assert.Equal(DialogTurnStatus.Waiting, result.Status);
            Assert.Equal(resultInvoke.Status, (activity.Value as InvokeResponse).Status);
            Assert.Equal(resultInvoke.Body, (activity.Value as InvokeResponse).Body.ToString());
            Mock.Verify(_context);
        }

        [Fact]
        public async Task ContinueDialogAsync_ShouldReturnEndOfDialog()
        {
            var activity = new Activity { Type = ActivityTypes.EndOfConversation, Value = "EOC testing" };

            _context.SetupGet(e => e.Activity)
                .Returns(new Activity() { ChannelId = Channels.Test, Conversation = new ConversationAccount() { Id = "1"} })
                .Verifiable(Times.Exactly(8));
            _context.SetupGet(e => e.Services)
                .Returns([]);
            _context.SetupGet(e => e.StackState)
                .Returns([]);

            MockSendToSkillAsync(activity);

            await _dialog.State.LoadAsync(_context.Object);
            var result = await _dialog.ContinueDialogAsync(_dialogContext.Object);

            Assert.Equal(DialogTurnStatus.Complete, result.Status);
            Assert.Equal(activity.Value, result.Result);
            Mock.Verify(_context);
        }

        [Fact]
        public async Task RepromptDialogAsync_ShouldSendActivity()
        {
            var activity = new Activity() { ChannelId = Channels.Test, Conversation = new ConversationAccount() { Id = "1" } };

            _context.SetupGet(e => e.Activity)
                .Returns(activity)
                .Verifiable(Times.Exactly(5));
            _context.SetupGet(e => e.Services)
                .Returns([]);
            _context.SetupGet(e => e.StackState)
                .Returns([]);

            MockSendToSkillAsync(activity);

            await _dialog.State.LoadAsync(_context.Object);
            await _dialog.RepromptDialogAsync(_context.Object, _dialogState.DialogStack[0]);

            Mock.Verify(_context);
        }

        [Fact]
        public async Task ResumeDialogAsync_ShouldSendActivity()
        {
            var activity = new Activity() { ChannelId = Channels.Test, Conversation = new ConversationAccount() { Id = "1" } };

            _context.SetupGet(e => e.Activity)
                .Returns(activity)
                .Verifiable(Times.Exactly(5));
            _context.SetupGet(e => e.Services)
                .Returns([]);
            _context.SetupGet(e => e.StackState)
                .Returns([]);

            MockSendToSkillAsync(activity);

            await _dialog.State.LoadAsync(_context.Object);
            var result = await _dialog.ResumeDialogAsync(_dialogContext.Object, DialogReason.BeginCalled);

            Assert.Equal(DialogTurnStatus.Waiting, result.Status);
            Mock.Verify(_context);
        }

        private static IActivity CreateOAuthCardAttachmentActivity(string uri)
        {
            var oauthCard = new OAuthCard { TokenExchangeResource = new TokenExchangeResource { Uri = uri } };
            var attachment = new Attachment
            {
                ContentType = OAuthCard.ContentType,
                Content = oauthCard
            };

            var attachmentActivity = MessageFactory.Attachment(attachment);
            attachmentActivity.Conversation = new ConversationAccount { Id = Guid.NewGuid().ToString() };
            attachmentActivity.From = new ChannelAccount("blah", "name");

            return attachmentActivity;
        }

        /// <summary>
        /// Helper to create a <see cref="SkillDialogOptions"/> for the skillDialog.
        /// </summary>
        /// <param name="conversationState"> The conversation state object.</param>
        /// <param name="mockSkillClient"> The skill client mock.</param>
        /// <returns> A Skill Dialog Options object.</returns>
        private static SkillDialogOptions CreateSkillDialogOptions(IAgentHost channelHost, ConversationState conversationState, Mock<IAgentClient> mockSkillClient = null, string connectionName = null)
        {
            var dialogOptions = new SkillDialogOptions
            {
                AgentHost = channelHost,
                Skill = "test",
                ConversationState = conversationState,
                ConnectionName = connectionName
            };
            return dialogOptions;
        }

        private static Mock<IAgentClient> CreateMockSkillClient(Action<string, IActivity, IActivity,CancellationToken> captureAction, int returnStatus = 200, IList<IActivity> expectedReplies = null)
        {
            var mockSkillClient = new Mock<IAgentClient>();
            var activityList = new ExpectedReplies(expectedReplies ?? new List<IActivity> { MessageFactory.Text("dummy activity") });

            if (captureAction != null)
            {
                mockSkillClient
                    .Setup(x => x.SendActivityAsync<ExpectedReplies>(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<IActivity>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(new InvokeResponse<ExpectedReplies>
                    {
                        Status = returnStatus,
                        Body = activityList
                    }))
                    .Callback(captureAction);
            }
            else
            {
                mockSkillClient
                    .Setup(x => x.SendActivityAsync<ExpectedReplies>(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<IActivity>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(new InvokeResponse<ExpectedReplies>
                    {
                        Status = returnStatus,
                        Body = activityList
                    }));
            }

            return mockSkillClient;
        }

        private Activity CreateSendActivity()
        {
            var activityToSend = (Activity)Activity.CreateMessageActivity();
            activityToSend.DeliveryMode = DeliveryModes.ExpectReplies;
            activityToSend.Text = Guid.NewGuid().ToString();
            return activityToSend;
        }

        private class MockSkillDialog(SkillDialogOptions dialogOptions, string dialogId = null) : SkillDialog(dialogOptions, dialogId)
        {
            protected override bool OnValidateActivity(IActivity activity)
            {
                return !(activity.Text == "shouldNotValidate");
            }

            public AgentState State => DialogOptions.ConversationState;
        }
        private void MockSendToSkillAsync(Activity activity)
        {
            _httpMessageHandler.HttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ProtocolJsonSerializer.ToJson(new ExpectedReplies([activity])), System.Text.Encoding.UTF8, "application/json")
            };

            _context.Setup(e => e.SendActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse())
                .Verifiable(Times.AtMostOnce);
        }
    }

    class TestHttpMessageHandler : HttpMessageHandler
    {
        private int _sendRequest = 0;

        public HttpResponseMessage HttpResponseMessage { get; set; }

        public Action<IActivity, int> SendAssert { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _sendRequest++;

            if (SendAssert != null)
            {
#if !NETFRAMEWORK
                var activity = ProtocolJsonSerializer.ToObject<Activity>(request.Content.ReadAsStream());
#else
                var activity = ProtocolJsonSerializer.ToObject<Activity>(request.Content.ReadAsStringAsync().Result);
#endif
                SendAssert(activity, _sendRequest);
            }

            return Task.FromResult(HttpResponseMessage);
        }
    }
}
