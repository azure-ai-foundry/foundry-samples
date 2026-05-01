// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.AspNetCore.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// Defines methods for handling the lifecycle of an HTTP response in a channel-based communication pipeline.
    /// </summary>
    /// <remarks>Implementations of this interface can perform custom processing at different stages of the
    /// HTTP response, such as initialization, activity handling, and finalization. These methods are typically called
    /// in sequence to allow for extensibility and integration with channel-specific logic.</remarks>
    public interface IChannelResponseHandler
    {
        /// <summary>
        /// Initiates the process of sending the HTTP response to the client asynchronously.
        /// </summary>
        /// <param name="httpResponse">The HTTP response to be sent. Must not be null.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation of beginning the HTTP response.</returns>
        Task ResponseBegin(HttpResponse httpResponse, CancellationToken cancellationToken = default);

        /// <summary>
        /// Handles the HTTP response associated with the specified activity.
        /// </summary>
        /// <param name="httpResponse">The HTTP response to process. Cannot be null.</param>
        /// <param name="activity">The activity associated with the HTTP response. Cannot be null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task OnResponse(HttpResponse httpResponse, IActivity activity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes the specified data to the HTTP response and completes the response asynchronously.
        /// </summary>
        /// <param name="httpResponse">The HTTP response to which the data will be written. Cannot be null.</param>
        /// <param name="data">The data to write to the response. The format and serialization depend on the implementation.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation of writing the data and ending the response.</returns>
        Task ResponseEnd(HttpResponse httpResponse, object data, CancellationToken cancellationToken = default);
    }
}
