// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Telemetry.Adapter;
using Microsoft.Agents.Builder.Telemetry.Adapter.Scopes;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    internal class ActivityResponseHandler : IChannelResponseHandler
    {
        private const string ActivityEventTemplate = "event: activity\r\ndata: {0}\r\n\r\n";
        private const string InvokeResponseEventTemplate = "event: invokeResponse\r\ndata: {0}\r\n\r\n";

        public Task ResponseBegin(HttpResponse httpResponse, CancellationToken cancellationToken = default)
        {
            httpResponse.StatusCode = (int) HttpStatusCode.OK;
            httpResponse.ContentType = "text/event-stream";
            return Task.CompletedTask;
        }

        public async Task OnResponse(HttpResponse httpResponse, IActivity activity, CancellationToken cancellationToken = default)
        {
            using var telemetryScope = new ScopeWriteResponse(activity);
            await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes(string.Format(ActivityEventTemplate, ProtocolJsonSerializer.ToJson(activity))), cancellationToken).ConfigureAwait(false);
            await httpResponse.Body.FlushAsync(cancellationToken);
        }

        public async Task ResponseEnd(HttpResponse httpResponse, object data, CancellationToken cancellationToken = default)
        {
            if (data is InvokeResponse invokeResponse)
            {
                await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes(string.Format(InvokeResponseEventTemplate, ProtocolJsonSerializer.ToJson(invokeResponse))), cancellationToken).ConfigureAwait(false);
                await httpResponse.Body.FlushAsync(cancellationToken);
            }
        }
    }
}
