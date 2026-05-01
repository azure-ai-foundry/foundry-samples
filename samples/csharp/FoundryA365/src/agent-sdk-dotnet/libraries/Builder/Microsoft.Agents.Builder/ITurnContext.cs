// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// A method that can participate in send activity events for the current turn.
    /// </summary>
    /// <param name="turnContext">The context object for the turn.</param>
    /// <param name="activities">The activities to send.</param>
    /// <param name="next">The delegate to call to continue event processing.</param>
    /// <returns>A task that represents the work queued to execute.</returns>
    /// <remarks>A handler calls the <paramref name="next"/> delegate to pass control to
    /// the next registered handler. If a handler doesn't call the next delegate,
    /// the adapter does not call any of the subsequent handlers and does not send the
    /// <paramref name="activities"/>.
    /// <para>If the activities are successfully sent, the <paramref name="next"/> delegate returns
    /// an array of <see cref="ResourceResponse"/> objects containing the IDs that
    /// the receiving channel assigned to the activities. Use this array as the return value of this handler.</para>
    /// </remarks>
    /// <seealso cref="Microsoft.Agents.Builder.IChannelAdapter"/>
    /// <seealso cref="Microsoft.Agents.Builder.ITurnContext.OnSendActivities(Microsoft.Agents.Builder.SendActivitiesHandler)"/>
    public delegate Task<ResourceResponse[]> SendActivitiesHandler(ITurnContext turnContext, List<IActivity> activities, Func<Task<ResourceResponse[]>> next);

    /// <summary>
    /// A method that can participate in update activity events for the current turn.
    /// </summary>
    /// <param name="turnContext">The context object for the turn.</param>
    /// <param name="activity">The replacement activity.</param>
    /// <param name="next">The delegate to call to continue event processing.</param>
    /// <returns>A task that represents the work queued to execute.</returns>
    /// <remarks>A handler calls the <paramref name="next"/> delegate to pass control to
    /// the next registered handler. If a handler doesn’t call the next delegate,
    /// the adapter does not call any of the subsequent handlers and does not update the
    /// activity.
    /// <para>The activity's <see cref="Activity.Id"/> indicates the activity in the
    /// conversation to replace.</para>
    /// <para>If the activity is successfully sent, the <paramref name="next"/> delegate returns
    /// a <see cref="ResourceResponse"/> object containing the ID that the receiving
    /// channel assigned to the activity. Use this response object as the return value of this handler.</para>
    /// </remarks>
    /// <seealso cref="Microsoft.Agents.Builder.IChannelAdapter"/>
    /// <seealso cref="Microsoft.Agents.Builder.SendActivitiesHandler"/>
    /// <seealso cref="Microsoft.Agents.Builder.DeleteActivityHandler"/>
    /// <seealso cref="Microsoft.Agents.Builder.ITurnContext.OnUpdateActivity(Microsoft.Agents.Builder.UpdateActivityHandler)"/>
    public delegate Task<ResourceResponse> UpdateActivityHandler(ITurnContext turnContext, IActivity activity, Func<Task<ResourceResponse>> next);

    /// <summary>
    /// A method that can participate in delete activity events for the current turn.
    /// </summary>
    /// <param name="turnContext">The context object for the turn.</param>
    /// <param name="reference">The conversation containing the activity.</param>
    /// <param name="next">The delegate to call to continue event processing.</param>
    /// <returns>A task that represents the work queued to execute.</returns>
    /// <remarks>A handler calls the <paramref name="next"/> delegate to pass control to
    /// the next registered handler. If a handler doesn’t call the next delegate,
    /// the adapter does not call any of the subsequent handlers and does not delete the
    /// activity.
    /// <para>The conversation reference's <see cref="ConversationReference.ActivityId"/>
    /// indicates the activity in the conversation to replace.</para>
    /// </remarks>
    /// <seealso cref="Microsoft.Agents.Builder.IChannelAdapter"/>
    /// <seealso cref="Microsoft.Agents.Builder.SendActivitiesHandler"/>
    /// <seealso cref="Microsoft.Agents.Builder.UpdateActivityHandler"/>
    /// <seealso cref="Microsoft.Agents.Builder.ITurnContext.OnDeleteActivity(Microsoft.Agents.Builder.DeleteActivityHandler)"/>
    public delegate Task DeleteActivityHandler(ITurnContext turnContext, ConversationReference reference, Func<Task> next);

    /// <summary>
    /// Provides context for a turn of an Agent, where the context's <see cref="Activity"/> property is strongly typed.
    /// </summary>
    /// <typeparam name="T">The activity type for this turn of the Agent.</typeparam>
    /// <remarks>The <see cref="IActivity"/> interface defines properties shared by every type of activity.
    /// The interfaces that derive from <see cref="IActivity"/> include properties specific to a specific
    /// <seealso cref="Microsoft.Agents.Core.Models.IActivity"/>
    /// <seealso cref="Microsoft.Agents.Builder.IAgent"/>
    /// <seealso cref="Microsoft.Agents.Builder.ITurnContext"/>
    /// </remarks>
    public interface ITurnContext<T> : ITurnContext
        where T : IActivity
    {
        /// <summary>
        /// Gets the activity for this turn of the Agent.
        /// </summary>
        /// <value>The activity for this turn of the Agent.</value>
        new T Activity { get; }
    }

    /// <summary>
    /// Provides context for a turn of an Agent.
    /// </summary>
    /// <remarks>Context provides information needed to process an incoming <see cref="IActivity"/>.
    /// The context object is created by a <see cref="IChannelAdapter"/> and persists for the
    /// length of the turn.</remarks>
    /// <seealso cref="Microsoft.Agents.Builder.IAgent"/>
    public interface ITurnContext
    {
        /// <summary>
        /// Gets the Adapter that created this context object.
        /// </summary>
        /// <value>The Adapter that created this context object.</value>
        IChannelAdapter Adapter { get; }

        TurnContextStateCollection Services { get; }
        TurnContextStateCollection StackState { get; }

        /// <summary>
        /// Gets the Activity for this Turn.  This is the incoming <see cref="IActivity"/>.
        /// </summary>
        /// <value>The activity for this turn of the Agent.</value>
        IActivity Activity { get; }

        /// <summary>
        /// Provides access to the IStreamingResponse to allow streaming of chunked messages (for example, via streaming AI responses).
        /// </summary>
        IStreamingResponse StreamingResponse { get; }

        /// <summary>
        /// Gets a value indicating whether at least one response was sent for the current turn.
        /// </summary>
        /// <value><c>true</c> if at least one response was sent for the current turn; otherwise, <c>false</c>.</value>
        /// <seealso cref="SendActivityAsync(Microsoft.Agents.Core.Models.IActivity, System.Threading.CancellationToken)"/>
        bool Responded { get; }

        ClaimsIdentity Identity { get; }

        /// <summary>
        /// Sends a message activity to the sender of the incoming activity.
        /// </summary>
        /// <param name="text">The text of the message to send.</param>
        /// <param name="speak">Optional, text to be spoken by your Agent on a speech-enabled
        /// channel.</param>
        /// <param name="inputHint">Optional, indicates whether your Agent is accepting,
        /// expecting, or ignoring user input after the message is delivered to the client.
        /// <see cref="InputHints"/> defines the possible values.
        /// Default is <see cref="InputHints.AcceptingInput"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>If the activity is successfully sent, the task result contains
        /// a <see cref="ResourceResponse"/> object that contains the ID that the receiving
        /// channel assigned to the activity.
        /// <para>See the channel's documentation for limits imposed upon the contents of
        /// <paramref name="text"/>.</para>
        /// <para>To control various characteristics of your Agent's speech such as voice,
        /// rate, volume, pronunciation, and pitch, specify <paramref name="speak"/> in
        /// Speech Synthesis Markup Language (SSML) format.</para></returns>
        /// <seealso cref="OnSendActivities(Microsoft.Agents.Builder.SendActivitiesHandler)"/>
        /// <seealso cref="SendActivityAsync(Microsoft.Agents.Core.Models.IActivity, System.Threading.CancellationToken)"/>
        /// <seealso cref="SendActivitiesAsync(Microsoft.Agents.Core.Models.IActivity[], System.Threading.CancellationToken)"/>
        Task<ResourceResponse> SendActivityAsync(string text, string speak = null, string inputHint = "acceptingInput", CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends an activity to the sender of the incoming activity.
        /// </summary>
        /// <param name="activity">The activity to send.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>If the activity is successfully sent, the task result contains
        /// a <see cref="ResourceResponse"/> object containing the ID that the receiving
        /// channel assigned to the activity.</returns>
        /// <seealso cref="OnSendActivities(Microsoft.Agents.Builder.SendActivitiesHandler)"/>
        /// <seealso cref="SendActivityAsync(string, string, string, System.Threading.CancellationToken)"/>
        /// <seealso cref="SendActivitiesAsync(Microsoft.Agents.Core.Models.IActivity[], System.Threading.CancellationToken)"/>
        Task<ResourceResponse> SendActivityAsync(IActivity activity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a set of activities to the sender of the incoming activity.
        /// </summary>
        /// <param name="activities">The activities to send.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>If the activities are successfully sent, the task result contains
        /// an array of <see cref="ResourceResponse"/> objects containing the IDs that
        /// the receiving channel assigned to the activities.</returns>
        /// <seealso cref="OnSendActivities(Microsoft.Agents.Builder.SendActivitiesHandler)"/>
        /// <seealso cref="SendActivityAsync(string, string, string, System.Threading.CancellationToken)"/>
        /// <seealso cref="SendActivityAsync(Microsoft.Agents.Core.Models.IActivity, System.Threading.CancellationToken)"/>
        Task<ResourceResponse[]> SendActivitiesAsync(IActivity[] activities, CancellationToken cancellationToken = default);

        /// <summary>
        /// Replaces an existing activity.
        /// </summary>
        /// <param name="activity">New replacement activity.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>If the activity is successfully sent, the task result contains
        /// a <see cref="ResourceResponse"/> object containing the ID that the receiving
        /// channel assigned to the activity.
        /// <para>Before calling this, set the ID of the replacement activity to the ID
        /// of the activity to replace.</para>
        /// <para>Not all channels support this operation. For channels that don't, this call may throw an exception.</para></returns>
        /// <seealso cref="OnUpdateActivity(Microsoft.Agents.Builder.UpdateActivityHandler)"/>
        /// <seealso cref="SendActivitiesAsync(Microsoft.Agents.Core.Models.IActivity[], System.Threading.CancellationToken)"/>
        /// <seealso cref="DeleteActivityAsync(Microsoft.Agents.Core.Models.ConversationReference, System.Threading.CancellationToken)"/>
        Task<ResourceResponse> UpdateActivityAsync(IActivity activity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes an existing activity.
        /// </summary>
        /// <param name="activityId">The ID of the activity to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <remarks>Not all channels support this operation. For channels that don\'t, this call may throw an exception.</remarks>
        /// <seealso cref="OnDeleteActivity(Microsoft.Agents.Builder.DeleteActivityHandler)"/>
        /// <seealso cref="DeleteActivityAsync(Microsoft.Agents.Core.Models.ConversationReference, System.Threading.CancellationToken)"/>
        /// <seealso cref="SendActivitiesAsync(Microsoft.Agents.Core.Models.IActivity[], System.Threading.CancellationToken)"/>
        /// <seealso cref="UpdateActivityAsync(Microsoft.Agents.Core.Models.IActivity, System.Threading.CancellationToken)"/>
        Task DeleteActivityAsync(string activityId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes an existing activity.
        /// </summary>
        /// <param name="conversationReference">The conversation containing the activity to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>The conversation reference's <see cref="ConversationReference.ActivityId"/>
        /// indicates the activity in the conversation to delete.
        /// <para>Not all channels support this operation. For channels that don't, this call may throw an exception.</para></returns>
        /// <seealso cref="OnDeleteActivity(Microsoft.Agents.Builder.DeleteActivityHandler)"/>
        /// <seealso cref="DeleteActivityAsync(string, System.Threading.CancellationToken)"/>
        /// <seealso cref="SendActivitiesAsync(Microsoft.Agents.Core.Models.IActivity[], System.Threading.CancellationToken)"/>
        /// <seealso cref="UpdateActivityAsync(Microsoft.Agents.Core.Models.IActivity, System.Threading.CancellationToken)"/>
        Task DeleteActivityAsync(ConversationReference conversationReference, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a response handler for send activity operations.
        /// </summary>
        /// <param name="handler">The handler to add to the context object.</param>
        /// <remarks>When the context's <see cref="SendActivityAsync(IActivity, CancellationToken)"/>
        /// or <see cref="SendActivitiesAsync(IActivity[], CancellationToken)"/> method is called,
        /// the adapter calls the registered handlers in the order in which they were
        /// added to the context object.
        /// </remarks>
        /// <returns>The updated context object.</returns>
        /// <seealso cref="SendActivityAsync(string, string, string, System.Threading.CancellationToken)"/>
        /// <seealso cref="SendActivityAsync(Microsoft.Agents.Core.Models.IActivity, System.Threading.CancellationToken)"/>
        /// <seealso cref="SendActivitiesAsync(Microsoft.Agents.Core.Models.IActivity[], System.Threading.CancellationToken)"/>
        /// <seealso cref="Microsoft.Agents.Builder.SendActivitiesHandler"/>
        ITurnContext OnSendActivities(SendActivitiesHandler handler);

        /// <summary>
        /// Adds a response handler for update activity operations.
        /// </summary>
        /// <param name="handler">The handler to add to the context object.</param>
        /// <returns>The updated context object.</returns>
        /// <remarks>When the context's <see cref="UpdateActivityAsync(IActivity, CancellationToken)"/> is called,
        /// the adapter calls the registered handlers in the order in which they were
        /// added to the context object.
        /// </remarks>
        /// <seealso cref="UpdateActivityAsync(Microsoft.Agents.Core.Models.IActivity, System.Threading.CancellationToken)"/>
        /// <seealso cref="Microsoft.Agents.Builder.UpdateActivityHandler"/>
        /// <seealso cref="OnSendActivities(Microsoft.Agents.Builder.SendActivitiesHandler)"/>
        /// <seealso cref="OnDeleteActivity(Microsoft.Agents.Builder.DeleteActivityHandler)"/>
        ITurnContext OnUpdateActivity(UpdateActivityHandler handler);

        /// <summary>
        /// Adds a response handler for delete activity operations.
        /// </summary>
        /// <param name="handler">The handler to add to the context object.</param>
        /// <returns>The updated context object.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="handler"/> is <c>null</c>.</exception>
        /// <remarks>When the context's <see cref="DeleteActivityAsync(string, CancellationToken)"/> is called,
        /// the adapter calls the registered handlers in the order in which they were
        /// added to the context object.
        /// </remarks>
        /// <seealso cref="DeleteActivityAsync(Microsoft.Agents.Core.Models.ConversationReference, System.Threading.CancellationToken)"/>
        /// <seealso cref="DeleteActivityAsync(string, System.Threading.CancellationToken)"/>
        /// <seealso cref="Microsoft.Agents.Builder.DeleteActivityHandler"/>
        /// <seealso cref="OnSendActivities(Microsoft.Agents.Builder.SendActivitiesHandler)"/>
        /// <seealso cref="OnUpdateActivity(Microsoft.Agents.Builder.UpdateActivityHandler)"/>
        ITurnContext OnDeleteActivity(DeleteActivityHandler handler);


        /// <summary>
        /// Sends a trace activity to the <see cref="IChannelAdapter"/> for logging purposes.
        /// </summary>
        /// <param name="turnContext">The context for the current turn.</param>
        /// <param name="name">The value to assign to the activity's <see cref="Activity.Name"/> property.</param>
        /// <param name="value">The value to assign to the activity's <see cref="Activity.Value"/> property.</param>
        /// <param name="valueType">The value to assign to the activity's <see cref="Activity.ValueType"/> property.</param>
        /// <param name="label">The value to assign to the activity's <see cref="Activity.Label"/> property.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>If the adapter is being hosted in the Emulator, the task result contains
        /// a <see cref="ResourceResponse"/> object with the original trace activity's ID; otherwise,
        /// it contains a <see cref="ResourceResponse"/> object containing the ID that the receiving
        /// channel assigned to the activity.</returns>
        Task<ResourceResponse> TraceActivityAsync(string name, object value = null, string valueType = null, [CallerMemberName] string label = null, CancellationToken cancellationToken = default);
    }
}
