// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage.Transcript;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Middleware for logging incoming and outgoing activities to an <see cref="ITranscriptStore"/>.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="TranscriptLoggerMiddleware"/> class.
    /// </remarks>
    /// <param name="transcriptLogger">The conversation store to use.</param>
    public class TranscriptLoggerMiddleware(ITranscriptLogger transcriptLogger) : IMiddleware
    {
        private readonly ITranscriptLogger _logger = transcriptLogger ?? throw new ArgumentNullException(nameof(transcriptLogger), "TranscriptLoggerMiddleware requires a ITranscriptLogger implementation.");

        /// <summary>
        /// Records incoming and outgoing activities to the conversation store.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="nextTurn">The delegate to call to continue the Agent middleware pipeline.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <seealso cref="Microsoft.Agents.Builder.ITurnContext"/>
        /// <seealso cref="Microsoft.Agents.Core.Models.Activity"/>
        public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate nextTurn, CancellationToken cancellationToken)
        {
            var transcript = new Queue<IActivity>();

            try
            {
                // log incoming activity at beginning of turn
                if (turnContext.Activity != null)
                {
                    turnContext.Activity.From ??= new ChannelAccount();

                    if (string.IsNullOrEmpty(turnContext.Activity.From.Role))
                    {
                        turnContext.Activity.From.Role = RoleTypes.User;
                    }

                    // We should not log ContinueConversation events used by Agents to initialize the middleware.
                    if (!(turnContext.Activity.Type == ActivityTypes.Event && turnContext.Activity.Name == ActivityEventNames.ContinueConversation))
                    {
                        LogActivity(transcript, CloneActivity(turnContext.Activity));
                    }
                }

                // hook up onSend pipeline
                turnContext.OnSendActivities(async (ctx, activities, nextSend) =>
                {
                    // run full pipeline
                    var responses = await nextSend().ConfigureAwait(false);

                    foreach (var activity in activities)
                    {
                        LogActivity(transcript, CloneActivity(activity));
                    }

                    return responses;
                });

                // hook up update activity pipeline
                turnContext.OnUpdateActivity(async (ctx, activity, nextUpdate) =>
                {
                    // run full pipeline
                    var response = await nextUpdate().ConfigureAwait(false);

                    // add Message Update activity
                    var updateActivity = CloneActivity(activity);
                    updateActivity.Type = ActivityTypes.MessageUpdate;
                    LogActivity(transcript, updateActivity);
                    return response;
                });

                // hook up delete activity pipeline
                turnContext.OnDeleteActivity(async (ctx, reference, nextDelete) =>
                {
                    // run full pipeline
                    await nextDelete().ConfigureAwait(false);

                    // add MessageDelete activity
                    // log as MessageDelete activity
                    var deleteActivity = new Activity
                    {
                        Type = ActivityTypes.MessageDelete,
                        Id = reference.ActivityId,
                    }
                        .ApplyConversationReference(reference, isIncoming: false);

                    LogActivity(transcript, deleteActivity);
                });

                // process Agent logic
                await nextTurn(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // Ensure we flush the transcript even if there is an error.
                // This ensures we capture the activity stream leading up to the error.
                // flush transcript at end of turn
                // NOTE: We are not awaiting this task by design, TryLogTranscriptAsync() observes all exceptions and we don't need to or want to block execution on the completion.
                _ = TryLogTranscriptAsync(_logger, transcript);
            }
        }

        /// <summary>
        /// Helper to sequentially flush the transcript queue to the log.
        /// </summary>
        private static async Task TryLogTranscriptAsync(ITranscriptLogger logger, Queue<IActivity> transcript)
        {
            try
            {
                while (transcript.Count > 0)
                {
                    // Process the queue and log all the activities in parallel.
                    var activity = transcript.Dequeue();
                    await logger.LogActivityAsync(activity).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Transcript logActivity failed with {ex}");
            }
        }

        private static IActivity CloneActivity(IActivity activity)
        {
            activity = activity.Clone();
            var activityWithId = EnsureActivityHasId(activity);

            return activityWithId;
        }

        private static IActivity EnsureActivityHasId(IActivity activity)
        {
            var activityWithId = activity;

            if (activity == null)
            {
                throw new ArgumentNullException(nameof(activity), "Cannot check or add Id on a null Activity.");
            }

            if (string.IsNullOrEmpty(activity.Id))
            {
                var generatedId = $"g_{Guid.NewGuid()}";
                activity.Id = generatedId;
            }

            return activityWithId;
        }

        private static void LogActivity(Queue<IActivity> transcript, IActivity activity)
        {
            activity.Timestamp ??= DateTime.UtcNow;
            transcript.Enqueue(activity);
        }
    }
}
