using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;
using Moq;
using Xunit;

namespace Microsoft.Agents.Builder.Testing
{
    public class TestFlowTests
    {
        [Fact]
        public async Task ValidateReplyContains()
        {
            var expectedSubstring = "expected substring";
            await new TestFlow(new TestAdapter(), async (turnContext, cancellationToken) =>
                {
                    await turnContext.SendActivityAsync(
                        $"String with {expectedSubstring} in it",
                        cancellationToken: cancellationToken);
                })
                .Send("hello")
                .AssertReplyContains(expectedSubstring)
                .StartTestAsync();
        }

        [Fact]
        public async Task ValidateReplyContains_ExceptionWithDescription()
        {
            const string exceptionDescription = "Description message";
            const string stringThatNotSubstring = "some string";
            var message = "Just a sample string".Replace(stringThatNotSubstring, string.Empty);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await new TestFlow(new TestAdapter(), async (turnContext, cancellationToken) =>
                    {
                        await turnContext.SendActivityAsync(
                            message,
                            cancellationToken: cancellationToken);
                    })
                    .Send("hello")
                    .AssertReplyContains(stringThatNotSubstring, exceptionDescription)
                    .StartTestAsync();
            });
        }

        [Fact]
        public async Task ValidateDelay()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            await new TestFlow(new TestAdapter())
            .Send("hello")
            .Delay(TimeSpan.FromSeconds(1.1))
            .Send("some text")
            .StartTestAsync();
            sw.Stop();

            Assert.True(sw.Elapsed.TotalSeconds > 1, $"Delay broken, elapsed time {sw.Elapsed}?");
        }

        [Fact]
#pragma warning disable CS0618 // Type or member is obsolete
        public async Task ValidateNoReply()
        {
            const string message = "Just a sample string";
            await new TestFlow(new TestAdapter(), async (turnContext, cancellationToken) =>
                {
                    await turnContext.SendActivityAsync(
                        message,
                        cancellationToken: cancellationToken);
                })
                .Send("hello")
                .AssertReply(message)
                .AssertNoReply()
                .StartTestAsync();
        }
#pragma warning restore CS0618 // Type or member is obsolete

        [Fact]
#pragma warning disable CS0618 // Type or member is obsolete
        public async Task ValidateNoReply_ExceptionWithDescription()
        {
            const string exceptionDescription = "Description message";
            const string message = "Just a sample string";
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await new TestFlow(new TestAdapter(), async (turnContext, cancellationToken) =>
                    {
                        await turnContext.SendActivityAsync(
                            message,
                            cancellationToken: cancellationToken);
                        await turnContext.SendActivityAsync(
                            message,
                            cancellationToken: cancellationToken);
                    })
                    .Send("hello")
                    .AssertReply(message)
                    .AssertNoReply(exceptionDescription)
                    .StartTestAsync();
            });
        }
#pragma warning restore CS0618 // Type or member is obsolete

        [Fact]
        public async Task SendConversationUpdate_WithExplicitMembers_AddsThem()
        {
            var addedIds = new List<string>();
            await new TestFlow(new TestAdapter(), (turnContext, cancellationToken) =>
                {
                    if (turnContext.Activity.Type == ActivityTypes.ConversationUpdate)
                    {
                        foreach (var member in turnContext.Activity.MembersAdded)
                            addedIds.Add(member.Id);
                    }
                    return Task.CompletedTask;
                })
                .SendConversationUpdate(new[] { new ChannelAccount("alice", "Alice"), new ChannelAccount("bob", "Bob") })
                .StartTestAsync();

            Assert.Contains("alice", addedIds);
            Assert.Contains("bob", addedIds);
        }

        [Fact]
        public async Task SendConversationUpdate_NullMembers_UsesDefaultUser()
        {
            string addedId = null;
            var adapter = new TestAdapter();
            await new TestFlow(adapter, (turnContext, cancellationToken) =>
                {
                    if (turnContext.Activity.Type == ActivityTypes.ConversationUpdate
                        && turnContext.Activity.MembersAdded?.Count > 0)
                    {
                        addedId = turnContext.Activity.MembersAdded[0].Id;
                    }
                    return Task.CompletedTask;
                })
                .SendConversationUpdate((IEnumerable<ChannelAccount>)null)
                .StartTestAsync();

            Assert.Equal(adapter.Conversation.User.Id, addedId);
        }

        [Fact]
        public async Task SendConversationUpdate_EmptyMembers_ThrowsArgumentException()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await new TestFlow(new TestAdapter())
                    .SendConversationUpdate(Array.Empty<ChannelAccount>())
                    .StartTestAsync();
            });
        }

        [Fact]
        public async Task AssertNoMoreReplies_WhenNoReply_Passes()
        {
            const string message = "Just a sample string";
            await new TestFlow(new TestAdapter(), async (turnContext, cancellationToken) =>
                {
                    await turnContext.SendActivityAsync(message, cancellationToken: cancellationToken);
                })
                .Send("hello")
                .AssertReply(message)
                .AssertNoMoreReplies()
                .StartTestAsync();
        }

        [Fact]
        public async Task AssertNoMoreReplies_WhenReplyArrives_Throws()
        {
            const string message = "Just a sample string";
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await new TestFlow(new TestAdapter(), async (turnContext, cancellationToken) =>
                    {
                        await turnContext.SendActivityAsync(message, cancellationToken: cancellationToken);
                        await turnContext.SendActivityAsync(message, cancellationToken: cancellationToken);
                    })
                    .Send("hello")
                    .AssertReply(message)
                    .AssertNoMoreReplies("no second reply expected")
                    .StartTestAsync();
            });
        }

        [Fact]
        public async Task AssertNoMoreReplies_ErrorMessageContainsDescription()
        {
            const string description = "my description";
            const string message = "a reply";
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await new TestFlow(new TestAdapter(), async (turnContext, cancellationToken) =>
                    {
                        await turnContext.SendActivityAsync(message, cancellationToken: cancellationToken);
                        await turnContext.SendActivityAsync(message, cancellationToken: cancellationToken);
                    })
                    .Send("hello")
                    .AssertReply(message)
                    .AssertNoMoreReplies(description)
                    .StartTestAsync();
            });
            Assert.Contains(description, ex.Message);
        }

        [Fact]
        public async Task AssertTypingIndicator_WhenTypingActivity_Passes()
        {
            await new TestFlow(new TestAdapter(), async (turnContext, cancellationToken) =>
                {
                    await turnContext.SendActivityAsync(
                        new Microsoft.Agents.Core.Models.Activity { Type = ActivityTypes.Typing },
                        cancellationToken: cancellationToken);
                    await turnContext.SendActivityAsync("done", cancellationToken: cancellationToken);
                })
                .Send("hello")
                .AssertTypingIndicator()
                .AssertReply("done")
                .StartTestAsync();
        }

        [Fact]
        public async Task AssertTypingIndicator_WhenWrongType_Throws()
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await new TestFlow(new TestAdapter(), async (turnContext, cancellationToken) =>
                    {
                        await turnContext.SendActivityAsync("not a typing indicator", cancellationToken: cancellationToken);
                    })
                    .Send("hello")
                    .AssertTypingIndicator("should be typing")
                    .StartTestAsync();
            });
            Assert.Contains("Expected typing indicator", ex.Message);
            Assert.Contains("should be typing", ex.Message);
        }

        [Fact]
        public async Task AssertReplySatisfies_AsyncDelegate_Passes()
        {
            bool called = false;
            await new TestFlow(new TestAdapter(), async (turnContext, cancellationToken) =>
                {
                    await turnContext.SendActivityAsync("hello reply", cancellationToken: cancellationToken);
                })
                .Send("hello")
                .AssertReplySatisfies(async activity =>
                {
                    called = true;
                    await Task.CompletedTask;
                    if (activity.Text != "hello reply")
                        throw new InvalidOperationException("wrong text");
                })
                .StartTestAsync();

            Assert.True(called);
        }

        [Fact]
        public async Task AssertReplySatisfies_AsyncDelegate_ExceptionPropagates()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await new TestFlow(new TestAdapter(), async (turnContext, cancellationToken) =>
                    {
                        await turnContext.SendActivityAsync("wrong", cancellationToken: cancellationToken);
                    })
                    .Send("hello")
                    .AssertReplySatisfies(activity =>
                    {
                        if (activity.Text == "wrong")
                            throw new InvalidOperationException("Validation failed as expected");
                        return Task.CompletedTask;
                    })
                    .StartTestAsync();
            });
        }

        [Fact]
        public async Task AssertReplySatisfies_Validator_CallsValidateAsync()
        {
            bool validatorCalled = false;
            var mockValidator = new Mock<IResponseValidator>();
            mockValidator
                .Setup(v => v.ValidateAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .Returns(() => { validatorCalled = true; return Task.CompletedTask; });

            await new TestFlow(new TestAdapter(), async (turnContext, cancellationToken) =>
                {
                    await turnContext.SendActivityAsync("any reply", cancellationToken: cancellationToken);
                })
                .Send("hello")
                .AssertReplySatisfies(mockValidator.Object)
                .StartTestAsync();

            Assert.True(validatorCalled);
        }

        [Fact]
        public async Task AssertReplySatisfies_NoReply_ThrowsBeforeCallingValidator()
        {
            var mockValidator = new Mock<IResponseValidator>();

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await new TestFlow(new TestAdapter())
                    .Send("hello")
                    .AssertReplySatisfies(mockValidator.Object, timeout: 100)
                    .StartTestAsync();
            });

            // Validator should NOT have been called because there was no reply
            mockValidator.Verify(
                v => v.ValidateAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}
