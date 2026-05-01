// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.Dialogs.Choices;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Storage.Transcript;
using Microsoft.Agents.Core.Models;
using Microsoft.Recognizers.Text;
using Xunit;
using Microsoft.Agents.Core;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Compat;
using Microsoft.Agents.Builder.Dialogs.Prompts;

namespace Microsoft.Agents.Builder.Dialogs.Tests
{
    public class ConfirmPromptTests
    {
        [Fact]
        public void ConfirmPromptWithEmptyIdShouldFail()
        {
            Assert.Throws<ArgumentNullException>(() => { new ConfirmPrompt(string.Empty); });
        }

        [Fact]
        public void ConfirmPromptWithNullIdShouldFail()
        {
            Assert.Throws<ArgumentNullException>(() => { new ConfirmPrompt(null); });
        }

        [Fact]
        public async Task ConfirmPrompt()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter(TestAdapter.CreateConversation(nameof(ConfirmPrompt)))
                .Use(new AutoSaveStateMiddleware(convoState))
                .Use(new TranscriptLoggerMiddleware(new TraceTranscriptLogger(traceActivity: false)));

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, cancellationToken);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(new ConfirmPrompt("ConfirmPrompt", defaultLocale: Culture.English));

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    await dc.PromptAsync("ConfirmPrompt", new PromptOptions { Prompt = new Activity { Type = ActivityTypes.Message, Text = "Please confirm." } }, cancellationToken);
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    if ((bool)results.Result)
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Confirmed."), cancellationToken);
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Not confirmed."), cancellationToken);
                    }
                }
            })
            .Send("hello")
            .AssertReply("Please confirm. (1) Yes or (2) No")
            .Send("yes")
            .AssertReply("Confirmed.")
            .StartTestAsync();
        }

        [Fact]
        public async Task ConfirmPromptRetry()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter(TestAdapter.CreateConversation(nameof(ConfirmPromptRetry)))
                .Use(new AutoSaveStateMiddleware(convoState))
                .Use(new TranscriptLoggerMiddleware(new TraceTranscriptLogger(traceActivity: false)));

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, cancellationToken);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(new ConfirmPrompt("ConfirmPrompt", defaultLocale: Culture.English));

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    var options = new PromptOptions
                    {
                        Prompt = new Activity
                        {
                            Type = ActivityTypes.Message,
                            Text = "Please confirm.",
                        },
                        RetryPrompt = new Activity
                        {
                            Type = ActivityTypes.Message,
                            Text = "Please confirm, say 'yes' or 'no' or something like that.",
                        },
                    };
                    await dc.PromptAsync("ConfirmPrompt", options, cancellationToken);
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    if ((bool)results.Result)
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Confirmed."), cancellationToken);
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Not confirmed."), cancellationToken);
                    }
                }
            })
            .Send("hello")
            .AssertReply("Please confirm. (1) Yes or (2) No")
            .Send("lala")
            .AssertReply("Please confirm, say 'yes' or 'no' or something like that. (1) Yes or (2) No")
            .Send("no")
            .AssertReply("Not confirmed.")
            .StartTestAsync();
        }

        [Fact]
        public async Task ConfirmPromptNoOptions()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter(TestAdapter.CreateConversation(nameof(ConfirmPromptNoOptions)))
                .Use(new AutoSaveStateMiddleware(convoState))
                .Use(new TranscriptLoggerMiddleware(new TraceTranscriptLogger(traceActivity: false)));

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, cancellationToken);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(new ConfirmPrompt("ConfirmPrompt", defaultLocale: Culture.English));

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    var options = new PromptOptions();
                    await dc.PromptAsync("ConfirmPrompt", options, cancellationToken);
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    if ((bool)results.Result)
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Confirmed."), cancellationToken);
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Not confirmed."), cancellationToken);
                    }
                }
            })
            .Send("hello")
            .AssertReply(" (1) Yes or (2) No")
            .Send("lala")
            .AssertReply(" (1) Yes or (2) No")
            .Send("no")
            .AssertReply("Not confirmed.")
            .StartTestAsync();
        }

        [Fact]
        public async Task ConfirmPromptChoiceOptionsNumbers()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter(TestAdapter.CreateConversation(nameof(ConfirmPromptChoiceOptionsNumbers)))
                .Use(new AutoSaveStateMiddleware(convoState))
                .Use(new TranscriptLoggerMiddleware(new TraceTranscriptLogger(traceActivity: false)));

            var prompt = new ConfirmPrompt("ConfirmPrompt", defaultLocale: Culture.English);

            // Set options
            prompt.ChoiceOptions = new Choices.ChoiceFactoryOptions { IncludeNumbers = true };
            prompt.Style = Choices.ListStyle.Inline;

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, cancellationToken);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(prompt);

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    var options = new PromptOptions
                    {
                        Prompt = new Activity
                        {
                            Type = ActivityTypes.Message,
                            Text = "Please confirm.",
                        },
                        RetryPrompt = new Activity
                        {
                            Type = ActivityTypes.Message,
                            Text = "Please confirm, say 'yes' or 'no' or something like that.",
                        },
                    };
                    await dc.PromptAsync("ConfirmPrompt", options, cancellationToken);
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    if ((bool)results.Result)
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Confirmed."), cancellationToken);
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Not confirmed."), cancellationToken);
                    }
                }
            })
            .Send("hello")
            .AssertReply("Please confirm. (1) Yes or (2) No")
            .Send("lala")
            .AssertReply("Please confirm, say 'yes' or 'no' or something like that. (1) Yes or (2) No")
            .Send("2")
            .AssertReply("Not confirmed.")
            .StartTestAsync();
        }

        [Fact]
        public async Task ConfirmPromptChoiceOptionsMultipleAttempts()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter(TestAdapter.CreateConversation(nameof(ConfirmPromptChoiceOptionsMultipleAttempts)))
                .Use(new AutoSaveStateMiddleware(convoState))
                .Use(new TranscriptLoggerMiddleware(new TraceTranscriptLogger(traceActivity: false)));

            var prompt = new ConfirmPrompt("ConfirmPrompt", defaultLocale: Culture.English);

            // Set options
            prompt.ChoiceOptions = new Choices.ChoiceFactoryOptions { IncludeNumbers = true };
            prompt.Style = Choices.ListStyle.Inline;

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, cancellationToken);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(prompt);

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    var options = new PromptOptions
                    {
                        Prompt = new Activity
                        {
                            Type = ActivityTypes.Message,
                            Text = "Please confirm.",
                        },
                        RetryPrompt = new Activity
                        {
                            Type = ActivityTypes.Message,
                            Text = "Please confirm, say 'yes' or 'no' or something like that.",
                        },
                    };
                    await dc.PromptAsync("ConfirmPrompt", options, cancellationToken);
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    if ((bool)results.Result)
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Confirmed."), cancellationToken);
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Not confirmed."), cancellationToken);
                    }
                }
            })
            .Send("hello")
            .AssertReply("Please confirm. (1) Yes or (2) No")
            .Send("lala")
            .AssertReply("Please confirm, say 'yes' or 'no' or something like that. (1) Yes or (2) No")
            .Send("what")
            .AssertReply("Please confirm, say 'yes' or 'no' or something like that. (1) Yes or (2) No")
            .Send("2")
            .AssertReply("Not confirmed.")
            .StartTestAsync();
        }

        [Fact]
        public async Task ConfirmPromptChoiceOptionsNoNumbers()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter(TestAdapter.CreateConversation(nameof(ConfirmPromptChoiceOptionsNoNumbers)))
                .Use(new AutoSaveStateMiddleware(convoState))
                .Use(new TranscriptLoggerMiddleware(new TraceTranscriptLogger(traceActivity: false)));

            var prompt = new ConfirmPrompt("ConfirmPrompt", defaultLocale: Culture.English);

            // Set options
            prompt.ChoiceOptions = new Choices.ChoiceFactoryOptions { IncludeNumbers = false, InlineSeparator = "~" };

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, cancellationToken);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(prompt);

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    var options = new PromptOptions
                    {
                        Prompt = new Activity
                        {
                            Type = ActivityTypes.Message,
                            Text = "Please confirm.",
                        },
                        RetryPrompt = new Activity
                        {
                            Type = ActivityTypes.Message,
                            Text = "Please confirm, say 'yes' or 'no' or something like that.",
                        },
                    };
                    await dc.PromptAsync("ConfirmPrompt", options, cancellationToken);
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    if ((bool)results.Result)
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Confirmed."), cancellationToken);
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Not confirmed."), cancellationToken);
                    }
                }
            })
            .Send("hello")
            .AssertReply("Please confirm. Yes or No")
            .Send("2")
            .AssertReply("Please confirm, say 'yes' or 'no' or something like that. Yes or No")
            .Send("no")
            .AssertReply("Not confirmed.")
            .StartTestAsync();
        }

        [Fact]
        public async Task ConfirmPromptDifferentRecognizeLanguage()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter(TestAdapter.CreateConversation(nameof(ConfirmPromptDifferentRecognizeLanguage)))
                .Use(new AutoSaveStateMiddleware(convoState))
                .Use(new TranscriptLoggerMiddleware(new TraceTranscriptLogger(traceActivity: false)));

            var prompt = new ConfirmPrompt("ConfirmPrompt", defaultLocale: Culture.English);

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, cancellationToken);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(prompt);

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    var options = new PromptOptions
                    {
                        Prompt = new Activity
                        {
                            Type = ActivityTypes.Message,
                            Text = "Please confirm.",
                        },
                        RetryPrompt = new Activity
                        {
                            Type = ActivityTypes.Message,
                            Text = "Please confirm, say 'yes' or 'no' or something like that.",
                        },
                        RecognizeLanguage = "es-es"
                    };
                    await dc.PromptAsync("ConfirmPrompt", options, cancellationToken);
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    if ((bool)results.Result)
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Confirmed."), cancellationToken);
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Not confirmed."), cancellationToken);
                    }
                }
            })
            .Send("hola")
            .AssertReply("Please confirm. (1) Yes or (2) No")
            .Send("si")
            .AssertReply("Confirmed.")
            .StartTestAsync();
        }

        [Fact]
        public async Task ShouldUsePromptClassStyleProperty()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter()
                .Use(new AutoSaveStateMiddleware(convoState));

            var prompt = new ConfirmPrompt("ConfirmPrompt", defaultLocale: Culture.English)
            {
                Style = ListStyle.Inline
            };

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
                {
                    await convoState.LoadAsync(turnContext, false, cancellationToken);
                    var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                    var dialogs = new DialogSet(dialogState);
                    dialogs.Add(prompt);

                    var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                    var results = await dc.ContinueDialogAsync(cancellationToken);
                    if (results.Status == DialogTurnStatus.Empty)
                    {
                        await dc.PromptAsync(
                            "ConfirmPrompt",
                            new PromptOptions
                            {
                                Prompt = new Activity { Type = ActivityTypes.Message, Text = "is it true?" },
                            },
                            cancellationToken);
                    }
                })
                .Send("hello")
                .AssertReply("is it true? (1) Yes or (2) No")
                .StartTestAsync();
        }

        [Fact]
        public async Task PromptOptionsStyleShouldOverridePromptClassStyleProperty()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter()
                .Use(new AutoSaveStateMiddleware(convoState));

            var prompt = new ConfirmPrompt("ConfirmPrompt", defaultLocale: Culture.English)
            {
                Style = ListStyle.Inline
            };

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
                {
                    await convoState.LoadAsync(turnContext, false, cancellationToken);
                    var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                    var dialogs = new DialogSet(dialogState);
                    dialogs.Add(prompt);

                    var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                    var results = await dc.ContinueDialogAsync(cancellationToken);
                    if (results.Status == DialogTurnStatus.Empty)
                    {
                        await dc.PromptAsync(
                            "ConfirmPrompt",
                            new PromptOptions
                            {
                                Prompt = new Activity { Type = ActivityTypes.Message, Text = "is it true?" },
                                Style = ListStyle.None
                            },
                            cancellationToken);
                    }
                })
                .Send("hello")
                .AssertReply("is it true?")
                .StartTestAsync();
        }

        private Action<Activity> SuggestedActionsValidator(string expectedText, SuggestedActions expectedSuggestedActions)
        {
            return activity =>
            {
                Assert.Equal(expectedText, activity.Text);
                Assert.Equal(expectedSuggestedActions.Actions.Count, activity.SuggestedActions.Actions.Count);
                for (var i = 0; i < expectedSuggestedActions.Actions.Count; i++)
                {
                    Assert.Equal(expectedSuggestedActions.Actions[i].Type, activity.SuggestedActions.Actions[i].Type);
                    Assert.Equal(expectedSuggestedActions.Actions[i].Value, activity.SuggestedActions.Actions[i].Value);
                    Assert.Equal(expectedSuggestedActions.Actions[i].Title, activity.SuggestedActions.Actions[i].Title);
                }
            };
        }
    }
}
