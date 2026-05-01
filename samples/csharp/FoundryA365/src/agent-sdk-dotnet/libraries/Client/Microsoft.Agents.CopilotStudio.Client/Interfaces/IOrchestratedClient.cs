// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.CopilotStudio.Client.Models;
using System.Collections.Generic;
using System.Threading;

namespace Microsoft.Agents.CopilotStudio.Client
{
    /// <summary>
    /// Defines the contract for a client that connects to the ExternalOrchestration API endpoint for Copilot Studio.
    /// Supports starting conversations, invoking tools, handling user responses, and sending conversation updates
    /// in an externally orchestrated flow.
    /// </summary>
    public interface IOrchestratedClient
    {
        /// <summary>
        /// Starts a new externally orchestrated conversation.
        /// </summary>
        /// <param name="conversationId">The conversation ID for the new conversation.</param>
        /// <param name="requestProperties">Optional properties mapped to HTTP request headers.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>An async enumerable stream of orchestrated responses from the copilot.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="conversationId"/> is null or empty.</exception>
        /// <exception cref="System.Net.Http.HttpRequestException">Thrown when the HTTP request fails.</exception>
        /// <exception cref="System.Text.Json.JsonException">Thrown when the response cannot be deserialized.</exception>
        IAsyncEnumerable<OrchestratedResponse> StartConversationAsync(string conversationId, RequestProperties? requestProperties = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invokes a tool (topic) in an externally orchestrated conversation.
        /// </summary>
        /// <param name="conversationId">The conversation ID.</param>
        /// <param name="toolInputs">The tool invocation input containing the tool schema name and parameters.</param>
        /// <param name="activity">An optional activity to include with the tool invocation.</param>
        /// <param name="requestProperties">Optional properties mapped to HTTP request headers.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>An async enumerable stream of orchestrated responses from the copilot.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="conversationId"/> or <paramref name="toolInputs"/> is null.</exception>
        /// <exception cref="System.Net.Http.HttpRequestException">Thrown when the HTTP request fails.</exception>
        /// <exception cref="System.Text.Json.JsonException">Thrown when the response cannot be deserialized.</exception>
        IAsyncEnumerable<OrchestratedResponse> InvokeToolAsync(string conversationId, ToolInvocationInput toolInputs, IActivity? activity = null, RequestProperties? requestProperties = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Forwards a user response to an in-progress externally orchestrated conversation.
        /// </summary>
        /// <param name="conversationId">The conversation ID.</param>
        /// <param name="activity">The user activity to forward.</param>
        /// <param name="requestProperties">Optional properties mapped to HTTP request headers.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>An async enumerable stream of orchestrated responses from the copilot.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="conversationId"/> or <paramref name="activity"/> is null.</exception>
        /// <exception cref="System.Net.Http.HttpRequestException">Thrown when the HTTP request fails.</exception>
        /// <exception cref="System.Text.Json.JsonException">Thrown when the response cannot be deserialized.</exception>
        IAsyncEnumerable<OrchestratedResponse> HandleUserResponseAsync(string conversationId, IActivity activity, RequestProperties? requestProperties = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a conversation update event to the bot in an externally orchestrated conversation.
        /// </summary>
        /// <param name="conversationId">The conversation ID.</param>
        /// <param name="requestProperties">Optional properties mapped to HTTP request headers.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>An async enumerable stream of orchestrated responses from the copilot.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="conversationId"/> is null or empty.</exception>
        /// <exception cref="System.Net.Http.HttpRequestException">Thrown when the HTTP request fails.</exception>
        /// <exception cref="System.Text.Json.JsonException">Thrown when the response cannot be deserialized.</exception>
        IAsyncEnumerable<OrchestratedResponse> ConversationUpdateAsync(string conversationId, RequestProperties? requestProperties = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes an orchestrated turn with a custom request.
        /// This is the low-level method that all other methods delegate to.
        /// </summary>
        /// <param name="conversationId">The conversation ID.</param>
        /// <param name="request">The orchestrated turn request to send.</param>
        /// <param name="requestProperties">Optional properties mapped to HTTP request headers.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>An async enumerable stream of orchestrated responses from the copilot.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="conversationId"/> or <paramref name="request"/> is null.</exception>
        /// <exception cref="System.Net.Http.HttpRequestException">Thrown when the HTTP request fails.</exception>
        /// <exception cref="System.Text.Json.JsonException">Thrown when the response cannot be deserialized.</exception>
        IAsyncEnumerable<OrchestratedResponse> ExecuteTurnAsync(string conversationId, OrchestratedTurnRequest request, RequestProperties? requestProperties = null, CancellationToken cancellationToken = default);
    }
}
