// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Client.Tests
{
    class TestHttpMessageHandler : HttpMessageHandler
    {
        private int _sendRequest = 0;

        public HttpResponseMessage HttpResponseMessage { get; set; }

        public Action<IActivity, int> SendAssert { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _sendRequest++;

            if (SendAssert != null)
            {
#if !NETFRAMEWORK
                var activity = ProtocolJsonSerializer.ToObject<Activity>(request.Content.ReadAsStream());
#else
                var activity = ProtocolJsonSerializer.ToObject<Activity>(request.Content.ReadAsStreamAsync().Result);
#endif
                SendAssert(activity, _sendRequest);
            }

            return Task.FromResult(HttpResponseMessage);
        }
    }
}
