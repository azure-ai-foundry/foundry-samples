// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.CopilotStudio.Client.Models;
using Microsoft.Agents.Core.Models;
using System.Collections.Generic;
using System.Threading;

namespace Microsoft.Agents.CopilotStudio.Client.Interfaces
{
    /// <summary>
    /// Defines the contract for a client that connects to the Direct-to-Engine API endpoint for Copilot Studio.
    /// </summary>
    public interface ICopilotClient
    {
        /// <summary>
        /// [Deprecated] Use SendActivityAsync(IActivity, CancellationToken) instead.
        /// Sends an activity to the remote bot and returns the response as an async enumerable stream of activities.
        /// </summary>
        /// <param name="activity">The activity to send.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>An async enumerable stream of activities representing the agent's responses.</returns>
        IAsyncEnumerable<IActivity> AskQuestionAsync(IActivity activity, CancellationToken ct = default);

        /// <summary>
        /// Sends a string question to the remote bot and returns the response as an async enumerable stream of activities.
        /// </summary>
        /// <param name="question">The question to send to the Copilot.</param>
        /// <param name="conversationId">The conversation ID to reference. Optional. If not set, it will use the current conversation ID.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An async enumerable stream of activities representing the agent's responses to the question.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="question"/> is null.</exception>
        /// <exception cref="HttpRequestException">Thrown when the HTTP request to Copilot Studio fails.</exception>
        IAsyncEnumerable<IActivity> AskQuestionAsync(string question, string? conversationId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes an activity within the context of a specific conversation.
        /// This method ensures the activity is associated with the provided conversation ID.
        /// </summary>
        /// <param name="conversationId">The conversation ID to execute the activity within. Cannot be null.</param>
        /// <param name="activityToSend">The activity to send to the Copilot Studio agent. Cannot be null.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>An async enumerable stream of activities representing the agent's responses.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="activityToSend"/> or <paramref name="conversationId"/> is null.</exception>
        /// <exception cref="HttpRequestException">Thrown when the HTTP request to Copilot Studio fails.</exception>
        /// <exception cref="JsonException">Thrown when the response cannot be deserialized.</exception>
        /// <remarks>
        /// This method forces the activity's conversation ID to match the provided parameter,
        /// overriding any conversation ID that may already be set on the activity.
        /// </remarks>
        IAsyncEnumerable<IActivity> ExecuteAsync(string conversationId, IActivity activityToSend, CancellationToken cancellationToken);

        /// <summary>
        /// Sends an activity to the remote bot and returns the response as an async enumerable stream of activities.
        /// </summary>
        /// <param name="activity">The activity to send.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An async enumerable stream of activities representing the agent's responses to the sent activity.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="activity"/> is null.</exception>
        /// <exception cref="HttpRequestException">Thrown when the HTTP request to Copilot Studio fails.</exception>
        /// <exception cref="JsonException">Thrown when the response cannot be deserialized.</exception>
        /// <remarks>
        /// This method uses the conversation ID from the activity if present, otherwise it uses the
        /// conversation ID from a previous StartConversationAsync call.
        /// </remarks>
        IAsyncEnumerable<IActivity> SendActivityAsync(IActivity activity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts a conversation with Copilot Studio.
        /// </summary>
        /// <param name="emitStartConversationEvent">Indicates whether to ask the remote bot to emit a start event.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An async enumerable stream of activities returned by the agent during conversation initialization.</returns>
        IAsyncEnumerable<IActivity> StartConversationAsync(bool emitStartConversationEvent = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts a new conversation with the Copilot Studio agent using a custom start request.
        /// </summary>
        /// <param name="startRequest">Custom start request containing locale, conversation ID, and other start parameters.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>An async enumerable stream of activities returned by the agent during conversation initialization.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="startRequest"/> is null.</exception>
        /// <exception cref="HttpRequestException">Thrown when the HTTP request to Copilot Studio fails.</exception>
        /// <exception cref="JsonException">Thrown when the response cannot be deserialized.</exception>
        IAsyncEnumerable<IActivity> StartConversationAsync(StartRequest startRequest, CancellationToken cancellationToken);

        /// <summary>
        /// Subscribes to an ongoing conversation to receive updates and activities.
        /// This method supports reconnection scenarios by allowing specification of the last received event ID.
        /// </summary>
        /// <param name="conversationId">The conversation ID to subscribe to. Cannot be null.</param>
        /// <param name="lastReceivedEventId">Optional. The ID of the last event received before disconnection. Used for resuming subscriptions.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the subscription.</param>
        /// <returns>An async enumerable stream of subscribe events containing activities and event IDs.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="conversationId"/> is null.</exception>
        /// <exception cref="HttpRequestException">Thrown when the HTTP request to Copilot Studio fails.</exception>
        /// <exception cref="JsonException">Thrown when the response cannot be deserialized.</exception>
        /// <remarks>
        /// This feature is currently available to Microsoft internal developers only.
        /// When reconnecting, provide the last received event ID to resume from that point.
        /// </remarks>
        IAsyncEnumerable<SubscribeEvent> SubscribeAsync(string conversationId, string? lastReceivedEventId, CancellationToken cancellationToken);
    }
}