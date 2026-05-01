// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Xunit;
using Xunit.Sdk;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Compat;
using Microsoft.Agents.Builder.Dialogs.Prompts;
using Microsoft.Agents.Builder.UserAuth.TokenService;

namespace Microsoft.Agents.Builder.Dialogs.Tests
{
    public class OAuthPromptTests
    {
        private const string UserId = "user-id";
        private const string ConnectionName = "connection-name";
        private const string ChannelId = "channel-id";
        private const string MagicCode = "888999";
        private const string Token = "token123";
        private const string ExchangeToken = "exch123";

        public static TheoryData<TestAdapter, bool> SasTestData =>
        new TheoryData<TestAdapter, bool>
        {
            { new TestAdapter(), false },
            //old adapter { new SignInResourceAdapter(), true }
        };

        [Fact]
        public void OAuthPromptWithEmptySettingsShouldFail()
        {
            Assert.Throws<ArgumentNullException>(() => new OAuthPrompt("abc", null));
        }

        [Fact]
        public void OAuthPromptWithEmptyIdShouldFail()
        {
            Assert.Throws<ArgumentNullException>(() => new OAuthPrompt(string.Empty, new OAuthPromptSettings()));
        }

        [Fact]
        public async Task OAuthPromptWithDefaultTypeHandlingForStorage()
        {
            await OAuthPrompt(new MemoryStorage());
        }

        [Fact]
        public async Task OAuthPromptBeginDialogWithNoDialogContext()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                var prompt = new OAuthPrompt("abc", new OAuthPromptSettings());
                await prompt.BeginDialogAsync(null);
            });
        }

        [Fact]
        public async Task OAuthPromptBeginDialogWithWrongOptions()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                var prompt = new OAuthPrompt("abc", new OAuthPromptSettings()
                    {
                        AzureBotOAuthConnectionName = "abc",
                    });
                var convoState = new ConversationState(new MemoryStorage());

                var adapter = new TestAdapter()
                    .Use(new AutoSaveStateMiddleware(convoState));

                var tc = new TurnContext(adapter, new Activity() { Type = ActivityTypes.Message, Conversation = new ConversationAccount() { Id = "123" }, ChannelId = "test" });
                await convoState.LoadAsync(tc, false, default);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());

                // Create new DialogSet.
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(prompt);

                var dc = await dialogs.CreateContextAsync(tc);

                await prompt.BeginDialogAsync(dc, CancellationToken.None);
            });
        }

        [Fact]
        public async Task OAuthPromptBeginDialogWithNoPromptOptions()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                var prompt = new OAuthPrompt("abc", new OAuthPromptSettings() { AzureBotOAuthConnectionName = "test" });
                var convoState = new ConversationState(new MemoryStorage());

                var adapter = new TestAdapter()
                    .Use(new AutoSaveStateMiddleware(convoState));

                var tc = new TurnContext(adapter, new Activity() { Type = ActivityTypes.Message, Conversation = new ConversationAccount() { Id = "123" }, ChannelId = "test" });

                // Create new DialogSet.
                await convoState.LoadAsync(tc, false, default);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(prompt);

                var dc = await dialogs.CreateContextAsync(tc);

                await prompt.BeginDialogAsync(dc, new Options(), CancellationToken.None);
            });
        }

        [Fact]
        public async Task OAuthPromptWithMagicCode()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter()
                .Use(new AutoSaveStateMiddleware(convoState));

            AgentCallbackHandler botCallbackHandler = async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, default);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(new OAuthPrompt("OAuthPrompt", new OAuthPromptSettings() { Text = "Please sign in", AzureBotOAuthConnectionName = ConnectionName, Title = "Sign in" }));

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    var options = new PromptOptions
                    {
                        Prompt = new Activity
                        {
                            Type = ActivityTypes.Message,
                            Text = "Please select an option."
                        },
                        RetryPrompt = new Activity
                        {
                            Type = ActivityTypes.Message,
                            Text = "Retrying - Please select an option."
                        }
                    };
                    await dc.PromptAsync("OAuthPrompt", options, cancellationToken);
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    if (results.Result is TokenResponse)
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Logged in."), cancellationToken);
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Failed."), cancellationToken);
                    }
                }
            };

            await new TestFlow(adapter, botCallbackHandler)
            .Send("hello")
            .AssertReply(activity =>
            {
                Assert.Single(((Activity)activity).Attachments);
                Assert.Equal(OAuthCard.ContentType, ((Activity)activity).Attachments[0].ContentType);

                Assert.Equal(InputHints.AcceptingInput, ((Activity)activity).InputHint);

                // Add a magic code to the adapter
                adapter.AddUserToken(ConnectionName, activity.ChannelId.Channel, activity.Recipient.Id, Token, MagicCode);
            })
            .Send(MagicCode)
            .AssertReply("Logged in.")
            .StartTestAsync();
        }

        [Fact]
        public async Task OAuthPromptTimesOut_Message()
        {
            await PromptTimeoutEndsDialogTest(MessageFactory.Text("hi"));
        }

        [Fact]
        public async Task OAuthPromptTimesOut_TokenResponseEvent()
        {
            var activity = new Activity() { Type = ActivityTypes.Event, Name = SignInConstants.TokenResponseEventName };
            activity.Value = new TokenResponse(Channels.Msteams, ConnectionName, Token, DateTime.Parse("Tuesday, April 15, 2025 6:03:20 PM"));
            await PromptTimeoutEndsDialogTest(activity);
        }

        [Fact]
        public async Task OAuthPromptTimesOut_VerifyStateOperation()
        {
            var activity = new Activity() { Type = ActivityTypes.Invoke, Name = SignInConstants.VerifyStateOperationName };
            activity.Value = new { state = "888999" };

            await PromptTimeoutEndsDialogTest(activity);
        }

        [Fact]
        public async Task OAuthPromptTimesOut_TokenExchangeOperation()
        {
            var activity = new Activity() { Type = ActivityTypes.Invoke, Name = SignInConstants.TokenExchangeOperationName };

            activity.Value = new TokenExchangeInvokeRequest()
            {
                ConnectionName = ConnectionName,
                Token = ExchangeToken
            };

            await PromptTimeoutEndsDialogTest(activity);
        }

        [Fact]
        public async Task OAuthPromptContinueDialogWithNullDialogContext()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                var prompt = new OAuthPrompt("abc", new OAuthPromptSettings());
                var dialogs = new DialogSet(new DialogState());

                dialogs.Add(prompt);

                await prompt.ContinueDialogAsync(null, CancellationToken.None);
            });
        }

        [Fact]
        public async Task OAuthPromptDoesNotDetectCodeInBeginDialog()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter()
                .Use(new AutoSaveStateMiddleware(convoState));

            AgentCallbackHandler botCallbackHandler = async (turnContext, cancellationToken) =>
            {
                // Add a magic code to the adapter preemptively so that we can test if the message that triggers BeginDialogAsync uses magic code detection
                adapter.AddUserToken(ConnectionName, turnContext.Activity.ChannelId.Channel, turnContext.Activity.From.Id, Token, MagicCode);

                await convoState.LoadAsync(turnContext, false, default);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(new OAuthPrompt("OAuthPrompt", new OAuthPromptSettings() { Text = "Please sign in", AzureBotOAuthConnectionName = ConnectionName, Title = "Sign in" }));

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);

                if (results.Status == DialogTurnStatus.Empty)
                {
                    // If magicCode is detected when prompting, this will end the dialog and return the token in tokenResult
                    var tokenResult = await dc.PromptAsync("OAuthPrompt", new PromptOptions(), cancellationToken: cancellationToken);
                    if (tokenResult.Result is TokenResponse)
                    {
                        throw new XunitException("MagicCode detected in prompting.");
                    }
                }
            };

            // Call BeginDialogAsync by sending the magic code as the first message. It SHOULD respond with an OAuthPrompt since we haven't authenticated yet
            await new TestFlow(adapter, botCallbackHandler)
            .Send(MagicCode)
            .AssertReply(activity =>
            {
                Assert.Single(((Activity)activity).Attachments);
                Assert.Equal(OAuthCard.ContentType, ((Activity)activity).Attachments[0].ContentType);

                Assert.Equal(InputHints.AcceptingInput, ((Activity)activity).InputHint);
            })
            .StartTestAsync();
        }

        [Fact]
        public async Task OAuthPromptWithTokenExchangeInvoke()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter()
                .Use(new AutoSaveStateMiddleware(convoState));

            AgentCallbackHandler botCallbackHandler = async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, default);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(new OAuthPrompt("OAuthPrompt", new OAuthPromptSettings() { Text = "Please sign in", AzureBotOAuthConnectionName = ConnectionName, Title = "Sign in" }));
                
                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    await dc.PromptAsync("OAuthPrompt", new PromptOptions(), cancellationToken: cancellationToken);
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    if (results.Result is TokenResponse)
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Logged in."), cancellationToken);
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Failed."), cancellationToken);
                    }
                }
            };

            await new TestFlow(adapter, botCallbackHandler)
            .Send("hello")
            .AssertReply(activity =>
            {
                Assert.Single(((Activity)activity).Attachments);
                Assert.Equal(OAuthCard.ContentType, ((Activity)activity).Attachments[0].ContentType);
                Assert.Equal(InputHints.AcceptingInput, ((Activity)activity).InputHint);

                // Add an exchangable token to the adapter
                adapter.AddExchangeableToken(ConnectionName, activity.ChannelId.Channel, activity.Recipient.Id, ExchangeToken, Token);
            })
            .Send(new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = SignInConstants.TokenExchangeOperationName,
                Value = ProtocolJsonSerializer.ToJson(new TokenExchangeInvokeRequest()
                {
                    ConnectionName = ConnectionName,
                    Token = ExchangeToken
                })
            })
            .AssertReply(a =>
            {
                Assert.Equal("invokeResponse", a.Type);
                var response = ((Activity)a).Value as InvokeResponse;
                Assert.NotNull(response);
                Assert.Equal(200, response.Status);
                var body = response.Body as TokenExchangeInvokeResponse;
                Assert.Equal(ConnectionName, body.ConnectionName);
                Assert.Null(body.FailureDetail);
            })
            .AssertReply("Logged in.")
            .StartTestAsync();
        }

        [Fact]
        public async Task OAuthPromptWithTokenExchangeFail()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter()
                .Use(new AutoSaveStateMiddleware(convoState));

            AgentCallbackHandler botCallbackHandler = async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, default);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(new OAuthPrompt("OAuthPrompt", new OAuthPromptSettings() { Text = "Please sign in", AzureBotOAuthConnectionName = ConnectionName, Title = "Sign in" }));

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    await dc.PromptAsync("OAuthPrompt", new PromptOptions(), cancellationToken: cancellationToken);
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    if (results.Result is TokenResponse)
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Logged in."), cancellationToken);
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Failed."), cancellationToken);
                    }
                }
            };

            await new TestFlow(adapter, botCallbackHandler)
            .Send("hello")
            .AssertReply(activity =>
            {
                Assert.Single(((Activity)activity).Attachments);
                Assert.Equal(OAuthCard.ContentType, ((Activity)activity).Attachments[0].ContentType);
                Assert.Equal(InputHints.AcceptingInput, ((Activity)activity).InputHint);

                // No exchangable token is added to the adapter
            })
            .Send(new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = SignInConstants.TokenExchangeOperationName,
                Value = ProtocolJsonSerializer.ToJson(new TokenExchangeInvokeRequest()
                {
                    ConnectionName = ConnectionName,
                    Token = ExchangeToken
                })
            })
            .AssertReply(a =>
            {
                Assert.Equal("invokeResponse", a.Type);
                var response = ((Activity)a).Value as InvokeResponse;
                Assert.NotNull(response);
                Assert.Equal(412, response.Status);
                var body = response.Body as TokenExchangeInvokeResponse;
                Assert.Equal(ConnectionName, body.ConnectionName);
                Assert.NotNull(body.FailureDetail);
            })
            .StartTestAsync();
        }

        [Fact]
        public async Task OAuthPromptWithTokenExchangeNoBodyFails()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter()
                .Use(new AutoSaveStateMiddleware(convoState));

            AgentCallbackHandler botCallbackHandler = async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, default);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(new OAuthPrompt("OAuthPrompt", new OAuthPromptSettings() { Text = "Please sign in", AzureBotOAuthConnectionName = ConnectionName, Title = "Sign in" }));

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    await dc.PromptAsync("OAuthPrompt", new PromptOptions(), cancellationToken: cancellationToken);
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    if (results.Result is TokenResponse)
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Logged in."), cancellationToken);
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Failed."), cancellationToken);
                    }
                }
            };

            await new TestFlow(adapter, botCallbackHandler)
            .Send("hello")
            .AssertReply(activity =>
            {
                Assert.Single(((Activity)activity).Attachments);
                Assert.Equal(OAuthCard.ContentType, ((Activity)activity).Attachments[0].ContentType);
                Assert.Equal(InputHints.AcceptingInput, ((Activity)activity).InputHint);

                // No exchangable token is added to the adapter
            })
            .Send(new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = SignInConstants.TokenExchangeOperationName,

                // send no body
            })
            .AssertReply(a =>
            {
                Assert.Equal("invokeResponse", a.Type);
                var response = ((Activity)a).Value as InvokeResponse;
                Assert.NotNull(response);
                Assert.Equal(400, response.Status);
                var body = response.Body as TokenExchangeInvokeResponse;
                Assert.Equal(ConnectionName, body.ConnectionName);
                Assert.NotNull(body.FailureDetail);
            })
            .StartTestAsync();
        }

        [Fact]
        public async Task OAuthPromptWithTokenExchangeWrongConnectionNameFail()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter()
                .Use(new AutoSaveStateMiddleware(convoState));

            AgentCallbackHandler botCallbackHandler = async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, default);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(new OAuthPrompt("OAuthPrompt", new OAuthPromptSettings() { Text = "Please sign in", AzureBotOAuthConnectionName = ConnectionName, Title = "Sign in" }));

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    await dc.PromptAsync("OAuthPrompt", new PromptOptions(), cancellationToken: cancellationToken);
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    if (results.Result is TokenResponse)
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Logged in."), cancellationToken);
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Failed."), cancellationToken);
                    }
                }
            };

            await new TestFlow(adapter, botCallbackHandler)
            .Send("hello")
            .AssertReply(activity =>
            {
                Assert.Single(((Activity)activity).Attachments);
                Assert.Equal(OAuthCard.ContentType, ((Activity)activity).Attachments[0].ContentType);
                Assert.Equal(InputHints.AcceptingInput, ((Activity)activity).InputHint);

                // No exchangable token is added to the adapter
            })
            .Send(new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = SignInConstants.TokenExchangeOperationName,
                Value = ProtocolJsonSerializer.ToJson(new TokenExchangeInvokeRequest()
                {
                    ConnectionName = "beepboop",
                    Token = ExchangeToken
                })
            })
            .AssertReply(a =>
            {
                Assert.Equal("invokeResponse", a.Type);
                var response = ((Activity)a).Value as InvokeResponse;
                Assert.NotNull(response);
                Assert.Equal(400, response.Status);
                var body = response.Body as TokenExchangeInvokeResponse;
                Assert.Equal(ConnectionName, body.ConnectionName);
                Assert.NotNull(body.FailureDetail);
            })
            .StartTestAsync();
        }

        [Fact]
        public async Task OAuthPromptInNotSupportedChannelShouldAddSignInCard()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter()
                .Use(new AutoSaveStateMiddleware(convoState));

            AgentCallbackHandler botCallbackHandler = async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, default);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(new OAuthPrompt("OAuthPrompt", new OAuthPromptSettings() { AzureBotOAuthConnectionName = ConnectionName }));

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    await dc.PromptAsync("OAuthPrompt", new PromptOptions(), cancellationToken: cancellationToken);
                }
            };

            var initialActivity = new Activity()
            {
                ChannelId = Channels.Skype,
                Text = "hello"
            };

            await new TestFlow(adapter, botCallbackHandler)
                .Send(initialActivity)
                .AssertReply(activity =>
                {
                    Assert.Single(((Activity)activity).Attachments);
                    Assert.Equal(SigninCard.ContentType, ((Activity)activity).Attachments[0].ContentType);
                })
                .StartTestAsync();
        }

        [Theory]
        [InlineData(null, Channels.Test, false)] //Do not override; ChannelRequiresSingInLink() returns false; Result: no link
        [InlineData(null, Channels.Msteams, true)] //Do not override; ChannelRequiresSingInLink() returns true; Result: show link
        [InlineData(false, Channels.Test, false)] //Override: no link; ChannelRequiresSingInLink() returns false; Result: no link
        [InlineData(true, Channels.Test, true)] //Override: show link; ChannelRequiresSingInLink() returns false; Result: show link
        [InlineData(false, Channels.Msteams, false)] //Override: no link; ChannelRequiresSingInLink() returns true; Result: no link
        [InlineData(true, Channels.Msteams, true)] //Override: show link;  ChannelRequiresSingInLink() returns true; Result: show link
        public async Task OAuthPromptSignInLinkSettingsCases(bool? showSignInLinkValue, string channelId, bool shouldHaveSignInLink)
        {
            var oAuthPromptSettings = new OAuthPromptSettings() { AzureBotOAuthConnectionName = "test" };
            oAuthPromptSettings.ShowSignInLink = showSignInLinkValue;

            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter()
                .Use(new AutoSaveStateMiddleware(convoState));

            AgentCallbackHandler botCallbackHandler = async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, default);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(new OAuthPrompt("OAuthPrompt", oAuthPromptSettings));

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    await dc.PromptAsync("OAuthPrompt", new PromptOptions(), cancellationToken: cancellationToken);
                }
            };

            var initialActivity = new Activity()
            {
                ChannelId = channelId,
                Text = "hello"
            };
            await new TestFlow(adapter, botCallbackHandler)
                .Send(initialActivity)
                .AssertReply(activity =>
                {
                    Assert.Single(((Activity)activity).Attachments);
                    Assert.Equal(OAuthCard.ContentType, ((Activity)activity).Attachments[0].ContentType);
                    var oAuthCard = (OAuthCard)((Activity)activity).Attachments[0].Content;
                    var cardAction = oAuthCard.Buttons[0];
                    Assert.Equal(shouldHaveSignInLink, cardAction.Value != null);
                })
                .StartTestAsync();
        }
        
        // old adapter
        /*
        [Fact]
        public async Task TestAdapterTokenExchange()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter()
                .Use(new AutoSaveStateMiddleware(convoState));

            BotCallbackHandler botCallbackHandler = async (turnContext, cancellationToken) =>
            {
                adapter.AddExchangeableToken(ConnectionName, turnContext.Activity.ChannelId, UserId, ExchangeToken, Token);

                // Positive case: Token
                var result = await adapter.ExchangeTokenAsync(turnContext, ConnectionName, UserId, new TokenExchangeRequest() { Token = ExchangeToken });
                Assert.NotNull(result);
                Assert.Equal(Token, result.Token);
                Assert.Equal(ConnectionName, result.ConnectionName);

                // Positive case: URI
                result = await adapter.ExchangeTokenAsync(turnContext, ConnectionName, UserId, new TokenExchangeRequest() { Uri = ExchangeToken });
                Assert.NotNull(result);
                Assert.Equal(Token, result.Token);
                Assert.Equal(ConnectionName, result.ConnectionName);

                // Negative case: Token
                result = await adapter.ExchangeTokenAsync(turnContext, ConnectionName, UserId, new TokenExchangeRequest() { Token = "beeboop" });
                Assert.Null(result);

                // Negative case: URI
                result = await adapter.ExchangeTokenAsync(turnContext, ConnectionName, UserId, new TokenExchangeRequest() { Uri = "beeboop" });
                Assert.Null(result);
            };

            await new TestFlow(adapter, botCallbackHandler)
            .Send("hello")
            .StartTestAsync();
        }
        */

        [Fact]
        public async Task OAuthPromptRecognizeTokenAsync_WithNullTextMessageActivity_DoesNotThrow()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter()
                .Use(new AutoSaveStateMiddleware(convoState));

            const string retryPromptText = "Sorry, invalid input. Please sign in.";

            AgentCallbackHandler botCallbackHandler = async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, default);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(new OAuthPrompt("OAuthPrompt", new OAuthPromptSettings() { Text = "Please sign in", AzureBotOAuthConnectionName = ConnectionName, Title = "Sign in" }));

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                   await dc.PromptAsync("OAuthPrompt", new PromptOptions() { RetryPrompt = MessageFactory.Text(retryPromptText) }, cancellationToken: cancellationToken);
                }
            };

            var messageActivityWithNullText = Activity.CreateMessageActivity();

            await new TestFlow(adapter, botCallbackHandler)
            .Send("hello")
            .AssertReply(activity =>
            {
                Assert.Single(((Activity)activity).Attachments);
                Assert.Equal(OAuthCard.ContentType, ((Activity)activity).Attachments[0].ContentType);
                Assert.Equal(InputHints.AcceptingInput, ((Activity)activity).InputHint);
            })
            .Send(messageActivityWithNullText)
            .AssertReply(retryPromptText)
            .StartTestAsync();
        }

        [Theory]
        [MemberData(nameof(SasTestData))]
        public async Task OAuthPromptSasUrlPresentInOAuthCard(TestAdapter testAdapter, bool containsSasurl)
        {
            var oAuthPromptSettings = new OAuthPromptSettings()
                {
                    AzureBotOAuthConnectionName = "test",
                };

            var convoState = new ConversationState(new MemoryStorage());

            var adapter = testAdapter
                .Use(new AutoSaveStateMiddleware(convoState));

            AgentCallbackHandler botCallbackHandler = async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, default);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(new OAuthPrompt("OAuthPrompt", oAuthPromptSettings));

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    await dc.PromptAsync("OAuthPrompt", new PromptOptions(), cancellationToken: cancellationToken);
                }
            };

            await new TestFlow(adapter, botCallbackHandler)
                .Send("hello")
                .AssertReply(activity =>
                {
                    Assert.Single(((Activity)activity).Attachments);
                    Assert.Equal(OAuthCard.ContentType, ((Activity)activity).Attachments[0].ContentType);
                    var oAuthCard = (OAuthCard)((Activity)activity).Attachments[0].Content;
                    if (containsSasurl)
                    {
                        Assert.NotNull(oAuthCard.TokenPostResource);
                        Assert.NotNull(oAuthCard.TokenPostResource.SasUrl);
                    }
                    else
                    {
                        Assert.Null(oAuthCard.TokenPostResource);
                    }

                    Assert.NotNull(oAuthCard.TokenExchangeResource);
                })
                .StartTestAsync();
        }

        [Fact]
        public async Task OAuthPromptEndOnInvalidMessageSetting()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter()
                .Use(new AutoSaveStateMiddleware(convoState));

            AgentCallbackHandler botCallbackHandler = async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, default);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(new OAuthPrompt("OAuthPrompt", new OAuthPromptSettings() { Text = "Please sign in", AzureBotOAuthConnectionName = ConnectionName, Title = "Sign in", EndOnInvalidMessage = true }));

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    await dc.PromptAsync("OAuthPrompt", new PromptOptions(), cancellationToken: cancellationToken);
                }
                else if (results.Status == DialogTurnStatus.Waiting)
                {
                    throw new InvalidOperationException("Test OAuthPromptEndOnInvalidMessageSetting expected DialogTurnStatus.Complete");
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    if (results.Result is TokenResponse)
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Logged in."), cancellationToken);
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Ended."), cancellationToken);
                    }
                }
            };

            await new TestFlow(adapter, botCallbackHandler)
            .Send("hello")
            .AssertReply(activity =>
            {
                Assert.Single(((Activity)activity).Attachments);
                Assert.Equal(OAuthCard.ContentType, ((Activity)activity).Attachments[0].ContentType);
            })
            .Send("blah")
            .AssertReply("Ended.")
            .StartTestAsync();
        }

        [Fact]
        public async Task GetUserTokenShouldReturnToken()
        {
            var oauthPromptSettings = new OAuthPromptSettings
            {
                AzureBotOAuthConnectionName = ConnectionName,
                Text = "Please sign in",
                Title = "Sign in",
            };

            var prompt = new OAuthPrompt("OAuthPrompt", oauthPromptSettings);
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter()
                .Use(new AutoSaveStateMiddleware(convoState));

            adapter.AddUserToken(ConnectionName, ChannelId, UserId, Token);

            // Create new DialogSet.
            var dialogs = new DialogSet(new DialogState());
            dialogs.Add(prompt);

            var activity = new Activity { ChannelId = ChannelId, From = new ChannelAccount { Id = UserId } };
            var turnContext = adapter.CreateTurnContext(activity);

            var userToken = await prompt.GetUserTokenAsync(turnContext, CancellationToken.None);

            Assert.Equal(Token, userToken.Token);
        }

        private async Task OAuthPrompt(IStorage storage)
        {
            var convoState = new ConversationState(storage);

            var adapter = new TestAdapter()
                .Use(new AutoSaveStateMiddleware(convoState));

            AgentCallbackHandler botCallbackHandler = async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, default);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(new OAuthPrompt("OAuthPrompt", new OAuthPromptSettings() { Text = "Please sign in", AzureBotOAuthConnectionName = ConnectionName, Title = "Sign in" }));

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    await dc.PromptAsync("OAuthPrompt", new PromptOptions(), cancellationToken: cancellationToken);
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    if (results.Result is TokenResponse)
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Logged in."), cancellationToken);
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Failed."), cancellationToken);
                    }
                }
            };

            await new TestFlow(adapter, botCallbackHandler)
            .Send("hello")
            .AssertReply(activity =>
            {
                Assert.Single(((Activity)activity).Attachments);
                Assert.Equal(OAuthCard.ContentType, activity.Attachments[0].ContentType);

                Assert.Equal(InputHints.AcceptingInput, activity.InputHint);

                // Prepare an EventActivity with a TokenResponse and send it to the botCallbackHandler
                var eventActivity = CreateEventResponse(adapter, activity, ConnectionName, Token);
                var ctx = new TurnContext(adapter, (Activity)eventActivity);
                botCallbackHandler(ctx, CancellationToken.None);
            })
            .AssertReply("Logged in.")
            .StartTestAsync();
        }

        private async Task PromptTimeoutEndsDialogTest(IActivity oauthPromptActivity)
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter()
                .Use(new AutoSaveStateMiddleware(true, convoState));

            AgentCallbackHandler botCallbackHandler = async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, default);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);

                // Set timeout to zero, so the prompt will end immediately.
                dialogs.Add(new OAuthPrompt("OAuthPrompt", new OAuthPromptSettings() { Text = "Please sign in", AzureBotOAuthConnectionName = ConnectionName, Title = "Sign in", Timeout = 0 }));

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    await dc.PromptAsync("OAuthPrompt", new PromptOptions(), cancellationToken: cancellationToken);
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    // If the TokenResponse comes back, the timeout did not occur.
                    if (results.Result is TokenResponse)
                    {
                        await turnContext.SendActivityAsync("failed", cancellationToken: cancellationToken);
                    }
                    else
                    {
                        await turnContext.SendActivityAsync("ended", cancellationToken: cancellationToken);
                    }
                }
            };

            var flow = new TestFlow(adapter, botCallbackHandler)
            .Send("hello")
            .AssertReply(activity =>
            {
                Assert.Single(((Activity)activity).Attachments);
                Assert.Equal(OAuthCard.ContentType, ((Activity)activity).Attachments[0].ContentType);

                // Add a magic code to the adapter
                adapter.AddUserToken(ConnectionName, activity.ChannelId.Channel, activity.Recipient.Id, Token, MagicCode);

                // Add an exchangable token to the adapter
                adapter.AddExchangeableToken(ConnectionName, activity.ChannelId.Channel, activity.Recipient.Id, ExchangeToken, Token);
            })
            .Delay(500)
            .Send(oauthPromptActivity);

            if (oauthPromptActivity.Name == SignInConstants.TokenExchangeOperationName)
            {
                flow = flow.AssertReply(a =>
                {
                    Assert.Equal("invokeResponse", a.Type);
                    var response = ((Activity)a).Value as InvokeResponse;
                    Assert.NotNull(response);
                    Assert.Equal(400, response.Status);
                    var body = response.Body as TokenExchangeInvokeResponse;
                    Assert.Equal(ConnectionName, body.ConnectionName);
                    Assert.NotNull(body.FailureDetail);
                });
            }
            
            await flow.AssertReply("ended")
                .StartTestAsync();
        }

        private IActivity CreateEventResponse(TestAdapter adapter, IActivity activity, string connectionName, string token)
        {
            // add the token to the TestAdapter
            adapter.AddUserToken(connectionName, activity.ChannelId.Channel, activity.Recipient.Id, token);

            // send an event TokenResponse activity to the botCallback handler
            var eventActivity = activity.CreateReply();
            eventActivity.Type = ActivityTypes.Event;
            var from = eventActivity.From;
            eventActivity.From = eventActivity.Recipient;
            eventActivity.Recipient = from;
            eventActivity.Name = SignInConstants.TokenResponseEventName;
            eventActivity.Value = new TokenResponse()
            {
                ConnectionName = connectionName,
                Token = token,
            };

            return eventActivity;
        }

        // old adapter
        /*
        private class SignInResourceAdapter : TestAdapter
        {
            public override async Task<SignInResource> GetSignInResourceAsync(ITurnContext turnContext, AppCredentials oAuthAppCredentials, string connectionName, string userId, string finalRedirect = null, CancellationToken cancellationToken = default)
            {
                var result = await base.GetSignInResourceAsync(turnContext, oAuthAppCredentials, connectionName, userId, finalRedirect, cancellationToken);
                result.TokenPostResource = new TokenPostResource()
                {
                    SasUrl = $"https://www.fakesas.com/{connectionName}/{userId}"
                };
                return result;
            }
        }
        */
    }

    public class Location
    {
        public float? Lat { get; set; }

        public float? Long { get; set; }
    }


    public class Options
    {
        public string FirstName { get; set; }

        public string LastName { get; set; }

        public int? Age { get; set; }

        public bool? Bool { get; set; }

        public Location Location { get; set; }
    }

}
