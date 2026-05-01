// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Telemetry.Adapter.Scopes;
using Microsoft.Agents.Core.Models;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    internal class ExpectRepliesResponseWriter(IActivity incomingActivity) : IChannelResponseHandler
    {
        private readonly ExpectedReplies _expectedReplies = new();

        public Task ResponseBegin(HttpResponse httpResponse, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task OnResponse(HttpResponse httpResponse, IActivity activity, CancellationToken cancellationToken = default)
        {
            if (incomingActivity.DeliveryMode == DeliveryModes.ExpectReplies)
            {
                _expectedReplies.Activities.Add(activity);
            }
            return Task.CompletedTask;
        }

        public async Task ResponseEnd(HttpResponse httpResponse, object data, CancellationToken cancellationToken = default)
        {
            using var telemetryScope = new ScopeWriteResponse(_expectedReplies.Activities);
            if (data is InvokeResponse invokeResponse)
            {
                if (incomingActivity.DeliveryMode == DeliveryModes.ExpectReplies)
                {
                    // The case for Invoke with ExpectReplies
                    _expectedReplies.Body = invokeResponse.Body; 
                    invokeResponse = new InvokeResponse()
                    {
                        Status = invokeResponse.Status,
                        Body = _expectedReplies
                    };
                }
            }
            else
            {
                // This would be the case for ExpectReplies on a non-Invoke
                invokeResponse = new InvokeResponse()
                {
                    Status = (int)HttpStatusCode.OK,
                    Body = _expectedReplies
                };
            }


            await HttpHelper.WriteResponseAsync(httpResponse, invokeResponse).ConfigureAwait(false);
        }
    }
}
