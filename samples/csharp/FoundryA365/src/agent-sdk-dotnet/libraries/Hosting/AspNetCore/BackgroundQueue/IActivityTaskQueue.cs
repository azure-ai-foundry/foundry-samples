// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue
{
    /// <summary>
    /// Interface for a class used to transfer an ActivityWithClaims to the <see cref="Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue.HostedActivityService"/>.
    /// </summary>
    public interface IActivityTaskQueue
    {
        /// <summary>
        /// Enqueue an Activity, with Claims, to be processed on a background thread.
        /// before enqueueing.
        /// </summary>
        /// <remarks>It is assumed these Claims have been authenticated via JwtTokenValidation.AuthenticateRequest.</remarks>
        /// <param name="claimsIdentity">Authenticated <see cref="System.Security.Claims.ClaimsIdentity"/> used to process the
        /// activity.</param>
        /// <param name="adapter"></param>
        /// <param name="activity"><see cref="Microsoft.Agents.Core.Models.Activity"/> to be processed.</param>
        /// <param name="proactive"></param>
        /// <param name="proactiveAudience"></param>
        /// <param name="agentType"></param>
        /// <param name="onComplete"></param>
        /// <param name="headers">Headers used for the current <see cref="Microsoft.Agents.Core.Models.Activity"/> request.</param>
        /// <returns>true if the item was queued.  false would indicate the queue has been disabled (probably shutting down).</returns>
        bool QueueBackgroundActivity(ClaimsIdentity claimsIdentity, IChannelAdapter adapter, IActivity activity, bool proactive = false, string proactiveAudience = null, Type agentType = null, Func<InvokeResponse, Task> onComplete = null, IHeaderDictionary headers = null);

        /// <summary>
        /// Wait for a signal of an enqueued Activity with Claims to be processed.
        /// </summary>
        /// <param name="cancellationToken">CancellationToken used to cancel the wait.</param>
        /// <returns>An ActivityWithClaims to be processed.</returns>
        /// <remarks>It is assumed these claims have already been authenticated.</remarks>
        Task<ActivityWithClaims> WaitForActivityAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Stops the operation, optionally waiting for all pending items to be processed before completing.
        /// </summary>
        /// <param name="waitForEmpty">true to wait until all pending items have been processed before stopping; otherwise, false to stop
        /// immediately.</param>
        void Stop(bool waitForEmpty = true);
    }
}
