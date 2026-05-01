// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Per-turn typing indicator worker. Sends periodic "typing" activities on a background task
    /// for the duration of the turn, using channel-specific timing strategies.
    /// </summary>
    /// <remarks>
    /// Unlike the legacy <c>TypingTimer</c>, this class is scoped to a single turn and uses
    /// <see cref="Task.Delay(int, CancellationToken)"/> instead of <see cref="System.Threading.Timer"/>,
    /// avoiding thread-pool starvation and static shared state bugs.
    /// </remarks>
    internal sealed class TypingWorker : IAsyncDisposable
    {
        private readonly ITurnContext _turnContext;
        private readonly ITypingChannelStrategy _strategy;

        private readonly CancellationTokenSource _stopCts = new();
        private TaskCompletionSource<bool> _resetTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private Task _workerTask;
        private int _started;   // 0 = not started, 1 = started (Interlocked)
        private int _disposed;  // 0 = not disposed, 1 = disposed (Interlocked)

        private TypingWorker(ITurnContext turnContext, ITypingChannelStrategy strategy)
        {
            _turnContext = turnContext;
            _strategy = strategy;
        }

        /// <summary>
        /// Creates a <see cref="TypingWorker"/> for the current turn, or returns <c>null</c> if
        /// the activity is not a message or typing is not applicable.
        /// </summary>
        public static TypingWorker? Create(ITurnContext turnContext, TypingOptions options)
        {
            if (turnContext.Activity.Type != ActivityTypes.Message)
            {
                return null;
            }

            var channelId = turnContext.Activity.ChannelId ?? string.Empty;
            ITypingChannelStrategy strategy;
            if (options.ChannelStrategies != null &&
                options.ChannelStrategies.TryGetValue(channelId, out var channelStrategy))
            {
                strategy = channelStrategy;
            }
            else
            {
                strategy = new TypingChannelStrategy(options.InitialDelayMs, options.IntervalMs);
            }

            if (strategy.InitialDelayMs < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    strategy.InitialDelayMs,
                    $"{nameof(ITypingChannelStrategy.InitialDelayMs)} must be >= 0.");
            }

            if (strategy.IntervalMs < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    strategy.IntervalMs,
                    $"{nameof(ITypingChannelStrategy.IntervalMs)} must be >= 0.");
            }

            return new TypingWorker(turnContext, strategy);
        }

        /// <summary>
        /// Starts the background typing task and registers handlers to reset the interval countdown
        /// on non-typing agent sends, updates, or deletions.
        /// </summary>
        public void Start()
        {
            if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
            {
                return;
            }

            _turnContext.OnSendActivities(OnSendActivitiesAsync);
            _turnContext.OnDeleteActivity((ctx, reference, next) => { ResetInterval(); return next(); });
            _turnContext.OnUpdateActivity((ctx, activity, next) => { ResetInterval(); return next(); });

            // Fire and forget — RunAsync yields immediately at the first Task.Delay.
            _workerTask = RunAsync(_stopCts.Token);
        }

        private async Task RunAsync(CancellationToken stopToken)
        {
            try
            {
                await WaitAsync(_strategy.InitialDelayMs, stopToken).ConfigureAwait(false);

                while (!stopToken.IsCancellationRequested)
                {
                    await SendTypingActivityAsync(stopToken).ConfigureAwait(false);
                    await WaitAsync(_strategy.IntervalMs, stopToken).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                // Typing indicator failures (including transient transport errors) are best-effort
                // and must not fault the background worker task, because DisposeAsync awaits it
                // during turn cleanup and a faulted task would mask or replace the turn exception.
            }
        }

        /// <summary>
        /// Waits for <paramref name="ms"/> milliseconds, restarting the countdown each time
        /// <see cref="ResetInterval"/> is called (i.e., on each non-typing agent send).
        /// Uses <see cref="TaskCompletionSource{T}"/> for reset signalling to avoid
        /// <see cref="CancellationTokenSource"/> allocation and disposal races per reset.
        /// </summary>
        private async Task WaitAsync(int ms, CancellationToken stopToken)
        {
            while (true)
            {
                var resetTask = _resetTcs.Task;
                await Task.WhenAny(Task.Delay(ms, stopToken), resetTask).ConfigureAwait(false);

                stopToken.ThrowIfCancellationRequested();

                if (!resetTask.IsCompleted)
                {
                    return; // Delay elapsed without reset — done.
                }

                // Reset fired — restart the countdown.
            }
        }

        private Task<ResourceResponse[]> OnSendActivitiesAsync(
            ITurnContext ctx,
            List<IActivity> activities,
            Func<Task<ResourceResponse[]>> next)
        {
            foreach (var activity in activities)
            {
                if (activity.Type == ActivityTypes.Typing)
                {
                    var streamInfo = activity.GetStreamingEntity();
                    if (streamInfo != null && streamInfo.StreamType == StreamTypes.Final)
                    {
                        // Streaming complete — stop the worker.
                        _stopCts.Cancel();
                    }
                    // else: our own periodic typing send — ignore.
                }
                else
                {
                    // Non-typing activity (e.g., message, event) — reset interval countdown.
                    ResetInterval();
                }
            }

            return next();
        }

        private void ResetInterval()
        {
            var old = Interlocked.Exchange(ref _resetTcs, new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
            old.TrySetResult(true);
        }

        private async Task SendTypingActivityAsync(CancellationToken cancellationToken)
        {
            // Send directly on the adapter to bypass OnSendActivities middleware (matching
            // ShowTypingMiddleware's approach) so our own handler doesn't reset the interval.
            var conversationReference = _turnContext.Activity.GetConversationReference();
            var typingActivity = _strategy.TypingFactory(_turnContext, conversationReference);
            typingActivity.ApplyConversationReference(conversationReference);

            await _turnContext.Adapter.SendActivitiesAsync(
                _turnContext, [typingActivity], cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Stops the background task and waits for it to complete.
        /// Safe to call multiple times; subsequent calls are no-ops.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _stopCts.Cancel();

            if (_workerTask != null)
            {
                try
                {
                    await _workerTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected on cancellation.
                }
            }

            _stopCts.Dispose();
        }
    }
}
