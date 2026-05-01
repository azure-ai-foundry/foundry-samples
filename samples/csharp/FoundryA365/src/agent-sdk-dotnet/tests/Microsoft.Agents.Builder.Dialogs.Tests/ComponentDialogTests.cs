// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Storage.Transcript;
using Microsoft.Agents.Core.Models;
using Microsoft.Recognizers.Text;
using Xunit;
using Moq;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Compat;
using Microsoft.Agents.Builder.Dialogs.Prompts;

namespace Microsoft.Agents.Builder.Dialogs.Tests
{
    [Trait("TestCategory", "Prompts")]
    [Trait("TestCategory", "ComponentDialog Tests")]
    public class ComponentDialogTests
    {
        private readonly Mock<DialogContext> _dialogContext = new(new DialogSet(), new Mock<ITurnContext>().Object, new DialogState());

        [Fact]
        public async Task CallDialogInParentComponent()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter(TestAdapter.CreateConversation(nameof(CallDialogDefinedInParentComponent)))
                .Use(new AutoSaveStateMiddleware(convoState))
                .Use(new TranscriptLoggerMiddleware(new TraceTranscriptLogger(traceActivity: false)));

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, cancellationToken);
                var state = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(state);

                var childComponent = new ComponentDialog("childComponent");
                var childStep = new WaterfallStep[]
                    {
                        async (step, token) =>
                        {
                            await step.Context.SendActivityAsync("Child started.");
                            return await step.BeginDialogAsync("parentDialog", "test");
                        },
                        async (step, token) =>
                        {
                            await step.Context.SendActivityAsync($"Child finished. Value: {step.Result}");
                            return await step.EndDialogAsync();
                        }
                    };
                childComponent.AddDialog(new WaterfallDialog("childDialog", childStep));

                var parentComponent = new ComponentDialog("parentComponent");
                parentComponent.AddDialog(childComponent);
                var parentStep = new WaterfallStep[]
                    {
                        async (step, token) =>
                        {
                            await step.Context.SendActivityAsync("Parent called.");
                            return await step.EndDialogAsync(step.Options);
                        }
                    };
                parentComponent.AddDialog(new WaterfallDialog("parentDialog", parentStep));

                dialogs.Add(parentComponent);

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    await dc.BeginDialogAsync("parentComponent", null, cancellationToken);
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    var value = (int)results.Result;
                    await turnContext.SendActivityAsync(MessageFactory.Text($"Bot received the number '{value}'."), cancellationToken);
                }
            })
            .Send("Hi")
                .AssertReply("Child started.")
                .AssertReply("Parent called.")
                .AssertReply("Child finished. Value: test")
            .StartTestAsync();
        }

        [Fact]
        public async Task BasicWaterfallTest()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter(TestAdapter.CreateConversation(nameof(BasicWaterfallTest)))
                .Use(new AutoSaveStateMiddleware(convoState))
                .Use(new TranscriptLoggerMiddleware(new TraceTranscriptLogger(traceActivity: false)));

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, cancellationToken);
                var state = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(state);

                dialogs.Add(CreateWaterfall());
                dialogs.Add(new NumberPrompt<int>("number", defaultLocale: Culture.English));

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    await dc.BeginDialogAsync("test-waterfall", null, cancellationToken);
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    var value = (int)results.Result;
                    await turnContext.SendActivityAsync(MessageFactory.Text($"Bot received the number '{value}'."), cancellationToken);
                }
            })
            .Send("hello")
            .AssertReply("Enter a number.")
            .Send("42")
            .AssertReply("Thanks for '42'")
            .AssertReply("Enter another number.")
            .Send("64")
            .AssertReply("Bot received the number '64'.")
            .StartTestAsync();
        }

        [Fact]
        public async Task BasicComponentDialogTest()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter(TestAdapter.CreateConversation(nameof(BasicComponentDialogTest)))
                .Use(new AutoSaveStateMiddleware(convoState))
                .Use(new TranscriptLoggerMiddleware(new TraceTranscriptLogger(traceActivity: false)));

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, cancellationToken);
                var state = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(state);

                dialogs.Add(new TestComponentDialog());

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    await dc.BeginDialogAsync("TestComponentDialog", null, cancellationToken);
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    var value = (int)results.Result;
                    await turnContext.SendActivityAsync(MessageFactory.Text($"Bot received the number '{value}'."), cancellationToken);
                }
            })
            .Send("hello")
            .AssertReply("Enter a number.")
            .Send("42")
            .AssertReply("Thanks for '42'")
            .AssertReply("Enter another number.")
            .Send("64")
            .AssertReply("Bot received the number '64'.")
            .StartTestAsync();
        }

        [Fact]
        public async Task NestedComponentDialogTest()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter(TestAdapter.CreateConversation(nameof(NestedComponentDialogTest)))
                .Use(new AutoSaveStateMiddleware(convoState))
                .Use(new TranscriptLoggerMiddleware(new TraceTranscriptLogger(traceActivity: false)));

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, cancellationToken);
                var state = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(state);

                dialogs.Add(new TestNestedComponentDialog());

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    await dc.BeginDialogAsync("TestNestedComponentDialog", null, cancellationToken);
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    var value = (int)results.Result;
                    await turnContext.SendActivityAsync(MessageFactory.Text($"Bot received the number '{value}'."), cancellationToken);
                }
            })
            .Send("hello")

            // step 1
            .AssertReply("Enter a number.")

            // step 2
            .Send("42")
            .AssertReply("Thanks for '42'")
            .AssertReply("Enter another number.")

            // step 3 and step 1 again (nested component)
            .Send("64")
            .AssertReply("Got '64'.")
            .AssertReply("Enter a number.")

            // step 2 again (from the nested component)
            .Send("101")
            .AssertReply("Thanks for '101'")
            .AssertReply("Enter another number.")

            // driver code
            .Send("5")
            .AssertReply("Bot received the number '5'.")
            .StartTestAsync();
        }

        [Fact]
        public async Task CallDialogDefinedInParentComponent()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter()
                .Use(new AutoSaveStateMiddleware(convoState));

            var options = new Dictionary<string, string> { { "value", "test" } };

            var childComponent = new ComponentDialog("childComponent");
            var childActions = new WaterfallStep[]
            {
                async (step, ct) =>
                {
                    await step.Context.SendActivityAsync("Child started.");
                    return await step.BeginDialogAsync("parentDialog", options);
                },
                async (step, ct) =>
                {
                    Assert.Equal("test", (string)step.Result);
                    await step.Context.SendActivityAsync("Child finished.");
                    return await step.EndDialogAsync();
                },
            };
            childComponent.AddDialog(new WaterfallDialog(
                "childDialog",
                childActions));

            var parentComponent = new ComponentDialog("parentComponent");
            parentComponent.AddDialog(childComponent);
            var parentActions = new WaterfallStep[]
            {
                async (step, dc) =>
                {
                    var stepOptions = step.Options as IDictionary<string, string>;
                    Assert.NotNull(stepOptions);
                    Assert.True(stepOptions.ContainsKey("value"));
                    await step.Context.SendActivityAsync($"Parent called with: {stepOptions["value"]}");
                    return await step.EndDialogAsync(stepOptions["value"]);
                },
            };
            parentComponent.AddDialog(new WaterfallDialog(
                "parentDialog",
                parentActions));

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, cancellationToken);
                var state = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(state);
                dialogs.Add(parentComponent);

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    await dc.BeginDialogAsync("parentComponent", null, cancellationToken);
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    var value = (int)results.Result;
                    await turnContext.SendActivityAsync(MessageFactory.Text("Done"), cancellationToken);
                }
            })
            .Send("Hi")
            .AssertReply("Child started.")
            .AssertReply("Parent called with: test")
            .AssertReply("Child finished.")
            .StartTestAsync();
        }

        [Fact]
        public async Task BeginDialogAsync_ShouldThrowOnNullDialogContext()
        {
            var dialog = new ComponentDialog("dialogId");

            await Assert.ThrowsAsync<ArgumentNullException>(() => dialog.BeginDialogAsync(null));
        }

        [Fact]
        public async Task BeginDialogAsync_ShouldThrowOnOptionsCancellationToken()
        {
            var dialog = new ComponentDialog("dialogId");

            await Assert.ThrowsAsync<ArgumentException>(() => dialog.BeginDialogAsync(_dialogContext.Object, CancellationToken.None));
        }

        [Fact]
        public async Task ResumeDialogAsync_ShouldThrowOnResultCancellationToken()
        {
            var dialog = new ComponentDialog("dialogId");

            await Assert.ThrowsAsync<ArgumentException>(() => dialog.ResumeDialogAsync(_dialogContext.Object, DialogReason.BeginCalled, CancellationToken.None));
        }

        [Fact]
        public async Task ResumeDialogAsync_ShouldReturnEndOfTurn()
        {
            _dialogContext.Object.Stack.Add(new DialogInstance { Id = "A1", State = new Dictionary<string, object> { { "dialogs", new DialogState() } } });
            var dialog = new ComponentDialog("dialogId");
            dialog.AddDialog(new WaterfallDialog("A2"));
            dialog.InitialDialogId = null;

            var result = await dialog.ResumeDialogAsync(_dialogContext.Object, DialogReason.BeginCalled);

            Assert.Equal(Dialog.EndOfTurn, result);
            Assert.Equal("A2", dialog.InitialDialogId);
        }

        private static TestFlow CreateTestFlow(WaterfallDialog waterfallDialog)
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter()
                .Use(new AutoSaveStateMiddleware(convoState));

            var testFlow = new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, cancellationToken);
                var state = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(state);

                dialogs.Add(new CancelledComponentDialog(waterfallDialog));

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    results = await dc.BeginDialogAsync("TestComponentDialog", null, cancellationToken);
                }

                if (results.Status == DialogTurnStatus.Cancelled)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text($"Component dialog cancelled (result value is {results.Result?.ToString()})."), cancellationToken);
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    var value = (int)results.Result;
                    await turnContext.SendActivityAsync(MessageFactory.Text($"Bot received the number '{value}'."), cancellationToken);
                }
            });
            return testFlow;
        }

        private static WaterfallDialog CreateWaterfall()
        {
            var steps = new WaterfallStep[]
            {
                WaterfallStep1,
                WaterfallStep2,
            };
            return new WaterfallDialog("test-waterfall", steps);
        }

        private static async Task<DialogTurnResult> WaterfallStep1(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.PromptAsync("number", new PromptOptions { Prompt = MessageFactory.Text("Enter a number.") }, cancellationToken);
        }

        private static async Task<DialogTurnResult> WaterfallStep2(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (stepContext.Values != null)
            {
                var numberResult = (int)stepContext.Result;
                await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Thanks for '{numberResult}'"), cancellationToken);
            }

            return await stepContext.PromptAsync("number", new PromptOptions { Prompt = MessageFactory.Text("Enter another number.") }, cancellationToken);
        }

        private static async Task<DialogTurnResult> WaterfallStep3(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (stepContext.Values != null)
            {
                var numberResult = (int)stepContext.Result;
                await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Got '{numberResult}'."), cancellationToken);
            }

            return await stepContext.BeginDialogAsync("TestComponentDialog", null, cancellationToken);
        }

        private static Task<DialogTurnResult> CancelledWaterfallStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return Task.FromResult(new DialogTurnResult(DialogTurnStatus.Cancelled, 42));
        }

        private class TestComponentDialog : ComponentDialog
        {
            public TestComponentDialog()
                : base("TestComponentDialog")
            {
                AddDialog(CreateWaterfall());
                AddDialog(new NumberPrompt<int>("number", defaultLocale: Culture.English));
            }
        }

        private class TestNestedComponentDialog : ComponentDialog
        {
            public TestNestedComponentDialog()
                : base("TestNestedComponentDialog")
            {
                var steps = new WaterfallStep[]
                {
                    WaterfallStep1,
                    WaterfallStep2,
                    WaterfallStep3,
                };
                AddDialog(new WaterfallDialog(
                    "test-waterfall",
                    steps));
                AddDialog(new NumberPrompt<int>("number", defaultLocale: Culture.English));
                AddDialog(new TestComponentDialog());
            }
        }

        private class CancelledComponentDialog : ComponentDialog
        {
            public CancelledComponentDialog(Dialog waterfallDialog)
                : base("TestComponentDialog")
            {
                AddDialog(waterfallDialog);
                AddDialog(new NumberPrompt<int>("number", defaultLocale: Culture.English));
            }
        }
    }
}
