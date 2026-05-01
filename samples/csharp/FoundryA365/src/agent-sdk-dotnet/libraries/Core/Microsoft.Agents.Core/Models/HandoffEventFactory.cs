// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Contains utility methods for creating various event types.
    /// </summary>
    public static class HandoffEventFactory
    {
        /// <summary>
        /// Create handoff initiation event.
        /// </summary>
        /// <param name="activity">Incoming activity</param>
        /// <param name="handoffContext">agent hub-specific context.</param>
        /// <param name="transcript">transcript of the conversation.</param>
        /// <returns>handoff event.</returns>
        public static IActivity CreateHandoffInitiation(IActivity activity, object handoffContext, Transcript transcript = null)
        {
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));

            var handoffEvent = CreateHandoffEvent(HandoffEventNames.InitiateHandoff, handoffContext, activity.Conversation);

            handoffEvent.From = activity.From;
            handoffEvent.RelatesTo = activity.GetConversationReference();
            handoffEvent.ReplyToId = activity.Id;
            handoffEvent.ServiceUrl = activity.ServiceUrl;
            handoffEvent.ChannelId = activity.ChannelId;

            if (transcript != null)
            {
                var attachment = new Attachment
                {
                    Content = transcript,
                    ContentType = "application/json",
                    Name = "Transcript",
                };
                handoffEvent.Attachments.Add(attachment);
            }

            return handoffEvent;
        }

        /// <summary>
        /// Create handoff status event.
        /// </summary>
        /// <param name="conversation">Conversation being handed over.</param>
        /// <param name="state">State, possible values are: "accepted", "failed", "completed".</param>
        /// <param name="message">Additional message for failed handoff.</param>
        /// <returns>handoff event.</returns>
        public static Activity CreateHandoffStatus(ConversationAccount conversation, string state, string message = null)
        {
            AssertionHelpers.ThrowIfNull(conversation, nameof(conversation));
            AssertionHelpers.ThrowIfNullOrWhiteSpace(state, nameof(state));

            object value = new { state, message };

            var handoffEvent = CreateHandoffEvent(HandoffEventNames.HandoffStatus, value, conversation);
            return handoffEvent;
        }

        private static Activity CreateHandoffEvent(string name, object value, ConversationAccount conversation)
        {
            var handoffEvent = Activity.CreateEventActivity() as Activity;

            handoffEvent.Name = name;
            handoffEvent.Value = value;
            handoffEvent.Id = Guid.NewGuid().ToString();
            handoffEvent.Timestamp = DateTime.UtcNow;
            handoffEvent.Conversation = conversation;
            handoffEvent.Attachments = [];
            handoffEvent.Entities = [];
            return handoffEvent;
        }
    }
}
