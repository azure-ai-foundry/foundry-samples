// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Core.Models;
using Moq;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public class TurnContextTests
    {
        [Fact]
        public void Constructor_ShouldThrowOnNullAdapter()
        {
            Assert.Throws<ArgumentNullException>(() => new TurnContext((ChannelAdapter)null, new Activity()));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullTurnContext()
        {
            Assert.Throws<ArgumentNullException>(() => new TurnContext((TurnContext)null, new Activity()));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullActivity()
        {
            var adapter = new TestAdapter(TestAdapter.CreateConversation("ConstructorNullActivity"));
            Assert.Throws<ArgumentNullException>(() => new TurnContext(adapter, null));
        }

        [Fact]
        public void Constructor_ShouldInstantiateCorrectly()
        {
            var context = new TurnContext(new TestAdapter(TestAdapter.CreateConversation("Constructor")), new Activity());
            Assert.NotNull(context);
        }

        [Fact]
        public void TurnContext_ShouldBeClonedCorrectly()
        {
            var context1 = new TurnContext(new SimpleAdapter(), new Activity() { Text = "one" });
            context1.StackState.Set("x", "test");
            context1.OnSendActivities((context, activities, next) => next());
            context1.OnDeleteActivity((context, activity, next) => next());
            context1.OnUpdateActivity((context, activity, next) => next());
            var ccontext2 = new TurnContext(context1, new Activity() { Text = "two" });
            Assert.Equal("one", context1.Activity.Text);
            Assert.Equal("two", ccontext2.Activity.Text);
            Assert.Equal(context1.Adapter, ccontext2.Adapter);
            Assert.Equal(context1.StackState, ccontext2.StackState);

            var binding = BindingFlags.Instance | BindingFlags.NonPublic;
            var onSendField = typeof(TurnContext).GetField("_onSendActivities", binding);
            var onDeleteField = typeof(TurnContext).GetField("_onDeleteActivity", binding);
            var onUpdateField = typeof(TurnContext).GetField("_onUpdateActivity", binding);
            Assert.Equal(onSendField.GetValue(context1), onSendField.GetValue(ccontext2));
            Assert.Equal(onDeleteField.GetValue(context1), onDeleteField.GetValue(ccontext2));
            Assert.Equal(onUpdateField.GetValue(context1), onUpdateField.GetValue(ccontext2));
        }

        [Fact]
        public void Responded_ShouldBeSetAsFalse()
        {
            var context = new TurnContext(new TestAdapter(TestAdapter.CreateConversation("RespondedIsFalse")), new Activity());
            Assert.False(context.Responded);
        }

        [Fact]
        public async Task Responded_ShouldBeTrueAfterReplyingToActivity()
        {
            var adapter = new TestAdapter(TestAdapter.CreateConversation("CacheValueUsingSetAndGet"));
            await new TestFlow(adapter, MyBotLogic)
                    .Send("TestResponded")
                    .StartTestAsync();
        }

        [Fact]
        public void Get_ThrowsOnNullKey()
        {
            var context = new TurnContext(new SimpleAdapter(), new Activity());
            Assert.Throws<ArgumentNullException>(() => context.StackState.Get<object>(null));
        }

        [Fact]
        public void Get_ShouldReturnNullOnEmptyKey()
        {
            var context = new TurnContext(new SimpleAdapter(), new Activity());
            var service = context.StackState.Get<object>(string.Empty);
            Assert.Null(service);
        }

        [Fact]
        public void Get_ShouldReturnNullWithUnknownKey()
        {
            var context = new TurnContext(new SimpleAdapter(), new Activity());
            var result = context.StackState.Get<object>("test");
            Assert.Null(result);
        }

        [Fact]
        public void Get_ShouldReturnCachedValueUsingKeyName()
        {
            var context = new TurnContext(new SimpleAdapter(), new Activity());

            context.StackState.Set("bar", "foo");
            var result = context.StackState.Get<string>("bar");

            Assert.Equal("foo", result);
        }

        [Fact]
        public void Get_ShouldReturnGenericValuegUsingTypeAsKeyName()
        {
            var context = new TurnContext(new SimpleAdapter(), new Activity());

            context.StackState.Set("foo");
            string result = context.StackState.Get<string>();

            Assert.Equal("foo", result);
        }

        [Fact]
        public void Activity_ShouldBeCorrectlySet()
        {
            var context = new TurnContext(new SimpleAdapter(), TestMessage.Message());
            Assert.Equal("1234", context.Activity.Id);
        }

        [Fact]
        public async Task SendActivityAsync_ShouldSetRespondedAsTrue()
        {
            var adapter = new SimpleAdapter();
            var context = new TurnContext(adapter, new Activity());
            Assert.False(context.Responded);
            var response = await context.SendActivityAsync(TestMessage.Message("testtest"));

            Assert.True(context.Responded);
            Assert.Equal("testtest", response.Id);
        }

        [Fact]
        public async Task SendActivityAsync_ShouldSendBatchOfActivities()
        {
            var adapter = new SimpleAdapter();
            var context = new TurnContext(adapter, new Activity());
            Assert.False(context.Responded);

            var message1 = TestMessage.Message("message1");
            var message2 = TestMessage.Message("message2");

            var response = await context.SendActivitiesAsync([message1, message2]);

            Assert.True(context.Responded);
            Assert.Equal(2, response.Length);
            Assert.Equal("message1", response[0].Id);
            Assert.Equal("message2", response[1].Id);
        }

        [Fact]
        public async Task SendActivityAsync_ShouldSetRespondedUsingIMessageActivity()
        {
            var adapter = new SimpleAdapter();
            var context = new TurnContext(adapter, new Activity());
            Assert.False(context.Responded);

            var msg = Activity.CreateMessageActivity();
            await context.SendActivityAsync(msg);
            Assert.True(context.Responded);
        }

        [Fact]
        public async Task SendActivityAsync_ShouldNotSetRespondedOnTraceActivity()
        {
            var adapter = new SimpleAdapter();
            var context = new TurnContext(adapter, new Activity());
            Assert.False(context.Responded);

            // Send a Trace Activity, and make sure responded is NOT set.
            var trace = Activity.CreateTraceActivity("trace");
            await context.SendActivityAsync(trace);
            Assert.False(context.Responded);

            // Just to sanity check everything, send a Message and verify the
            // responded flag IS set.
            var msg = Activity.CreateMessageActivity();
            await context.SendActivityAsync(msg);
            Assert.True(context.Responded);
        }

        [Fact]
        public async Task SendActivityAsync_ShouldSendOneActivityThroughAdapter()
        {
            bool foundActivity = false;

            void ValidateResponses(IActivity[] activities)
            {
                Assert.True(activities.Length == 1, "Incorrect Count");
                Assert.Equal("1234", activities[0].Id);
                foundActivity = true;
            }

            var adapter = new SimpleAdapter(ValidateResponses);
            var context = new TurnContext(adapter, new Activity());
            await context.SendActivityAsync(TestMessage.Message());
            Assert.True(foundActivity);
        }

        [Fact]
        public async Task SendActivityAsync_ShouldCallOnSendBeforeDelivery()
        {
            var adapter = new SimpleAdapter();
            var context = new TurnContext(adapter, new Activity());

            int count = 0;
            context.OnSendActivities(async (context, activities, next) =>
            {
                Assert.NotNull(activities); // Null Array passed in
                count = activities.Count;
                return await next();
            });

            await context.SendActivityAsync(TestMessage.Message());

            Assert.Equal(1, count);
        }

        [Fact]
        public async Task SendActivityAsync_ShouldAllowInterceptionOfDeliveryOnSend()
        {
            bool responsesSent = false;
            void ValidateResponses(IActivity[] activities)
            {
                responsesSent = true;
                Assert.Fail("ValidateResponses should not be called. Interceptor did not work.");
            }

            var adapter = new SimpleAdapter(ValidateResponses);
            var context = new TurnContext(adapter, new Activity());

            int count = 0;
            context.OnSendActivities((context, activities, next) =>
            {
                Assert.NotNull(activities);
                count = activities.Count;

                // Do not call next.
                return Task.FromResult<ResourceResponse[]>(null);
            });

            await context.SendActivityAsync(TestMessage.Message());

            Assert.Equal(1, count);
            Assert.False(responsesSent, "Responses made it to the adapter.");
        }

        [Fact]
        public async Task SendActivityAsync_ShouldInterceptAndMutateOnSend()
        {
            bool foundIt = false;
            void ValidateResponses(IActivity[] activities)
            {
                Assert.NotNull(activities);
                Assert.Single(activities);
                Assert.Equal("changed", activities[0].Id);
                foundIt = true;
            }

            var adapter = new SimpleAdapter(ValidateResponses);
            var context = new TurnContext(adapter, new Activity());

            context.OnSendActivities(async (context, activities, next) =>
            {
                Assert.NotNull(activities); // Null Array passed in
                Assert.Single(activities);
                Assert.True(activities[0].Id == "1234", "Unknown Id Passed In");
                activities[0].Id = "changed";
                return await next();
            });

            await context.SendActivityAsync(TestMessage.Message());

            // Intercepted the message, changed it, and sent it on to the Adapter
            Assert.True(foundIt);
        }

        [Fact]
        public async Task UpdateActivityAsync_ShouldUpdateOneActivityToAdapter()
        {
            bool foundActivity = false;

            void ValidateUpdate(IActivity activity)
            {
                Assert.NotNull(activity);
                Assert.Equal("test", activity.Id);
                foundActivity = true;
            }

            var adapter = new SimpleAdapter(ValidateUpdate);
            var context = new TurnContext(adapter, new Activity());

            var message = TestMessage.Message("test");
            var updateResult = await context.UpdateActivityAsync(message);

            Assert.True(foundActivity);
            Assert.Equal("test", updateResult.Id);
        }

        [Fact]
        public async Task UpdateActivityAsync_ShouldWorkWithMessageFactory()
        {
            const string ACTIVITY_ID = "activity ID";
            const string CONVERSATION_ID = "conversation ID";

            var foundActivity = false;

            void ValidateUpdate(IActivity activity)
            {
                Assert.NotNull(activity);
                Assert.Equal(ACTIVITY_ID, activity.Id);
                Assert.Equal(CONVERSATION_ID, activity.Conversation.Id);
                foundActivity = true;
            }

            var adapter = new SimpleAdapter(ValidateUpdate);
            var context = new TurnContext(adapter, new Activity(conversation: new ConversationAccount(id: CONVERSATION_ID)));

            var message = MessageFactory.Text("test text");

            message.Id = ACTIVITY_ID;

            var updateResult = await context.UpdateActivityAsync(message);

            Assert.True(foundActivity);
            Assert.Equal(ACTIVITY_ID, updateResult.Id);

            context.Dispose();
        }

        [Fact]
        public async Task UpdateActivityAsync_ShouldCallOnUpdateBeforeDelivery()
        {
            bool foundActivity = false;

            void ValidateUpdate(IActivity activity)
            {
                Assert.NotNull(activity);
                Assert.Equal("1234", activity.Id);
                foundActivity = true;
            }

            SimpleAdapter adapter = new SimpleAdapter(ValidateUpdate);
            TurnContext context = new TurnContext(adapter, new Activity());

            bool wasCalled = false;
            context.OnUpdateActivity(async (context, activity, next) =>
            {
                Assert.NotNull(activity);
                wasCalled = true;
                return await next();
            });
            await context.UpdateActivityAsync(TestMessage.Message());
            Assert.True(wasCalled);
            Assert.True(foundActivity);
        }

        [Fact]
        public async Task UpdateActivityAsync_ShouldInterceptOnUpdate()
        {
            bool adapterCalled = false;
            void ValidateUpdate(IActivity activity)
            {
                adapterCalled = true;
                Assert.Fail("ValidateUpdate should not be called. Interceptor did not work.");
            }

            var adapter = new SimpleAdapter(ValidateUpdate);
            var context = new TurnContext(adapter, new Activity());

            bool wasCalled = false;
            context.OnUpdateActivity((context, activity, next) =>
            {
                Assert.NotNull(activity);
                wasCalled = true;

                // Do Not Call Next
                return Task.FromResult<ResourceResponse>(null);
            });

            await context.UpdateActivityAsync(TestMessage.Message());
            Assert.True(wasCalled); // Interceptor was called
            Assert.False(adapterCalled); // Adapter was not
        }

        [Fact]
        public async Task UpdateActivityAsync_ShouldInterceptAndMutateOnUpdate()
        {
            bool adapterCalled = false;
            void ValidateUpdate(IActivity activity)
            {
                Assert.Equal("mutated", activity.Id);
                adapterCalled = true;
            }

            var adapter = new SimpleAdapter(ValidateUpdate);
            var context = new TurnContext(adapter, new Activity());

            context.OnUpdateActivity(async (context, activity, next) =>
            {
                Assert.NotNull(activity);
                Assert.Equal("1234", activity.Id);
                activity.Id = "mutated";
                return await next();
            });

            await context.UpdateActivityAsync(TestMessage.Message());
            Assert.True(adapterCalled); // Adapter was called
        }

        [Fact]
        public async Task DeleteActivityAsync_ShouldDeleteOneActivityThroughAdapter()
        {
            bool deleteCalled = false;

            void ValidateDelete(ConversationReference reference)
            {
                Assert.NotNull(reference);
                Assert.Equal("12345", reference.ActivityId);
                deleteCalled = true;
            }

            var adapter = new SimpleAdapter(ValidateDelete);
            var context = new TurnContext(adapter, TestMessage.Message());
            await context.DeleteActivityAsync("12345");
            Assert.True(deleteCalled);
        }

        [Fact]
        public async Task DeleteActivityAsync_ShouldDeleteConversationReferenceThroughAdapter()
        {
            bool deleteCalled = false;

            void ValidateDelete(ConversationReference reference)
            {
                Assert.NotNull(reference);
                Assert.Equal("12345", reference.ActivityId);
                deleteCalled = true;
            }

            var adapter = new SimpleAdapter(ValidateDelete);
            var context = new TurnContext(adapter, TestMessage.Message());

            var reference = new ConversationReference("12345");

            await context.DeleteActivityAsync(reference);
            Assert.True(deleteCalled);
        }

        [Fact]
        public async Task DeleteActivityAsync_ShouldInterceptOnDelete()
        {
            bool adapterCalled = false;

            void ValidateDelete(ConversationReference reference)
            {
                adapterCalled = true;
                Assert.Fail("ValidateDelete should not be called. Interceptor did not work.");
            }

            var adapter = new SimpleAdapter(ValidateDelete);
            var context = new TurnContext(adapter, new Activity());

            bool wasCalled = false;
            context.OnDeleteActivity((context, convRef, next) =>
            {
                Assert.NotNull(convRef);
                wasCalled = true;

                // Do Not Call Next
                return Task.FromResult<ResourceResponse[]>(null);
            });

            await context.DeleteActivityAsync("1234");
            Assert.True(wasCalled); // Interceptor was called
            Assert.False(adapterCalled); // Adapter was not
        }

        [Fact]
        public async Task DeleteActivityAsync_ShouldInterceptAndMutateOnDelete()
        {
            bool adapterCalled = false;

            void ValidateDelete(ConversationReference reference)
            {
                Assert.Equal("mutated", reference.ActivityId);
                adapterCalled = true;
            }

            var adapter = new SimpleAdapter(ValidateDelete);
            var context = new TurnContext(adapter, new Activity());

            context.OnDeleteActivity(async (context, convRef, next) =>
            {
                Assert.NotNull(convRef);
                Assert.True(convRef.ActivityId == "1234", "Incorrect Activity Id");
                convRef.ActivityId = "mutated";
                await next();
            });

            await context.DeleteActivityAsync("1234");
            Assert.True(adapterCalled); // Adapter was called + validated the change
        }

        [Fact]
        public async Task SendActivityAsync_ShouldThrowExceptionInOnSend()
        {
            var adapter = new SimpleAdapter();
            var context = new TurnContext(adapter, new Activity());

            context.OnSendActivities((context, activities, next) =>
            {
                throw new Exception("test");
            });

            await Assert.ThrowsAsync<Exception>(() => context.SendActivityAsync(TestMessage.Message()));
        }

        [Fact]
        public async Task SendActivityAsync_ShouldCreateActivityWithParameters()
        {
            var adapter = new SimpleAdapter();
            var context = new TurnContext(adapter, new Activity());
            Assert.False(context.Responded);
            var textMessage = "test activity created with parameters";
            await context.SendActivityAsync(textMessage, textMessage, InputHints.AcceptingInput);

            Assert.True(context.Responded);
        }

        [Fact]
        public async Task SendActivitiesAsync_ShouldThrowExceptionOnEmptyActivitiesList()
        {
            var adapter = new SimpleAdapter();
            var context = new TurnContext(adapter, new Activity());
           
            await Assert.ThrowsAsync<ArgumentException>(async () => await context.SendActivitiesAsync([]));
        }

        /*
        [Fact]
        public async Task SendActivitiesAsync_ShouldSendActivitiesWithExpectRepliesMode()
        {
            var adapter = new SimpleAdapter();
            var context = new TurnContext(adapter, new Activity() { DeliveryMode = DeliveryModes.ExpectReplies });
            var message = new Activity
            {
                Type = ActivityTypes.InvokeResponse,
                Id = "expect replies message",
                Text = "test",
            };

            await context.SendActivitiesAsync([message]);

            Assert.True(context.Responded);
            Assert.NotEmpty(context.BufferedReplyActivities);
            Assert.Equal("expect replies message", context.BufferedReplyActivities[0].Id);
        }
        */

        [Fact]
        public async Task TraceActivityAsync_ShouldNotUpdateContextReponded()
        {
            var adapter = new SimpleAdapter();
            var context = new TurnContext(adapter, TestMessage.Message());

            await context.TraceActivityAsync("Message with trace");

            Assert.False(context.Responded);
        }

        [Fact]
        public void TraceActivityAsync_ShouldNotUpdateContextResponseOnMultipleDispose()
        {
            // Arrange
            var adapter = new SimpleAdapter();
            var activity = TestMessage.Message();

            // Create a custom disposable TurnContextStateCollection
            var stackState = new CustomDisposableTurnContextStateCollection();
            var services = new CustomDisposableTurnContextStateCollection();
            var mockTurnContext = new Mock<ITurnContext>();
            mockTurnContext.Setup(tc => tc.Adapter).Returns(adapter);
            mockTurnContext.Setup(tc => tc.StackState).Returns(stackState);
            mockTurnContext.Setup(tc => tc.Services).Returns(services);

            var context = new TurnContext(mockTurnContext.Object, activity);

            // Act
            context.Dispose();
            context.Dispose();

            // Assert
            Assert.True(stackState.IsDisposed, "StackState.Dispose was not called.");
            Assert.Equal(1, stackState.DisposeCallCount);
            Assert.True(services.IsDisposed, "Services.Dispose was not called.");
            Assert.Equal(1, services.DisposeCallCount);
        }

        private class CustomDisposableTurnContextStateCollection : TurnContextStateCollection, IDisposable
        {
            public bool IsDisposed { get; private set; }
            public int DisposeCallCount { get; private set; }

            protected override void Dispose(bool disposing)
            {
                IsDisposed = true;
                DisposeCallCount++;
            }
        }

        [Fact]
        public async Task SendActivityAsync_ShouldThrowAfterDispose()
        {
            var adapter = new SimpleAdapter();
            var context = new TurnContext(adapter, new Activity());
            context.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => context.SendActivityAsync("hello"));
        }

        [Fact]
        public async Task UpdateActivityAsync_ShouldThrowAfterDispose()
        {
            var adapter = new SimpleAdapter();
            var context = new TurnContext(adapter, new Activity());
            context.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => context.UpdateActivityAsync(new Activity()));
        }

        [Fact]
        public async Task DeleteActivityAsync_ShouldThrowAfterDispose()
        {
            var adapter = new SimpleAdapter();
            var context = new TurnContext(adapter, new Activity());
            context.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => context.DeleteActivityAsync("some-activity-id"));
        }

        private async Task MyBotLogic(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.Text == "TestResponded")
            {
                Assert.False(turnContext.Responded, "Responded should be false before sending.");

                await turnContext.SendActivityAsync(turnContext.Activity.CreateReply("one"), cancellationToken);

                Assert.True(turnContext.Responded, "Responded should be true after sending.");
            }
            else
            {
                await turnContext.SendActivityAsync(
                    turnContext.Activity.CreateReply($"echo:{turnContext.Activity.Text}"), cancellationToken);
            }
        }
    }
}
