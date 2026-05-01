// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Storage.Transcript;
using Microsoft.Agents.Core.Models;
using Xunit;
using Microsoft.Agents.Core;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Compat;
using Microsoft.Agents.Builder.Dialogs.Prompts;

namespace Microsoft.Agents.Builder.Dialogs.Tests
{
    public class TextPromptTests
    {
        [Fact]
        public void TextPromptWithEmptyIdShouldFail()
        {
            Assert.Throws<ArgumentNullException>(() => { new TextPrompt(string.Empty); });
        }

        [Fact]
        public void TextPromptWithNullIdShouldFail()
        {
            Assert.Throws<ArgumentNullException>(() => { new TextPrompt(null); });
        }

        [Fact]
        public async Task TextPrompt()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter(TestAdapter.CreateConversation(nameof(TextPrompt)))
                .Use(new AutoSaveStateMiddleware(convoState))
                .Use(new TranscriptLoggerMiddleware(new TraceTranscriptLogger(traceActivity: false)));

            var textPrompt = new TextPrompt("TextPrompt");

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, default);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(textPrompt);

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    var options = new PromptOptions { Prompt = new Activity { Type = ActivityTypes.Message, Text = "Enter some text." } };
                    await dc.PromptAsync("TextPrompt", options, cancellationToken);
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    var textResult = (string)results.Result;
                    await turnContext.SendActivityAsync(MessageFactory.Text($"Bot received the text '{textResult}'."), cancellationToken);
                }
            })
            .Send("hello")
            .AssertReply("Enter some text.")
            .Send("some text")
            .AssertReply("Bot received the text 'some text'.")
            .StartTestAsync();
        }

        [Fact]
        public async Task TextPromptWithNaughtyStrings()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter(TestAdapter.CreateConversation(nameof(TextPromptWithNaughtyStrings)))
                .Use(new AutoSaveStateMiddleware(convoState))
                .Use(new TranscriptLoggerMiddleware(new TraceTranscriptLogger(traceActivity: false)));

            var textPrompt = new TextPrompt("TextPrompt");

            var filePath = Path.Combine(new string[] { "Resources", "naughtyStrings.txt" });
            using var sr = new StreamReader(filePath);
            var naughtyString = string.Empty;
            do
            {
                naughtyString = GetNextNaughtyString(sr);
                try 
                {
                    await new TestFlow(adapter, async (turnContext, cancellationToken) =>
                    {
                        await convoState.LoadAsync(turnContext, false, default);
                        var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                        var dialogs = new DialogSet(dialogState);
                        dialogs.Add(textPrompt);

                        var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                        var results = await dc.ContinueDialogAsync(cancellationToken);
                        if (results.Status == DialogTurnStatus.Empty)
                        {
                            var options = new PromptOptions { Prompt = new Activity { Type = ActivityTypes.Message, Text = "Enter some text." } };
                            await dc.PromptAsync("TextPrompt", options, cancellationToken);
                        }
                        else if (results.Status == DialogTurnStatus.Complete)
                        {
                            var textResult = (string)results.Result;
                            await turnContext.SendActivityAsync(MessageFactory.Text(textResult), cancellationToken);
                        }
                    })
                    .Send("hello")
                    .AssertReply("Enter some text.")
                    .Send(naughtyString)
                    .AssertReply(naughtyString)
                    .StartTestAsync();
                }
                catch (Exception e)
                {
                    // If the input message is empty after a .Trim() operation, character the comparison will fail because the reply message will be a
                    // Message Activity with null as Text, this is expected behavior
                    var messageIsBlank = e.Message.Equals(" :\nExpected: \nReceived:", StringComparison.OrdinalIgnoreCase) && naughtyString.Equals(" ", StringComparison.OrdinalIgnoreCase);
                    var messageIsEmpty = e.Message.Equals(":\nExpected:\nReceived:", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(naughtyString);
                    if (!(messageIsBlank || messageIsEmpty))
                    {
                        throw;
                    }
                }
            }
            while (!string.IsNullOrEmpty(naughtyString));
        }

        [Fact]
        public async Task TextPromptValidator()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter(TestAdapter.CreateConversation(nameof(TextPromptValidator)))
                .Use(new AutoSaveStateMiddleware(convoState))
                .Use(new TranscriptLoggerMiddleware(new TraceTranscriptLogger(traceActivity: false)));

            PromptValidator<string> validator = async (promptContext, cancellationToken) =>
            {
                var value = promptContext.Recognized.Value;
                if (value.Length <= 3)
                {
                    await promptContext.Context.SendActivityAsync(MessageFactory.Text("Make sure the text is greater than three characters."), cancellationToken);
                    return false;
                }
                else
                {
                    return true;
                }
            };

            var textPrompt = new TextPrompt("TextPrompt", validator);

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, default);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(textPrompt);

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    var options = new PromptOptions { Prompt = new Activity { Type = ActivityTypes.Message, Text = "Enter some text." } };
                    await dc.PromptAsync("TextPrompt", options, cancellationToken);
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    var textResult = (string)results.Result;
                    await turnContext.SendActivityAsync(MessageFactory.Text($"Bot received the text '{textResult}'."), cancellationToken);
                }
            })
            .Send("hello")
            .AssertReply("Enter some text.")
            .Send("hi")
            .AssertReply("Make sure the text is greater than three characters.")
            .Send("hello")
            .AssertReply("Bot received the text 'hello'.")
            .StartTestAsync();
        }

        [Fact]
        public async Task TextPromptWithRetryPrompt()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter(TestAdapter.CreateConversation(nameof(TextPromptWithRetryPrompt)))
                .Use(new AutoSaveStateMiddleware(convoState))
                .Use(new TranscriptLoggerMiddleware(new TraceTranscriptLogger(traceActivity: false)));

            PromptValidator<string> validator = (promptContext, cancellationToken) =>
            {
                var value = promptContext.Recognized.Value;
                if (value.Length >= 3)
                {
                    return Task.FromResult(true);
                }

                return Task.FromResult(false);
            };
            var textPrompt = new TextPrompt("TextPrompt", validator);

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, default);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(textPrompt);

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    var options = new PromptOptions
                    {
                        Prompt = new Activity { Type = ActivityTypes.Message, Text = "Enter some text." },
                        RetryPrompt = new Activity { Type = ActivityTypes.Message, Text = "Make sure the text is greater than three characters." },
                    };
                    await dc.PromptAsync("TextPrompt", options, cancellationToken);
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    var textResult = (string)results.Result;
                    await turnContext.SendActivityAsync(MessageFactory.Text($"Bot received the text '{textResult}'."), cancellationToken);
                }
            })
            .Send("hello")
            .AssertReply("Enter some text.")
            .Send("hi")
            .AssertReply("Make sure the text is greater than three characters.")
            .Send("hello")
            .AssertReply("Bot received the text 'hello'.")
            .StartTestAsync();
        }

        [Fact]
        public async Task TextPromptValidatorWithMessageShouldNotSendRetryPrompt()
        {
            var convoState = new ConversationState(new MemoryStorage());

            var adapter = new TestAdapter(TestAdapter.CreateConversation(nameof(TextPromptValidatorWithMessageShouldNotSendRetryPrompt)))
                .Use(new AutoSaveStateMiddleware(convoState))
                .Use(new TranscriptLoggerMiddleware(new TraceTranscriptLogger(traceActivity: false)));

            PromptValidator<string> validator = async (promptContext, cancellationToken) =>
            {
                var value = promptContext.Recognized.Value;
                if (value.Length <= 3)
                {
                    await promptContext.Context.SendActivityAsync(MessageFactory.Text("The text should be greater than 3 chars."), cancellationToken);
                    return false;
                }
                else
                {
                    return true;
                }
            };
            var textPrompt = new TextPrompt("TextPrompt", validator);

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await convoState.LoadAsync(turnContext, false, default);
                var dialogState = convoState.GetValue<DialogState>("DialogState", () => new DialogState());
                var dialogs = new DialogSet(dialogState);
                dialogs.Add(textPrompt);

                var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

                var results = await dc.ContinueDialogAsync(cancellationToken);
                if (results.Status == DialogTurnStatus.Empty)
                {
                    var options = new PromptOptions
                    {
                        Prompt = new Activity { Type = ActivityTypes.Message, Text = "Enter some text." },
                        RetryPrompt = new Activity { Type = ActivityTypes.Message, Text = "Make sure the text is greater than three characters." },
                    };
                    await dc.PromptAsync("TextPrompt", options);
                }
                else if (results.Status == DialogTurnStatus.Complete)
                {
                    var textResult = (string)results.Result;
                    await turnContext.SendActivityAsync(MessageFactory.Text($"Bot received the text '{textResult}'."), cancellationToken);
                }
            })
            .Send("hello")
            .AssertReply("Enter some text.")
            .Send("hi")
            .AssertReply("The text should be greater than 3 chars.")
            .Send("hello")
            .AssertReply("Bot received the text 'hello'.")
            .StartTestAsync();
        }

        private static string GetNextNaughtyString(StreamReader sr)
        {
            while (!sr.EndOfStream)
            {
                var line = sr.ReadLine();
                if (line.StartsWith("#") || string.IsNullOrEmpty(line))
                {
                    // do nothing. Read next line. 
                }
                else
                {
                    return line;
                }
            }

            return string.Empty;
        }
    }
}
