// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Threading.Channels;
using System.Collections.Concurrent;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// Producer/consumer queue to read Activities sent by the Adapter during asynchronous background requests such as
    /// DeliveryMode.Stream/ExpectReplies.
    /// </summary>
    /// <remarks>
    /// StartHandlerForRequest and HandleResponsesAsync are called from the request thread.  SendActivitiesAsync would
    /// ultimately be called from the background thread as Activities are sent through Adapter. CompleteHandlerForRequest
    /// is used to signal the queue for the request is complete and not further Activities will be queued.
    /// </remarks>
    public class ChannelResponseQueue(ILogger logger)
    {
        private readonly ConcurrentDictionary<string, ChannelInfo> _conversations = new();

        /// <summary>
        /// Processes queued responses.  This blocks until CompleteHandlerForRequest is called.
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="action">Async action to call when an Activity is received.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task HandleResponsesAsync(string requestId, Func<IActivity, Task> action, CancellationToken cancellationToken)
        {
            if (_conversations.TryGetValue(requestId, out var channelInfo))
            {
                try
                {
                    while (await channelInfo.channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        var activity = await channelInfo.channel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                        await action(activity).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Request was cancelled (e.g. client disconnected). Drain any remaining items and clean up.
                    logger.LogWarning("ChannelResponseQueue: HandleResponsesAsync cancelled for requestId '{RequestId}'.", requestId);
                }
                finally
                {
                    channelInfo.readDone.Release();
                }
            }
            else
            {
                logger.LogWarning("ChannelResponseQueue received unknown requestId '{RequestId}' in HandleResponsesAsync.", requestId);
            }
        }

        /// <summary>
        /// Starts the queue for a request.  This MUST be called before background processing starts.
        /// </summary>
        /// <param name="requestId"></param>
        public void StartHandlerForRequest(string requestId)
        {
            _conversations.GetOrAdd(requestId, new ChannelInfo());
        }

        /// <summary>
        /// Completes channel response handling synchronously. Blocks until all reads complete.
        /// Prefer <see cref="Microsoft.Agents.Hosting.AspNetCore.ChannelResponseQueue.CompleteHandlerForRequestAsync(string)"/> when calling from an async context.
        /// </summary>
        /// <param name="requestId"></param>
        public void CompleteHandlerForRequest(string requestId)
        {
            if (_conversations.TryGetValue(requestId, out var channelInfo))
            {
                if (channelInfo.channel.Writer.TryComplete())
                {
                    // Wait for HandleResponsesAsync to finish BEFORE removing the entry.
                    // Removing first would cause HandleResponsesAsync to find no entry and skip reading.
                    channelInfo.readDone.Wait();
                    _conversations.Remove(requestId, out _);
                    channelInfo.readDone.Dispose();
                }
            }
        }

        /// <summary>
        /// Completes channel response handling asynchronously. Waits without blocking a thread pool thread.
        /// </summary>
        /// <param name="requestId"></param>
        public async Task CompleteHandlerForRequestAsync(string requestId)
        {
            if (_conversations.TryGetValue(requestId, out var channelInfo))
            {
                if (channelInfo.channel.Writer.TryComplete())
                {
                    // Wait for HandleResponsesAsync to finish BEFORE removing the entry.
                    // Removing first would cause HandleResponsesAsync to find no entry and skip reading.
                    await channelInfo.readDone.WaitAsync().ConfigureAwait(false);
                    _conversations.Remove(requestId, out _);
                    channelInfo.readDone.Dispose();
                }
            }
        }

        /// <summary>
        /// Called by the background processing to queue Activities.  This will signal HandleResponsesAsync to
        /// process the Activities.
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="activities"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task SendActivitiesAsync(string requestId, IActivity[] activities, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(requestId) || !_conversations.TryGetValue(requestId, out var channelInfo))
            {
                logger.LogWarning("ChannelResponseQueue received unknown requestId '{RequestId}' for Activities: {Activity}", requestId, ProtocolJsonSerializer.ToJson(activities));
            }
            else
            {
                foreach (var activity in activities)
                {
                    try
                    {
                        // Write the Activity to the Channel.  It is consumed on the other side via HandleResponses.
                        await channelInfo.channel.Writer.WriteAsync(activity, cancellationToken).ConfigureAwait(false);
                    }
                    catch (ChannelClosedException)
                    {
                        // The channel was completed (e.g. due to cancellation). Stop writing.
                        logger.LogWarning("ChannelResponseQueue: Channel closed for requestId '{RequestId}'. Remaining activities will not be sent.", requestId);
                        break;
                    }
                }
            }
        }
    }

    sealed class ChannelInfo
    {
        public SemaphoreSlim readDone = new(0, 1);
        public Channel<IActivity> channel = Channel.CreateUnbounded<IActivity>();
    }
}
