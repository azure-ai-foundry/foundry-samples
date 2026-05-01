// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public class StreamingResponseTests
    {
        [Theory]
        [InlineData(Channels.Webchat, DeliveryModes.Normal, 600)]
        [InlineData(Channels.Msteams, DeliveryModes.Normal, 1200)]
        [InlineData(Channels.Test, DeliveryModes.Stream, 150)]
        public async Task TestStreamingResponseSuccess(string channelId, string deliveryMode, int delay)
        {
            var responses = new List<IActivity>();

            var adapter = new Mock<IChannelAdapter>();
            adapter
                .Setup(a => a.SendActivitiesAsync(It.IsAny<ITurnContext>(), It.IsAny<IActivity[]>(), It.IsAny<CancellationToken>()))
                .Callback<ITurnContext, IActivity[], CancellationToken>((context, activities, ct) =>
                {
                    foreach (var activity in activities)
                    {
                        responses.Add(activity);
                    }
                });

            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, DeliveryMode = deliveryMode, ChannelId = channelId });

            context.StreamingResponse.QueueTextChunk("this");
            await Task.Delay(delay);
            context.StreamingResponse.QueueTextChunk(" is a ");
            await Task.Delay(delay * 2);
            context.StreamingResponse.QueueTextChunk("test");

            var result = await context.StreamingResponse.EndStreamAsync();

            Assert.Equal(StreamingResponseResult.Success, result);
            Assert.True(responses.Count > 1);

            AssertTyping(responses.First());
            AssertFinal(responses.Last(), "this is a test");
        }

        [Theory]
        [InlineData(Channels.Test, DeliveryModes.Normal)]
        [InlineData(Channels.Webchat, DeliveryModes.ExpectReplies)]
        [InlineData(Channels.Msteams, DeliveryModes.ExpectReplies)]
        public async Task TestNonStreamingResponseSuccess(string channelId, string deliveryMode)
        {
            var responses = new List<IActivity>();

            var adapter = new Mock<IChannelAdapter>();
            adapter
                .Setup(a => a.SendActivitiesAsync(It.IsAny<ITurnContext>(), It.IsAny<IActivity[]>(), It.IsAny<CancellationToken>()))
                .Callback<ITurnContext, IActivity[], CancellationToken>((context, activities, ct) =>
                {
                    foreach (var activity in activities)
                    {
                        responses.Add(activity);
                    }
                });

            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, DeliveryMode = deliveryMode, ChannelId = channelId });

            context.StreamingResponse.QueueTextChunk("this");
            await Task.Delay(600);
            context.StreamingResponse.QueueTextChunk(" is a ");
            await Task.Delay(600);
            context.StreamingResponse.QueueTextChunk("test");

            var result = await context.StreamingResponse.EndStreamAsync();

            // We expect a single Activity, non-Streaming, with the complete Text value
            Assert.Equal(StreamingResponseResult.Success, result);
            Assert.Single(responses);
            Assert.Equal("this is a test", responses[0].Text);
            Assert.Null(responses[0].GetStreamingEntity());
        }

        [Fact]
        public async Task TestStreamingResponseUserCancel()
        {
            var responses = new List<IActivity>();

            var adapter = new Mock<IChannelAdapter>();
            adapter
                .Setup(a => a.SendActivitiesAsync(It.IsAny<ITurnContext>(), It.IsAny<IActivity[]>(), It.IsAny<CancellationToken>()))
                .Callback<ITurnContext, IActivity[], CancellationToken>((context, activities, ct) =>
                {
                    foreach (var activity in activities)
                    {
                        responses.Add(activity);
                    }

                    if (responses.Count == 2)
                    {
                        throw new ErrorResponseException("user cancelled")
                        {
                            Body = new ErrorResponse(new Error()
                            {
                                Code = "ContentStreamNotAllowed"
                            })
                        };
                    }
                });

            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Channels.Webchat });

            context.StreamingResponse.QueueTextChunk("this");
            await Task.Delay(600);
            context.StreamingResponse.QueueTextChunk(" is a ");
            await Task.Delay(600);
            context.StreamingResponse.QueueTextChunk("test");

            var result = await context.StreamingResponse.EndStreamAsync();

            Assert.Equal(StreamingResponseResult.UserCancelled, result);
            Assert.Equal(2, responses.Count);
            AssertTyping(responses[0]);
            AssertTyping(responses[1]);
        }

        [Fact]
        public async Task TestStreamingResponseStreamingFallback()
        {
            var responses = new List<IActivity>();

            var adapter = new Mock<IChannelAdapter>();
            adapter
                .Setup(a => a.SendActivitiesAsync(It.IsAny<ITurnContext>(), It.IsAny<IActivity[]>(), It.IsAny<CancellationToken>()))
                .Callback<ITurnContext, IActivity[], CancellationToken>((context, activities, ct) =>
                {
                    var entity = activities[0].GetStreamingEntity();
                    if (entity == null)
                    {
                        responses.Add(activities[0]);
                        return;
                    }

                    throw new ErrorResponseException("fallback")
                    {
                        Body = new ErrorResponse(new Error()
                        {
                            Code = "BadArgument",
                            Message = "streaming api is not enabled"
                        })
                    };
                });

            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Channels.Webchat });

            context.StreamingResponse.QueueTextChunk("this");
            await Task.Delay(600);
            context.StreamingResponse.QueueTextChunk(" is a ");
            await Task.Delay(600);
            context.StreamingResponse.QueueTextChunk("test");

            var result = await context.StreamingResponse.EndStreamAsync();

            // We expect a single Activity, non-Streaming, with the complete Text value
            Assert.Equal(StreamingResponseResult.Success, result);
            Assert.Single(responses);
            Assert.Equal("this is a test", responses[0].Text);
            Assert.Null(responses[0].GetStreamingEntity()); 
        }

        [Fact]
        public async Task TestStreamingResponseEndStreamTwice()
        {
            // streaming
            var context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Channels.Webchat });
            context.StreamingResponse.QueueTextChunk("chunk");
            var result = await context.StreamingResponse.EndStreamAsync();
            Assert.Equal(StreamingResponseResult.Success, result);
            // call end again
            result = await context.StreamingResponse.EndStreamAsync();
            Assert.Equal(StreamingResponseResult.AlreadyEnded, result);

            // non-streaming
            context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Channels.Test });
            context.StreamingResponse.QueueTextChunk("chunk");
            result = await context.StreamingResponse.EndStreamAsync();
            Assert.Equal(StreamingResponseResult.Success, result);
            // call end again
            result = await context.StreamingResponse.EndStreamAsync();
            Assert.Equal(StreamingResponseResult.AlreadyEnded, result);
        }

        [Fact]
        public async Task QueueInformativeUpdate_OnStreamingChannel_SendsInformativeActivity()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Channels.Webchat });

            await context.StreamingResponse.QueueInformativeUpdateAsync("Thinking...");
            await Task.Delay(600);
            context.StreamingResponse.QueueTextChunk("done");

            await context.StreamingResponse.EndStreamAsync();

            var informativeActivity = responses.FirstOrDefault(a => a.GetStreamingEntity()?.StreamType == StreamTypes.Informative);
            Assert.NotNull(informativeActivity);
            Assert.Equal("Thinking...", informativeActivity.Text);
            Assert.Equal(ActivityTypes.Typing, informativeActivity.Type);
        }

        [Fact]
        public async Task QueueInformativeUpdate_OnStreamingChannel_SendsMultipleInformativeActivity()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Channels.Webchat });
            int delay = 550;

            await context.StreamingResponse.QueueInformativeUpdateAsync("Thinking...");
            await Task.Delay(delay);
            context.StreamingResponse.QueueTextChunk("this");
            await Task.Delay(delay);
            context.StreamingResponse.QueueTextChunk(" is a ");
            await context.StreamingResponse.QueueInformativeUpdateAsync("Still thinking...");
            await Task.Delay(delay);
            context.StreamingResponse.QueueTextChunk("longer ");
            await Task.Delay(delay);
            context.StreamingResponse.QueueTextChunk("test");

            await context.StreamingResponse.EndStreamAsync();

            Assert.Equal(7, responses.Count); // 2 informative + 4 text chunks + 1 final message
            
            Assert.True(responses[0].GetStreamingEntity()?.StreamType == StreamTypes.Informative);
            Assert.Equal("Thinking...", responses[0].Text);
            Assert.Equal(ActivityTypes.Typing, responses[0].Type);

            Assert.True(responses[1].GetStreamingEntity()?.StreamType == StreamTypes.Streaming);
            Assert.Equal("this", responses[1].Text);
            Assert.Equal(ActivityTypes.Typing, responses[1].Type);

            Assert.True(responses[2].GetStreamingEntity()?.StreamType == StreamTypes.Informative);
            Assert.Equal("Still thinking...", responses[2].Text);
            Assert.Equal(ActivityTypes.Typing, responses[2].Type);

            Assert.True(responses[3].GetStreamingEntity()?.StreamType == StreamTypes.Streaming);
            Assert.Equal("this is a ", responses[3].Text);
            Assert.Equal(ActivityTypes.Typing, responses[3].Type);

            Assert.True(responses[4].GetStreamingEntity()?.StreamType == StreamTypes.Streaming);
            Assert.Equal("this is a longer ", responses[4].Text);
            Assert.Equal(ActivityTypes.Typing, responses[4].Type);

            Assert.True(responses[5].GetStreamingEntity()?.StreamType == StreamTypes.Streaming);
            Assert.Equal("this is a longer test", responses[5].Text);
            Assert.Equal(ActivityTypes.Typing, responses[5].Type);

            Assert.True(responses[6].GetStreamingEntity()?.StreamType == StreamTypes.Final);
            Assert.Equal("this is a longer test", responses[6].Text);
            Assert.Equal(ActivityTypes.Message, responses[6].Type);
        }

        [Fact]
        public async Task QueueInformativeUpdate_OnNonStreamingChannel_DoesNotSendActivity()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Channels.Test });

            await context.StreamingResponse.QueueInformativeUpdateAsync("Thinking...");
            context.StreamingResponse.QueueTextChunk("hello");

            await context.StreamingResponse.EndStreamAsync();

            // Non-streaming: no informative activities, only the final message
            Assert.Single(responses);
            Assert.Equal(ActivityTypes.Message, responses[0].Type);
        }

        [Fact]
        public async Task QueueInformativeUpdate_AfterStreamEnded_ThrowsInvalidOperationException()
        {
            var context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Channels.Webchat });
            context.StreamingResponse.QueueTextChunk("chunk");
            await context.StreamingResponse.EndStreamAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                context.StreamingResponse.QueueInformativeUpdateAsync("too late"));
        }

        [Fact]
        public async Task QueueTextChunk_AfterStreamEnded_ThrowsInvalidOperationException()
        {
            var context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Channels.Webchat });
            context.StreamingResponse.QueueTextChunk("chunk");
            await context.StreamingResponse.EndStreamAsync();

            Assert.Throws<InvalidOperationException>(() =>
                context.StreamingResponse.QueueTextChunk("too late"));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void QueueTextChunk_WithEmptyOrNullText_SilentlyIgnored(string text)
        {
            var context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Channels.Webchat });

            // Should not throw
            context.StreamingResponse.QueueTextChunk(text);
            Assert.Equal("", context.StreamingResponse.Message);
        }

        [Fact]
        public void AddCitation_WithCitationAndPosition_AddsCitationAtPosition()
        {
            var context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Channels.Webchat });
            var citation = new Citation("Some content", "My Doc", "https://example.com");

            context.StreamingResponse.AddCitation(citation, 1);

            Assert.Single(context.StreamingResponse.Citations);
            Assert.Equal(1, context.StreamingResponse.Citations[0].Position);
            Assert.Equal("My Doc", context.StreamingResponse.Citations[0].Appearance.Name);
            Assert.Equal("https://example.com", context.StreamingResponse.Citations[0].Appearance.Url);
        }

        [Fact]
        public void AddCitations_WithCitationList_AddsAllCitationsInOrder()
        {
            var context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Channels.Webchat });
            var citations = new List<Citation>
            {
                new("Content 1", "Doc 1", "https://example.com/1"),
                new("Content 2", "Doc 2", "https://example.com/2"),
            };

            context.StreamingResponse.AddCitations(citations);

            Assert.Equal(2, context.StreamingResponse.Citations.Count);
            Assert.Equal(1, context.StreamingResponse.Citations[0].Position);
            Assert.Equal(2, context.StreamingResponse.Citations[1].Position);
        }

        [Fact]
        public void AddCitation_WithClientCitation_AddedToList()
        {
            var context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Channels.Webchat });
            var citation = new ClientCitation { Position = 5, Appearance = new ClientCitationAppearance { Name = "Test" } };

            context.StreamingResponse.AddCitation(citation);

            Assert.Single(context.StreamingResponse.Citations);
            Assert.Equal(5, context.StreamingResponse.Citations[0].Position);
        }

        [Fact]
        public void AddCitations_WithClientCitationList_AddsAll()
        {
            var context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Channels.Webchat });
            var citations = new List<ClientCitation>
            {
                new() { Position = 1 },
                new() { Position = 2 },
                new() { Position = 3 },
            };

            context.StreamingResponse.AddCitations(citations);

            Assert.Equal(3, context.StreamingResponse.Citations.Count);
        }

        [Fact]
        public async Task EnableGeneratedByAILabel_True_AddsAIGeneratedContentToFinalMessage()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Channels.Test });
            context.StreamingResponse.EnableGeneratedByAILabel = true;

            context.StreamingResponse.QueueTextChunk("hello");
            await context.StreamingResponse.EndStreamAsync();

            var finalActivity = responses.Last();
            var aiEntity = finalActivity.Entities?.OfType<AIEntity>().FirstOrDefault();
            Assert.NotNull(aiEntity);
            Assert.Contains(AIEntity.AdditionalTypeAIGeneratedContent, aiEntity.AdditionalType);
        }

        [Fact]
        public async Task Citations_ReferencedInText_IncludedInFinalMessageAIEntity()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Channels.Test });
            context.StreamingResponse.AddCitation(new ClientCitation { Position = 1, Appearance = new ClientCitationAppearance { Name = "Ref Doc" } });

            context.StreamingResponse.QueueTextChunk("See [1] for details.");
            await context.StreamingResponse.EndStreamAsync();

            var finalActivity = responses.Last();
            var aiEntity = finalActivity.Entities?.OfType<AIEntity>().FirstOrDefault();
            Assert.NotNull(aiEntity);
            Assert.NotNull(aiEntity.Citation);
            Assert.Contains(aiEntity.Citation, c => c.Position == 1);
        }

        [Fact]
        public async Task FinalMessage_CustomActivity_IsUsedAsBase()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Channels.Test });

            var customFinal = new Activity { Text = "Custom final text" };
            context.StreamingResponse.FinalMessage = customFinal;
            context.StreamingResponse.QueueTextChunk("ignored text");

            await context.StreamingResponse.EndStreamAsync();

            var finalActivity = responses.Last();
            Assert.Equal("Custom final text", finalActivity.Text);
        }

        [Fact]
        public async Task SensitivityLabel_SetOnResponse_IncludedInFinalMessageAIEntity()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Channels.Test });
            context.StreamingResponse.EnableGeneratedByAILabel = true;
            context.StreamingResponse.SensitivityLabel = new SensitivityUsageInfo { Name = "Confidential" };

            context.StreamingResponse.QueueTextChunk("hello");
            await context.StreamingResponse.EndStreamAsync();

            var finalActivity = responses.Last();
            var aiEntity = finalActivity.Entities?.OfType<AIEntity>().FirstOrDefault();
            Assert.NotNull(aiEntity);
            Assert.NotNull(aiEntity.UsageInfo);
            Assert.Equal("Confidential", aiEntity.UsageInfo.Name);
        }

        [Fact]
        public async Task EndStreamAsync_OnStreamingChannel_WithNothingQueued_ReturnsNotStarted()
        {
            var context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Channels.Webchat });

            var result = await context.StreamingResponse.EndStreamAsync();

            Assert.Equal(StreamingResponseResult.NotStarted, result);
        }

        [Fact]
        public async Task EndStreamAsync_OnNonStreamingChannel_WithNothingQueued_SendsNoActivity()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Channels.Test });

            var result = await context.StreamingResponse.EndStreamAsync();

            Assert.Equal(StreamingResponseResult.Success, result);
            Assert.Empty(responses);
        }


        [Fact]
        public async Task ResetAsync_AfterStream_AllowsStreamToBeReused()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Channels.Test });

            context.StreamingResponse.QueueTextChunk("first");
            await context.StreamingResponse.EndStreamAsync();

            await context.StreamingResponse.ResetAsync();

            context.StreamingResponse.QueueTextChunk("second");
            var result = await context.StreamingResponse.EndStreamAsync();

            Assert.Equal(StreamingResponseResult.Success, result);
            Assert.Equal(2, responses.Count);
            Assert.Equal("first", responses[0].Text);
            Assert.Equal("second", responses[1].Text);
        }

        [Fact]
        public void QueueTextChunk_AccumulatesMessageText()
        {
            var context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Channels.Test });

            context.StreamingResponse.QueueTextChunk("Hello");
            context.StreamingResponse.QueueTextChunk(", ");
            context.StreamingResponse.QueueTextChunk("World");

            Assert.Equal("Hello, World", context.StreamingResponse.Message);
        }

        [Fact]
        public async Task FeedbackLoopEnabled_OnTeamsChannel_AddsFeedbackLoopToFinalMessage()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            // ExpectReplies keeps _isTeamsChannel=true but IsStreamingChannel=false (no timing delays needed)
            var context = new TurnContext(adapter.Object, new Activity()
            {
                Type = ActivityTypes.Message,
                DeliveryMode = DeliveryModes.ExpectReplies,
                ChannelId = Channels.Msteams
            });
            context.StreamingResponse.FeedbackLoopEnabled = true;

            context.StreamingResponse.QueueTextChunk("hello");
            await context.StreamingResponse.EndStreamAsync();

            var finalActivity = responses.Last();
            Assert.NotNull(finalActivity.ChannelData);
            var json = JsonSerializer.Serialize(finalActivity.ChannelData);
            using var doc = JsonDocument.Parse(json);
            Assert.Equal("default", doc.RootElement.GetProperty("feedbackLoop").GetProperty("type").GetString());
        }

        [Fact]
        public async Task FeedbackLoopEnabled_WithCustomType_SetsFeedbackLoopTypeOnFinalMessage()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity()
            {
                Type = ActivityTypes.Message,
                DeliveryMode = DeliveryModes.ExpectReplies,
                ChannelId = Channels.Msteams
            });
            context.StreamingResponse.FeedbackLoopEnabled = true;
            context.StreamingResponse.FeedbackLoopType = "custom";

            context.StreamingResponse.QueueTextChunk("hello");
            await context.StreamingResponse.EndStreamAsync();

            var finalActivity = responses.Last();
            var json = JsonSerializer.Serialize(finalActivity.ChannelData);
            using var doc = JsonDocument.Parse(json);
            Assert.Equal("custom", doc.RootElement.GetProperty("feedbackLoop").GetProperty("type").GetString());
        }

        [Fact]
        public async Task FeedbackLoopEnabled_WithExistingChannelData_PreservesExistingProperties()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity()
            {
                Type = ActivityTypes.Message,
                DeliveryMode = DeliveryModes.ExpectReplies,
                ChannelId = Channels.Msteams
            });
            context.StreamingResponse.FeedbackLoopEnabled = true;
            context.StreamingResponse.FinalMessage = new Activity
            {
                ChannelData = new { existingKey = "existingValue" }
            };

            context.StreamingResponse.QueueTextChunk("hello");
            await context.StreamingResponse.EndStreamAsync();

            var finalActivity = responses.Last();
            Assert.NotNull(finalActivity.ChannelData);
            var json = JsonSerializer.Serialize(finalActivity.ChannelData);
            using var doc = JsonDocument.Parse(json);
            Assert.Equal("existingValue", doc.RootElement.GetProperty("existingKey").GetString());
            Assert.Equal("default", doc.RootElement.GetProperty("feedbackLoop").GetProperty("type").GetString());
        }

        [Fact]
        public async Task FeedbackLoopEnabled_OnNonTeamsChannel_DoesNotAddFeedbackLoop()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity()
            {
                Type = ActivityTypes.Message,
                ChannelId = Channels.Test
            });
            context.StreamingResponse.FeedbackLoopEnabled = true;

            context.StreamingResponse.QueueTextChunk("hello");
            await context.StreamingResponse.EndStreamAsync();

            var finalActivity = responses.Last();
            Assert.Null(finalActivity.ChannelData);
        }

        private static Mock<IChannelAdapter> CreateMockAdapter(List<IActivity> responses)
        {
            var adapter = new Mock<IChannelAdapter>();
            adapter
                .Setup(a => a.SendActivitiesAsync(It.IsAny<ITurnContext>(), It.IsAny<IActivity[]>(), It.IsAny<CancellationToken>()))
                .Callback<ITurnContext, IActivity[], CancellationToken>((_, activities, _) =>
                {
                    foreach (var activity in activities)
                    {
                        responses.Add(activity);
                    }
                });
            return adapter;
        }

        private static void AssertTyping(IActivity activity)
        {
            var streamingEntity = activity.GetStreamingEntity();
            Assert.NotNull(streamingEntity);
            Assert.Equal(StreamTypes.Streaming, streamingEntity.StreamType);
        }

        private static void AssertFinal(IActivity activity, string expectedText)
        {
            var streamingEntity = activity.GetStreamingEntity();
            Assert.NotNull(streamingEntity);
            Assert.Equal(StreamResults.Success, streamingEntity.StreamResult);
            Assert.Equal(expectedText, activity.Text);
        }
    }
}
