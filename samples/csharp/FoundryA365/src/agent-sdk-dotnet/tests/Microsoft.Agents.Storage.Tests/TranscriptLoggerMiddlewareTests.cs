// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage.Transcript;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Storage.Tests
{
    public class TranscriptLoggerMiddlewareTests
    {
        /*
        [Fact]
        public async Task ShouldNotLogContinueConversation()
        {
            var transcriptStore = new MemoryTranscriptStore();
            var sut = new TranscriptLoggerMiddleware(transcriptStore);

            var conversationId = Guid.NewGuid().ToString();
            var adapter = new TestAdapter(TestAdapter.CreateConversation(conversationId))
                .Use(sut);

            await new TestFlow(adapter, async (context, cancellationToken) =>
                {
                    await context.SendActivityAsync("bar", cancellationToken: cancellationToken);
                })
                .Send("foo")
                .AssertReply(async activity =>
                {
                    Assert.Equal("bar", ((Activity)activity).Text);
                    var activities = await transcriptStore.GetTranscriptActivitiesAsync(activity.ChannelId, conversationId);
                    Assert.Equal(2, activities.Items.Count);
                })
                .Send(new Activity(ActivityTypes.Event) { Name = ActivityEventNames.ContinueConversation })
                .AssertReply(async activity =>
                {
                    // Ensure the event hasn't been added to the transcript.
                    var activities = await transcriptStore.GetTranscriptActivitiesAsync(activity.ChannelId, conversationId);
                    Assert.DoesNotContain(activities.Items, a => ((Activity)a).Type == ActivityTypes.Event && ((Activity)a).Name == ActivityEventNames.ContinueConversation);
                    Assert.Equal(3, activities.Items.Count);
                })
                .StartTestAsync();
        }
        
        [Fact]
        public async Task OnTurnAsync_ShouldExecuteOnUpdateActivityHandler()
        {
            var transcriptStore = new MemoryTranscriptStore();
            var sut = new TranscriptLoggerMiddleware(transcriptStore);

            var conversationId = Guid.NewGuid().ToString();
            var adapter = new TestAdapter(TestAdapter.CreateConversation(conversationId))
                .Use(sut);

            await new TestFlow(adapter, async (context, cancellationToken) =>
            {
                var created = new Activity
                {
                    Text = "created"
                };
                await context.SendActivityAsync(created, cancellationToken: cancellationToken);

                var updated = created.Clone();
                updated.Text = "updated";
                await context.UpdateActivityAsync(updated, cancellationToken);
            })
            .Send("Start")
            .AssertReply(async (updated) =>
            {
                var activities = await transcriptStore.GetTranscriptActivitiesAsync("test", conversationId);

                Assert.Equal("updated", updated.Text);
                Assert.Equal(2, activities.Items.Count);
                Assert.Equal("Start", activities.Items[0].Text);
                Assert.Equal("updated", activities.Items[1].Text);
            })
            .StartTestAsync();
        }

        [Fact]
        public async Task OnTurnAsync_ShouldExecuteOnDeleteActivityHandler()
        {
            var transcriptStore = new MemoryTranscriptStore();
            var sut = new TranscriptLoggerMiddleware(transcriptStore);

            var conversationId = Guid.NewGuid().ToString();
            var adapter = new TestAdapter(TestAdapter.CreateConversation(conversationId))
                .Use(sut);

            await new TestFlow(adapter, async (context, cancellationToken) =>
            {
                var created = new Activity
                {
                    Text = "created"
                };
                await context.SendActivityAsync(created, cancellationToken: cancellationToken);
                await context.DeleteActivityAsync(created.Id, cancellationToken);
                await context.SendActivityAsync("assert", cancellationToken: cancellationToken);
            })
            .Send("Start")
            .AssertReply(async (activity) =>
            {
                var activities = await transcriptStore.GetTranscriptActivitiesAsync("test", conversationId);

                Assert.Equal("assert", activity.Text);
                Assert.Equal(3, activities.Items.Count);
                Assert.Equal("Start", activities.Items[0].Text);
                Assert.Equal(ActivityTypes.MessageDelete, activities.Items[1].Type);
                Assert.Equal("created", activities.Items[1].Text);
            })
            .StartTestAsync();
        }

        [Fact]
        public async Task OnTurnAsync_ShouldGenerateActivityId()
        {
            var transcriptStore = new MemoryTranscriptStore();
            var sut = new TranscriptLoggerMiddleware(transcriptStore);

            var conversationId = Guid.NewGuid().ToString();
            var adapter = new TestAdapter(TestAdapter.CreateConversation(conversationId))
                .Use(sut);

            var activity = new Activity
            {
                ChannelId = "test",
                Text = "testing",
                Conversation = new ConversationAccount { Id = conversationId },
            };
            var turnContext = new TurnContext(adapter, activity);
            await sut.OnTurnAsync(turnContext, (_) => Task.CompletedTask, CancellationToken.None);

            var activities = await transcriptStore.GetTranscriptActivitiesAsync(activity.ChannelId, conversationId);

            Assert.Single(activities.Items);
            Assert.Equal(activity.Text, activities.Items[0].Text);
            Assert.NotEqual(activity.Id, activities.Items[0].Id);
        }
        */
    }
}
