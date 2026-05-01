// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Telemetry.TurnContext;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Provides context for a turn of an Agent.
    /// </summary>
    /// <remarks>
    /// Context provides information needed to process an incoming activity.
    /// The context object is created by a <see cref="IChannelAdapter"/> and persists for the
    /// length of the turn.  TurnContext cannot be used after the turn is complete.
    /// </remarks>
    /// <seealso cref="Microsoft.Agents.Builder.IAgent"/>
    public class TurnContext : ITurnContext, IDisposable
    {
        private readonly IList<SendActivitiesHandler> _onSendActivities = [];
        private readonly IList<UpdateActivityHandler> _onUpdateActivity = [];
        private readonly IList<DeleteActivityHandler> _onDeleteActivity = [];

        private bool _disposed;
        private readonly IStreamingResponse _streamingResponse;

        /// <summary>
        /// Initializes a new instance of the <see cref="TurnContext"/> class.
        /// </summary>
        /// <param name="adapter">The adapter creating the context.</param>
        /// <param name="activity">The incoming activity for the turn;
        /// or <c>null</c> for a turn for a proactive message.</param>
        /// <param name="identity"></param>
        /// <param name="state"></param>
        /// <exception cref="System.ArgumentNullException"><paramref name="activity"/> or
        /// <paramref name="adapter"/> is <c>null</c>.</exception>
        /// <remarks>For use by Adapter implementations only.</remarks>
        public TurnContext(IChannelAdapter adapter, IActivity activity, ClaimsIdentity identity = null)
        {
            Adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            Activity = activity ?? throw new ArgumentNullException(nameof(activity));
            Identity = identity;
            StackState = [];
            Services = [];

            _streamingResponse = new StreamingResponse(this);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ITurnContext"/> class from another TurnContext class to target an alternate Activity.
        /// </summary>
        /// <remarks>
        /// For supporting calling legacy systems that always assume ITurnContext.Activity is the activity should be processed.
        /// This class clones the TurnContext and then replaces the original.activity with the passed in activity.
        /// </remarks>
        /// <param name="turnContext">context to clone.</param>
        /// <param name="activity">activity to put into the new turn context.</param>
        public TurnContext(ITurnContext turnContext, IActivity activity)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));

            Activity = activity ?? throw new ArgumentNullException(nameof(activity));
            _streamingResponse = new StreamingResponse(this);

            // all properties should be copied over except for activity.
            Adapter = turnContext.Adapter;
            StackState = turnContext.StackState;
            Services = turnContext.Services;
            Responded = turnContext.Responded;

            if (turnContext is TurnContext tc)
            {
                // keep private middleware pipeline hooks.
                _onSendActivities = tc._onSendActivities;
                _onUpdateActivity = tc._onUpdateActivity;
                _onDeleteActivity = tc._onDeleteActivity;
            }
        }

        /// <summary>
        /// Gets the Adapter that created this context object.
        /// </summary>
        /// <value>The Adapter that created this context object.</value>
        public IChannelAdapter Adapter { get; }

        public TurnContextStateCollection StackState { get; }

        /// <summary>
        /// Gets the services registered on this context object.
        /// </summary>
        /// <value>The services registered on this context object.</value>
        public TurnContextStateCollection Services { get; }

        /// <summary>
        /// Gets the activity associated with this turn; or <c>null</c> when processing
        /// a proactive message.
        /// </summary>
        /// <value>The activity associated with this turn.</value>
        public IActivity Activity { get; }

        /// <inheritdoc/>
        public IStreamingResponse StreamingResponse { get { return _streamingResponse; } }

        public ClaimsIdentity Identity { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether at least one response was sent for the current turn.
        /// </summary>
        /// <value><c>true</c> if at least one response was sent for the current turn.</value>
        public bool Responded
        {
            get;
            private set;
        }

        /// <inheritdoc/>
        public ITurnContext OnSendActivities(SendActivitiesHandler handler)
        {
            AssertionHelpers.ThrowIfObjectDisposed(_disposed, nameof(OnSendActivities));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            _onSendActivities.Add(handler);
            return this;
        }

        /// <inheritdoc/>
        public ITurnContext OnUpdateActivity(UpdateActivityHandler handler)
        {
            AssertionHelpers.ThrowIfObjectDisposed(_disposed, nameof(OnUpdateActivity));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            _onUpdateActivity.Add(handler);
            return this;
        }

        /// <inheritdoc/>
        public ITurnContext OnDeleteActivity(DeleteActivityHandler handler)
        {
            AssertionHelpers.ThrowIfObjectDisposed(_disposed, nameof(OnDeleteActivity));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            _onDeleteActivity.Add(handler);
            return this;
        }

        /// <inheritdoc/>
        public Task<ResourceResponse> SendActivityAsync(string textReplyToSend, string speak = null, string inputHint = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfObjectDisposed(_disposed, nameof(SendActivityAsync));
            AssertionHelpers.ThrowIfNullOrWhiteSpace(textReplyToSend, nameof(textReplyToSend));

            var activityToSend = new Activity()
            {
                Type = ActivityTypes.Message,
                Text = textReplyToSend
            };

            if (!string.IsNullOrEmpty(speak))
            {
                activityToSend.Speak = speak;
            }

            if (!string.IsNullOrEmpty(inputHint))
            {
                activityToSend.InputHint = inputHint;
            }

            return SendActivityAsync(activityToSend, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<ResourceResponse> SendActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfObjectDisposed(_disposed, nameof(SendActivityAsync));
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));

            ResourceResponse[] responses = await SendActivitiesAsync(new[] { activity }, cancellationToken).ConfigureAwait(false);
            if (responses == null || responses.Length == 0)
            {
                // It's possible an interceptor prevented the activity from having been sent.
                // Just return an empty response in that case.
                return new ResourceResponse();
            }
            else
            {
                return responses[0];
            }
        }

        /// <inheritdoc/>
        public Task<ResourceResponse[]> SendActivitiesAsync(IActivity[] activities, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfObjectDisposed(_disposed, nameof(SendActivitiesAsync));
            AssertionHelpers.ThrowIfNull(activities, nameof(activities));

            if (activities.Length == 0)
            {
                throw new ArgumentException("Expecting one or more activities, but the array was empty.", nameof(activities));
            }

            using var telemetryScope = new ScopeSendActivities(this);

            // ConversationReference for the incoming Activity
            var conversationReference = Activity.GetConversationReference();

            var bufferedActivities = new List<IActivity>(activities.Length);

            for (var index = 0; index < activities.Length; index++)
            {
                // Buffer the incoming activities into a List<T> since we allow the set to be manipulated by the callbacks
                // Bind the relevant Conversation Reference properties, such as URLs and
                // ChannelId's, to the activity we're about to send
                bufferedActivities.Add(activities[index].ApplyConversationReference(conversationReference));
            }

            // If there are no callbacks registered, bypass the overhead of invoking them and send directly to the adapter
            if (_onSendActivities.Count == 0)
            {
                return SendActivitiesThroughAdapter();
            }

            // Send through the full callback pipeline
            return SendActivitiesThroughCallbackPipeline();

            Task<ResourceResponse[]> SendActivitiesThroughCallbackPipeline(int nextCallbackIndex = 0)
            {
                // If we've executed the last callback, we now send straight to the adapter
                if (nextCallbackIndex == _onSendActivities.Count)
                {
                    return SendActivitiesThroughAdapter();
                }

                return _onSendActivities[nextCallbackIndex].Invoke(this, bufferedActivities, () => SendActivitiesThroughCallbackPipeline(nextCallbackIndex + 1));
            }

            async Task<ResourceResponse[]> SendActivitiesThroughAdapter()
            {
                // Send from the list which may have been manipulated via the event handlers.
                // Note that 'responses' was captured from the root of the call, and will be
                // returned to the original caller.
                var responses = await Adapter.SendActivitiesAsync(this, [.. bufferedActivities], cancellationToken).ConfigureAwait(false);
                var sentNonTraceActivity = false;

                for (var index = 0; index < responses?.Length; index++)
                {
                    var activity = bufferedActivities[index];

                    activity.Id = responses[index].Id;

                    sentNonTraceActivity |= activity.Type != ActivityTypes.Trace;
                }

                if (sentNonTraceActivity)
                {
                    Responded = true;
                }

                return responses;
            }
        }

        /// <inheritdoc/>
        public Task<ResourceResponse> UpdateActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfObjectDisposed(_disposed, nameof(UpdateActivityAsync));
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));

            var conversationReference = Activity.GetConversationReference();
            var a = activity.ApplyConversationReference(conversationReference);
            return UpdateActivityInternalAsync(a, _onUpdateActivity, () => Adapter.UpdateActivityAsync(this, a, cancellationToken), cancellationToken);
        }

        /// <inheritdoc/>
        public Task DeleteActivityAsync(string activityId, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfObjectDisposed(_disposed, nameof(DeleteActivityAsync));
            AssertionHelpers.ThrowIfNullOrWhiteSpace(activityId, nameof(activityId));

            var cr = Activity.GetConversationReference();
            cr.ActivityId = activityId;
            return DeleteActivityInternalAsync(cr, _onDeleteActivity, () => Adapter.DeleteActivityAsync(this, cr, cancellationToken), cancellationToken);
        }

        /// <inheritdoc/>
        public Task DeleteActivityAsync(ConversationReference conversationReference, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfObjectDisposed(_disposed, nameof(DeleteActivityAsync));
            AssertionHelpers.ThrowIfNull(conversationReference, nameof(conversationReference));

            return DeleteActivityInternalAsync(conversationReference, _onDeleteActivity, () => Adapter.DeleteActivityAsync(this, conversationReference, cancellationToken), cancellationToken);
        }

        /// <inheritdoc/>
        public Task<ResourceResponse> TraceActivityAsync(string name, object value = null, string valueType = null, [CallerMemberName] string label = null, CancellationToken cancellationToken = default)
        {
            return SendActivityAsync(MessageFactory.CreateTrace(this.Activity, name, value, valueType, label), cancellationToken);
        }

        /// <summary>
        /// Frees resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <param name="disposing">Boolean value that determines whether to free resources or not.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                StackState.Dispose();
                Services.Dispose();
            }

            _disposed = true;
        }

        private async Task<ResourceResponse> UpdateActivityInternalAsync(
            IActivity activity,
            IEnumerable<UpdateActivityHandler> updateHandlers,
            Func<Task<ResourceResponse>> callAtBottom,
            CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));

            if (updateHandlers == null)
            {
                throw new ArgumentException($"{nameof(updateHandlers)} is null.", nameof(updateHandlers));
            }

            // No middleware to run.
            if (!updateHandlers.Any())
            {
                if (callAtBottom != null)
                {
                    return await callAtBottom().ConfigureAwait(false);
                }

                return null;
            }

            // Default to "No more Middleware after this".
            async Task<ResourceResponse> NextAsync()
            {
                // Remove the first item from the list of middleware to call,
                // so that the next call just has the remaining items to worry about.
                IEnumerable<UpdateActivityHandler> remaining = updateHandlers.Skip(1);
                var result = await UpdateActivityInternalAsync(activity, remaining, callAtBottom, cancellationToken).ConfigureAwait(false);
                activity.Id = result.Id;
                return result;
            }

            // Grab the current middleware, which is the 1st element in the array, and execute it
            UpdateActivityHandler toCall = updateHandlers.First();
            return await toCall(this, activity, NextAsync).ConfigureAwait(false);
        }

        private async Task DeleteActivityInternalAsync(
            ConversationReference cr,
            IEnumerable<DeleteActivityHandler> deleteHandlers,
            Func<Task> callAtBottom,
            CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfNull(cr, nameof(cr));

            if (deleteHandlers == null)
            {
                throw new ArgumentException($"{nameof(deleteHandlers)} is null", nameof(deleteHandlers));
            }

            // No middleware to run.
            if (!deleteHandlers.Any())
            {
                if (callAtBottom != null)
                {
                    await callAtBottom().ConfigureAwait(false);
                }

                return;
            }

            // Default to "No more Middleware after this".
            async Task NextAsync()
            {
                // Remove the first item from the list of middleware to call,
                // so that the next call just has the remaining items to worry about.
                IEnumerable<DeleteActivityHandler> remaining = deleteHandlers.Skip(1);
                await DeleteActivityInternalAsync(cr, remaining, callAtBottom, cancellationToken).ConfigureAwait(false);
            }

            // Grab the current middleware, which is the 1st element in the array, and execute it.
            DeleteActivityHandler toCall = deleteHandlers.First();
            await toCall(this, cr, NextAsync).ConfigureAwait(false);
        }
    }
}
