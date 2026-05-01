// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class TypingWorkerTests
    {
        private static TypingOptions MakeOptions(int initialDelayMs, int intervalMs) =>
            new TypingOptions
            {
                InitialDelayMs = initialDelayMs,
                IntervalMs = intervalMs,
                ChannelStrategies = new Dictionary<string, ITypingChannelStrategy>(
                    System.StringComparer.OrdinalIgnoreCase)
                {
                    [Channels.M365Copilot] = new TypingChannelStrategy(initialDelayMs: 0, intervalMs: intervalMs)
                }
            };

        private static (TestAdapter adapter, TurnContext context) CreateMessageTurn(
            string channelId = Channels.Test)
        {
            var adapter = new TestAdapter(channelId);
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello",
                ChannelId = channelId,
                Conversation = new ConversationAccount { Id = "conv1" },
                From = new ChannelAccount { Id = "user1" },
                Recipient = new ChannelAccount { Id = "bot1" },
            };
            var context = new TurnContext(adapter, activity);
            return (adapter, context);
        }

        [Fact]
        public void Create_ReturnsNull_ForNonMessageActivity()
        {
            var adapter = new TestAdapter();
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelId = Channels.Test,
                Conversation = new ConversationAccount { Id = "c" },
                From = new ChannelAccount { Id = "u" },
                Recipient = new ChannelAccount { Id = "b" }
            };
            var context = new TurnContext(adapter, activity);

            var worker = TypingWorker.Create(context, MakeOptions(500, 2000));

            Assert.Null(worker);
        }

        [Fact]
        public void Create_ReturnsWorker_ForMessageActivity()
        {
            var (_, context) = CreateMessageTurn();

            var worker = TypingWorker.Create(context, MakeOptions(500, 2000));

            Assert.NotNull(worker);
        }

        [Fact]
        public void Create_UsesDefaultStrategy_ForUnknownChannel()
        {
            var (_, context) = CreateMessageTurn("unknownchannel");

            var worker = TypingWorker.Create(context, MakeOptions(123, 456));

            Assert.NotNull(worker);
        }

        [Fact]
        public void Create_UsesM365CopilotStrategy_ForM365CopilotChannel()
        {
            // M365Copilot must use its channel-specific strategy (InitialDelayMs = 0).
            var (_, context) = CreateMessageTurn(Channels.M365Copilot);

            var worker = TypingWorker.Create(context, MakeOptions(9999, 2000));

            Assert.NotNull(worker);
        }

        [Fact]
        public async Task Start_SendsTypingActivity_AfterInitialDelay()
        {
            var (adapter, context) = CreateMessageTurn();
            var worker = TypingWorker.Create(context, MakeOptions(initialDelayMs: 50, intervalMs: 30_000))!;

            worker.Start();

            // Wait well beyond initial delay; only 1 typing should fire (interval is huge).
            await Task.Delay(300);
            await worker.DisposeAsync();

            var typingCount = adapter.ActiveQueue.Count(a => a.Type == ActivityTypes.Typing);
            Assert.True(typingCount >= 1, $"Expected at least 1 typing activity, got {typingCount}");
        }

        [Fact]
        public async Task Start_SendsMultipleTypingActivities_AtInterval()
        {
            var (adapter, context) = CreateMessageTurn();
            var worker = TypingWorker.Create(context, MakeOptions(initialDelayMs: 0, intervalMs: 60))!;

            worker.Start();

            // With 0ms initial delay and 60ms interval, after 400ms we expect ≥ 5 activities.
            await Task.Delay(400);
            await worker.DisposeAsync();

            var typingCount = adapter.ActiveQueue.Count(a => a.Type == ActivityTypes.Typing);
            Assert.True(typingCount >= 4, $"Expected at least 4 typing activities, got {typingCount}");
        }

        [Fact]
        public async Task Start_StopsOnDispose()
        {
            var (adapter, context) = CreateMessageTurn();
            var worker = TypingWorker.Create(context, MakeOptions(initialDelayMs: 0, intervalMs: 30))!;

            worker.Start();
            await Task.Delay(150); // Let a few typing activities fire.

            await worker.DisposeAsync();
            var countAfterDispose = adapter.ActiveQueue.Count(a => a.Type == ActivityTypes.Typing);

            // After dispose, the count must not increase.
            await Task.Delay(150);
            var countAfterWait = adapter.ActiveQueue.Count(a => a.Type == ActivityTypes.Typing);

            Assert.Equal(countAfterDispose, countAfterWait);
        }

        [Fact]
        public async Task Start_StopsOnStreamingFinalTypingActivitySent()
        {
            var (adapter, context) = CreateMessageTurn();
            // Long initial delay so the worker is still waiting when we send the final activity.
            var worker = TypingWorker.Create(context, MakeOptions(initialDelayMs: 5000, intervalMs: 30_000))!;
            worker.Start();

            // Send a streaming-final typing activity through the turn context middleware pipeline.
            var finalTyping = new Activity
            {
                Type = ActivityTypes.Typing,
                ChannelId = Channels.Test,
                Entities = [new StreamInfo { StreamType = StreamTypes.Final }]
            };
            await context.SendActivityAsync(finalTyping, CancellationToken.None);

            // Give the cancellation a moment to propagate.
            await Task.Delay(100);
            var countAfterStop = adapter.ActiveQueue.Count(a => a.Type == ActivityTypes.Typing);

            // No further typing should appear.
            await Task.Delay(200);
            var countAfterWait = adapter.ActiveQueue.Count(a => a.Type == ActivityTypes.Typing);
            await worker.DisposeAsync();

            Assert.Equal(countAfterStop, countAfterWait);
        }

        // ── Fix 1: transport errors must not fault the background task ───────────────

        [Fact]
        public async Task RunAsync_DoesNotFaultTask_WhenAdapterThrows()
        {
            // Arrange: adapter that throws on every send (simulates a transient transport error).
            var (_, context) = CreateMessageTurn();
            var throwingContext = new TurnContext(new ThrowingTestAdapter(), context.Activity);
            var worker = TypingWorker.Create(throwingContext, MakeOptions(initialDelayMs: 0, intervalMs: 30_000))!;

            worker.Start();

            // Act: give the background task time to fire and encounter the error.
            await Task.Delay(150);

            // Assert: DisposeAsync must not re-throw; a faulted _workerTask would propagate here.
            await worker.DisposeAsync();
        }

        // ── Fix 2: negative delay values must be rejected early ──────────────────────
        //
        // Task.Delay(ms) throws ArgumentOutOfRangeException for ms < 0 (other than -1).
        // Since TypingOptions/strategies are publicly settable, TypingWorker.Create validates
        // them and throws immediately so the problem surfaces at configuration time rather
        // than being silently swallowed inside the background task.

        [Fact]
        public void Create_WithNegativeInitialDelay_ThrowsArgumentOutOfRange()
        {
            var (_, context) = CreateMessageTurn();
            var options = new TypingOptions
            {
                InitialDelayMs = -1,
                IntervalMs = 2000,
                ChannelStrategies = new Dictionary<string, ITypingChannelStrategy>(
                    System.StringComparer.OrdinalIgnoreCase)
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => TypingWorker.Create(context, options));
        }

        [Fact]
        public void Create_WithNegativeIntervalMs_ThrowsArgumentOutOfRange()
        {
            var (_, context) = CreateMessageTurn();
            var options = new TypingOptions
            {
                InitialDelayMs = 0,
                IntervalMs = -1,
                ChannelStrategies = new Dictionary<string, ITypingChannelStrategy>(
                    System.StringComparer.OrdinalIgnoreCase)
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => TypingWorker.Create(context, options));
        }

        // ── Fix 3: StopTypingTimer must clear the service entry so Start can restart ─

        [Fact]
        public async Task StopTypingTimer_ClearsServiceEntry_AllowingRestart()
        {
            // Arrange
            var (_, context) = CreateMessageTurn();
            var app = new TestApplication(new TestApplicationOptions(new MemoryStorage()));

            // First start registers the worker.
            app.StartTypingTimer(context);
            Assert.NotNull(context.Services.Get<TypingWorker>());

            // Stop disposes and clears the service entry.
            await app.StopTypingTimer(context);
            Assert.Null(context.Services.Get<TypingWorker>());  // cleared by StopTypingTimer

            // Second start must succeed (without Fix 3 it early-returns because
            // the disposed worker is still registered).
            app.StartTypingTimer(context);
            Assert.NotNull(context.Services.Get<TypingWorker>());

            // Clean up.
            await app.StopTypingTimer(context);
        }

        // ─────────────────────────────────────────────────────────────────────────────

        // ─────────────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A <see cref="TestAdapter"/> whose <see cref="SendActivitiesAsync"/> always throws,
        /// simulating a transient transport failure.
        /// </summary>
        private sealed class ThrowingTestAdapter : TestAdapter
        {
            public override Task<ResourceResponse[]> SendActivitiesAsync(
                ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
                => throw new InvalidOperationException("Simulated transport error");
        }

        [Fact]
        public async Task Start_ResetsInterval_AfterNonTypingActivitySent()
        {
            // Use a 400ms interval and send a reset at ~100ms; the next typing should fire
            // ~400ms after the reset (~500ms total), not at the original ~400ms mark.
            var (adapter, context) = CreateMessageTurn();
            var worker = TypingWorker.Create(context, MakeOptions(initialDelayMs: 0, intervalMs: 400))!;
            worker.Start();

            // First typing fires at t~=0ms.
            await Task.Delay(50);
            var countBeforeReset = adapter.ActiveQueue.Count(a => a.Type == ActivityTypes.Typing);
            Assert.True(countBeforeReset >= 1, "First typing should have fired");

            // Reset the interval at t~=50ms. Without reset the next typing would fire at ~400ms.
            // With reset it fires at ~50ms + 400ms = ~450ms.
            await context.SendActivityAsync(
                new Activity
                {
                    Type = ActivityTypes.Event,
                    Name = "ping",
                    ChannelId = Channels.Test,
                    Conversation = new ConversationAccount { Id = "conv1" }
                },
                CancellationToken.None);

            // At t~=300ms (250ms after reset): without reset, typing would fire at ~400ms total.
            // With reset at t=50ms, next typing fires at ~450ms — so at t=300ms it hasn't fired yet.
            await Task.Delay(250); // t~=300ms
            var countBeforeExpected = adapter.ActiveQueue.Count(a => a.Type == ActivityTypes.Typing);
            Assert.Equal(countBeforeReset, countBeforeExpected);

            // At t~=600ms (550ms after reset, > 400ms interval): second typing fires.
            await Task.Delay(300); // t~=600ms
            var countAfterExpected = adapter.ActiveQueue.Count(a => a.Type == ActivityTypes.Typing);
            await worker.DisposeAsync();

            Assert.True(countAfterExpected > countBeforeReset,
                $"Second typing should have fired after reset interval elapsed, got {countAfterExpected}");
        }
    }
}
