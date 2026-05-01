// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    public class DateTimePromptTests
    {
        [Fact]
        public async Task BasicDateTimePrompt()
        {
            var convoState = new ConversationState(new MemoryStorage());

            TestAdapter adapter = new TestAdapter(TestAdapter.CreateConversation(nameof(BasicDateTimePrompt)))
                .Use(new AutoSaveStateMiddleware(convoState))
                .Use(new TranscriptLoggerMiddleware(new TraceTranscriptLogger(traceActivity: false)));

            // Create and add number prompt to DialogSet.
            var dateTimePrompt = new DateTimePrompt("DateTimePrompt", defaultLocale: Culture.English);

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, cancellationToken);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(dateTimePrompt);

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync();
                if (results.Status == DialogTurnStatus.Empty)
                {
                    var options = new PromptOptions { Prompt = new Activity { Type = ActivityTypes.Message, Text = "What date would you like?" } };
                    await dc.PromptAsync("DateTimePrompt", options, cancellationToken);
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    var resolution = ((IList<DateTimeResolution>)results.Result).First();
                    var reply = MessageFactory.Text($"Timex:'{resolution.Timex}' Value:'{resolution.Value}'");
                    await turnContext.SendActivityAsync(reply, cancellationToken);
                }
            })
            .Send("hello")
            .AssertReply("What date would you like?")
            .Send("5th December 2018 at 9am")
            .AssertReply("Timex:'2018-12-05T09' Value:'2018-12-05 09:00:00'")
            .StartTestAsync();
        }

        [Fact]
        public async Task MultipleResolutionsDateTimePrompt()
        {
            var convoState = new ConversationState(new MemoryStorage());

            TestAdapter adapter = new TestAdapter(TestAdapter.CreateConversation(nameof(MultipleResolutionsDateTimePrompt)))
                .Use(new AutoSaveStateMiddleware(convoState))
                .Use(new TranscriptLoggerMiddleware(new TraceTranscriptLogger(traceActivity: false)));

            // Create and add number prompt to DialogSet.
            var dateTimePrompt = new DateTimePrompt("DateTimePrompt", defaultLocale: Culture.English);

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, cancellationToken);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(dateTimePrompt);

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    var options = new PromptOptions { Prompt = new Activity { Type = ActivityTypes.Message, Text = "What date would you like?" } };
                    await dc.PromptAsync("DateTimePrompt", options, cancellationToken);
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    var resolutions = (IList<DateTimeResolution>)results.Result;
                    var timexExpressions = resolutions.Select(r => r.Timex).Distinct();
                    var reply = MessageFactory.Text(string.Join(" ", timexExpressions));
                    await turnContext.SendActivityAsync(reply, cancellationToken);
                }
            })
            .Send("hello")
            .AssertReply("What date would you like?")
            .Send("Wednesday 4 oclock")
            .AssertReply("XXXX-WXX-3T04 XXXX-WXX-3T16")
            .StartTestAsync();
        }

        [Fact]
        public async Task DateTimePromptWithValidator()
        {
            string folder = Environment.CurrentDirectory;
            var convoState = new ConversationState(new MemoryStorage());

            TestAdapter adapter = new TestAdapter(TestAdapter.CreateConversation(nameof(DateTimePromptWithValidator)))
                .Use(new AutoSaveStateMiddleware(convoState))
                .Use(new TranscriptLoggerMiddleware(new TraceTranscriptLogger(traceActivity: false)));

            // Create and add number prompt to DialogSet.
            var dateTimePrompt = new DateTimePrompt("DateTimePrompt", CustomValidator, defaultLocale: Culture.English);

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, cancellationToken);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(dateTimePrompt);

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    var options = new PromptOptions { Prompt = new Activity { Type = ActivityTypes.Message, Text = "What date would you like?" } };
                    await dc.PromptAsync("DateTimePrompt", options, cancellationToken);
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    var resolution = ((IList<DateTimeResolution>)results.Result).First();
                    var reply = MessageFactory.Text($"Timex:'{resolution.Timex}' Value:'{resolution.Value}'");
                    await turnContext.SendActivityAsync(reply, cancellationToken);
                }
            })
            .Send("hello")
            .AssertReply("What date would you like?")
            .Send("5th December 2018 at 9am")
            .AssertReply("Timex:'2018-12-05' Value:'2018-12-05'")
            .StartTestAsync();
        }

        private Task<bool> CustomValidator(PromptValidatorContext<IList<DateTimeResolution>> prompt, CancellationToken cancellationToken)
        {
            if (prompt.Recognized.Succeeded)
            {
                var resolution = prompt.Recognized.Value.First();

                // re-write the resolution to just include the date part.
                var rewrittenResolution = new DateTimeResolution
                {
                    Timex = resolution.Timex.Split('T')[0],
                    Value = resolution.Value.Split(' ')[0],
                };
                prompt.Recognized.Value = new List<DateTimeResolution> { rewrittenResolution };
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
    }
}
