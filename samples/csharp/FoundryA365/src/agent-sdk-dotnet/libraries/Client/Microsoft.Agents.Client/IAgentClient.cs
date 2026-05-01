// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Client
{
    public interface IAgentClient : IDisposable
    {
        string Name { get; }

        /// <summary>
        /// Sends an Activity with DeliveryMode "normal" or "expectReplies".  For `normal`, this would require handling of async replies via IChannelApiHandler via ChannelApiController.
        /// </summary>
        /// <remarks>This is a rather base level of functionality and in most cases <see cref="Microsoft.Agents.Client.IAgentClient.SendActivityForResultAsync"/> is easier to use.</remarks>
        /// <param name="agentConversationId">Agent conversation identifier.</param>
        /// <param name="activity">Activity to send.</param>
        /// <param name="relatesTo">What the activity relates to.</param>
        /// <param name="useAnonymous">Specify an anonymous user.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that represents the response of the async send.</returns>
        Task<InvokeResponse<T>> SendActivityAsync<T>(string agentConversationId, IActivity activity, IActivity relatesTo = null, bool useAnonymous = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends an Activity with DeliveryMode "normal" or "expectReplies". Convenience method when a result is not expected.
        /// </summary>
        /// <param name="agentConversationId">Agent conversation identifier.</param>
        /// <param name="activity">Activity to send.</param>
        /// <param name="relatesTo">What the activity relates to.</param>
        /// <param name="useAnonymous">Specify an anonymous user.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that represents the async send operation.</returns>
        Task SendActivityAsync(string agentConversationId, IActivity activity, IActivity relatesTo = null, bool useAnonymous = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Send an Activity with streaming replies.
        /// </summary>
        /// <remarks>
        /// This method will handle EndOfConversation Value and InvokeResponse.Body return values, specified by T.
        /// </remarks>
        /// <typeparam name="T"></typeparam>
        /// <param name="agentConversationId">Agent conversation identifier.</param>
        /// <param name="activity">Activity to send.</param>
        /// <param name="handler">Handler to process streamed activity responses.</param>
        /// <param name="relatesTo">What the activity relates to.</param>
        /// <param name="useAnonymous">Specify an anonymous user.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that represents the async streamed send operation.</returns>
        Task<StreamResponse<T>> SendActivityStreamedAsync<T>(string agentConversationId, IActivity activity, Action<IActivity> handler, IActivity relatesTo = null, bool useAnonymous = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Send an Activity with streaming replies.
        /// </summary>
        /// <param name="agentConversationId">Agent conversation identifier.</param>
        /// <param name="activity">Activity to send.</param>
        /// <param name="relatesTo">What the activity relates to.</param>
        /// <param name="useAnonymous">Specify an anonymous user.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Enumerator for the responses returned from the async streamed send operation.</returns>
        IAsyncEnumerable<object> SendActivityStreamedAsync(string agentConversationId, IActivity activity, IActivity relatesTo = null, bool useAnonymous = false, CancellationToken cancellationToken = default);
    }
}
