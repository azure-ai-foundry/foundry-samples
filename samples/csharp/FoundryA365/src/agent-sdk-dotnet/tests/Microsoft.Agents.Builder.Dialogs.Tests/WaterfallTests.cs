// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Core.Models;
using Microsoft.Recognizers.Text;
using Xunit;
using Microsoft.Agents.Core;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Compat;
using Microsoft.Agents.Builder.Dialogs.Prompts;

namespace Microsoft.Agents.Builder.Dialogs.Tests
{
    public class WaterfallTests
    {
        public static WaterfallDialog Create_Waterfall3()
        {
            var steps = new WaterfallStep[]
            {
                Waterfall3_Step1,
                Waterfall3_Step2,
            };
            return new WaterfallDialog(
                "test-waterfall-a",
                steps);
        }

        public static WaterfallDialog Create_Waterfall4()
        {
            var steps = new WaterfallStep[]
            {
                Waterfall4_Step1,
                Waterfall4_Step2,
            };
            return new WaterfallDialog(
                "test-waterfall-b",
                steps);
        }

        public static WaterfallDialog Create_Waterfall5()
        {
            var steps = new WaterfallStep[]
            {
                Waterfall5_Step1,
                Waterfall5_Step2,
            };
            return new WaterfallDialog(
                "test-waterfall-c",
                steps);
        }

        [Fact]
        public async Task Waterfall()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter()
                .Use(new AutoSaveStateMiddleware(convoState));

            var steps = new WaterfallStep[]
            {
                async (step, cancellationToken) =>
                {
                    await step.Context.SendActivityAsync("step1");
                    return Dialog.EndOfTurn;
                },
                async (step, cancellationToken) =>
                {
                    await step.Context.SendActivityAsync("step2");
                    return Dialog.EndOfTurn;
                },
                async (step, cancellationToken) =>
                {
                    await step.Context.SendActivityAsync("step3");
                    return Dialog.EndOfTurn;
                },
            };
            var waterfallDialog = new WaterfallDialog(
                "test",
                steps);

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, default);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(waterfallDialog);

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);
                await dc.ContinueDialogAsync(cancellationToken);
                if (!turnContext.Responded)
                {
                    await dc.BeginDialogAsync("test", null, cancellationToken);
                }
            })
            .Send("hello")
            .AssertReply("step1")
            .Send("hello")
            .AssertReply("step2")
            .Send("hello")
            .AssertReply("step3")
            .StartTestAsync();
        }

        [Fact]
        public async Task WaterfallStepParentIsWaterfallParent()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter()
                .Use(new AutoSaveStateMiddleware(convoState));

            const string WATERFALL_PARENT_ID = "waterfall-parent-test-dialog";
            var waterfallParent = new ComponentDialog(WATERFALL_PARENT_ID);

            var steps = new WaterfallStep[]
            {
                async (step, cancellationToken) =>
                {
                    Assert.Equal(step.Parent.ActiveDialog.Id, waterfallParent.Id);
                    await step.Context.SendActivityAsync("verified");
                    return Dialog.EndOfTurn;
                }
            };
            
            waterfallParent.AddDialog(new WaterfallDialog(
                "test",
                steps));
            waterfallParent.InitialDialogId = "test";

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, default);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(waterfallParent);

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);
                await dc.ContinueDialogAsync(cancellationToken);
                if (!turnContext.Responded)
                {
                    await dc.BeginDialogAsync(WATERFALL_PARENT_ID, null, cancellationToken);
                }
            })
            .Send("hello")
            .AssertReply("verified")
            .StartTestAsync();
        }

        [Fact]
        public async Task WaterfallWithCallback()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter()
                .Use(new AutoSaveStateMiddleware(convoState));

            var steps = new WaterfallStep[]
            {
                async (step, cancellationToken) =>
                {
                    await step.Context.SendActivityAsync("step1");
                    return Dialog.EndOfTurn;
                },
                async (step, cancellationToken) =>
                {
                    await step.Context.SendActivityAsync("step2");
                    return Dialog.EndOfTurn;
                },
                async (step, cancellationToken) =>
                {
                    await step.Context.SendActivityAsync("step3");
                    return Dialog.EndOfTurn;
                },
            };
            var waterfallDialog = new WaterfallDialog(
                "test",
                steps);

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, default);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(waterfallDialog);

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);
                await dc.ContinueDialogAsync(cancellationToken);
                if (!turnContext.Responded)
                {
                    await dc.BeginDialogAsync("test", null, cancellationToken);
                }
            })
            .Send("hello")
            .AssertReply("step1")
            .Send("hello")
            .AssertReply("step2")
            .Send("hello")
            .AssertReply("step3")
            .StartTestAsync();
        }

        [Fact]
        public void WaterfallWithStepsNull()
        {
            Assert.Throws<ArgumentNullException>(() => { new WaterfallDialog("test").AddStep(null); });
        }

        [Fact]
        public async Task WaterfallWithClass()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter()
                .Use(new AutoSaveStateMiddleware(convoState));

            var waterfallDialog = new MyWaterfallDialog("test");

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, default);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(waterfallDialog);

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);
                await dc.ContinueDialogAsync(cancellationToken);
                if (!turnContext.Responded)
                {
                    await dc.BeginDialogAsync("test", null, cancellationToken);
                }
            })
            .Send("hello")
            .AssertReply("step1")
            .Send("hello")
            .AssertReply("step2")
            .Send("hello")
            .AssertReply("step3")
            .StartTestAsync();
        }

        [Fact]
        public async Task WaterfallPrompt()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter()
                .Use(new AutoSaveStateMiddleware(convoState));

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, default);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);

                dialogs.Add(Create_Waterfall2());
                dialogs.Add(new NumberPrompt<int>("number", defaultLocale: Culture.English));

                var dc = await dialogs.CreateContextAsync(turnContext);

                await dc.ContinueDialogAsync();

                if (!turnContext.Responded)
                {
                    await dc.BeginDialogAsync("test-waterfall");
                }
            })
            .Send("hello")
            .AssertReply("step1")
            .AssertReply("Enter a number.")
            .Send("hello again")
            .AssertReply("It must be a number")
            .Send("42")
            .AssertReply("Thanks for '42'")
            .AssertReply("step2")
            .AssertReply("Enter a number.")
            .Send("apple")
            .AssertReply("It must be a number")
            .Send("orange")
            .AssertReply("It must be a number")
            .Send("64")
            .AssertReply("Thanks for '64'")
            .AssertReply("step3")
            .StartTestAsync();
        }

        [Fact]
        public async Task WaterfallNested()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter()
                .Use(new AutoSaveStateMiddleware(convoState));

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, default);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);

                dialogs.Add(Create_Waterfall3());
                dialogs.Add(Create_Waterfall4());
                dialogs.Add(Create_Waterfall5());

                var dc = await dialogs.CreateContextAsync(turnContext);

                await dc.ContinueDialogAsync();

                if (!turnContext.Responded)
                {
                    await dc.BeginDialogAsync("test-waterfall-a");
                }
            })
            .Send("hello")
            .AssertReply("step1")
            .AssertReply("step1.1")
            .Send("hello")
            .AssertReply("step1.2")
            .Send("hello")
            .AssertReply("step2")
            .AssertReply("step2.1")
            .Send("hello")
            .AssertReply("step2.2")
            .StartTestAsync();
        }

        [Fact]
        public async Task WaterfallDateTimePromptFirstInvalidThenValidInput()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var dateTimePrompt = new DateTimePrompt("dateTimePrompt", defaultLocale: Culture.English);

            var steps = new WaterfallStep[]
            {
                async (stepContext, cancellationToken) =>
                {
                    return await stepContext.PromptAsync("dateTimePrompt", new PromptOptions { Prompt = new Activity { Text = "Provide a date", Type = ActivityTypes.Message } });
                },
                async (stepContext, cancellationToken) =>
                {
                    Assert.NotNull(stepContext);
                    return await stepContext.EndDialogAsync();
                },
            };
            var waterfallDialog = new WaterfallDialog(
                "test-dateTimePrompt",
                steps);

            var adapter = new TestAdapter()
                .Use(new AutoSaveStateMiddleware(convoState));

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, default);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);

                dialogs.Add(dateTimePrompt);
                dialogs.Add(waterfallDialog);

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                await dc.ContinueDialogAsync(cancellationToken);

                if (!turnContext.Responded)
                {
                    await dc.BeginDialogAsync("test-dateTimePrompt", null, cancellationToken);
                }
            })
            .Send("hello")
            .AssertReply("Provide a date")
            .Send("hello again")
            .AssertReply("Provide a date")
            .Send("Wednesday 4 oclock")
            .StartTestAsync();
        }

        private static WaterfallDialog Create_Waterfall2()
        {
            var steps = new WaterfallStep[]
                {
                    Waterfall2_Step1,
                    Waterfall2_Step2,
                    Waterfall2_Step3,
                };
            return new WaterfallDialog(
                "test-waterfall",
                steps);
        }

        private static async Task<DialogTurnResult> Waterfall2_Step1(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            await stepContext.Context.SendActivityAsync("step1");
            return await stepContext.PromptAsync("number", new PromptOptions
            {
                Prompt = MessageFactory.Text("Enter a number."),
                RetryPrompt = MessageFactory.Text("It must be a number"),
            });
        }

        private static async Task<DialogTurnResult> Waterfall2_Step2(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (stepContext.Values != null)
            {
                var numberResult = (int)stepContext.Result;
                await stepContext.Context.SendActivityAsync($"Thanks for '{numberResult}'");
            }

            await stepContext.Context.SendActivityAsync("step2");
            return await stepContext.PromptAsync(
                "number",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text("Enter a number."),
                    RetryPrompt = MessageFactory.Text("It must be a number"),
                });
        }

        private static async Task<DialogTurnResult> Waterfall2_Step3(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (stepContext.Values != null)
            {
                var numberResult = (int)stepContext.Result;
                await stepContext.Context.SendActivityAsync($"Thanks for '{numberResult}'");
            }

            await stepContext.Context.SendActivityAsync("step3");
            return await stepContext.EndDialogAsync(new Dictionary<string, object> { { "Value", "All Done!" } });
        }

        private static async Task<DialogTurnResult> Waterfall3_Step1(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            await stepContext.Context.SendActivityAsync(MessageFactory.Text("step1"), cancellationToken);
            return await stepContext.BeginDialogAsync("test-waterfall-b", null, cancellationToken);
        }

        private static async Task<DialogTurnResult> Waterfall3_Step2(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            await stepContext.Context.SendActivityAsync(MessageFactory.Text("step2"), cancellationToken);
            return await stepContext.BeginDialogAsync("test-waterfall-c", null, cancellationToken);
        }

        private static async Task<DialogTurnResult> Waterfall4_Step1(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            await stepContext.Context.SendActivityAsync(MessageFactory.Text("step1.1"), cancellationToken);
            return Dialog.EndOfTurn;
        }

        private static async Task<DialogTurnResult> Waterfall4_Step2(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            await stepContext.Context.SendActivityAsync(MessageFactory.Text("step1.2"), cancellationToken);
            return Dialog.EndOfTurn;
        }

        private static async Task<DialogTurnResult> Waterfall5_Step1(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            await stepContext.Context.SendActivityAsync(MessageFactory.Text("step2.1"), cancellationToken);
            return Dialog.EndOfTurn;
        }

        private static async Task<DialogTurnResult> Waterfall5_Step2(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            await stepContext.Context.SendActivityAsync(MessageFactory.Text("step2.2"), cancellationToken);
            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }
    }
}
