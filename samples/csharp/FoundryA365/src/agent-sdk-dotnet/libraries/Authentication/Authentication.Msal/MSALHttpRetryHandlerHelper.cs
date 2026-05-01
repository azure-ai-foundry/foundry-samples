// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Agents.Authentication.Msal.Model;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Agents.Authentication.Msal
{
    public class MSALHttpRetryHandlerHelper : DelegatingHandler
    {
        private readonly IServiceProvider _systemServiceProvider;
        private readonly int _maxRetryCount = 2;

        public MSALHttpRetryHandlerHelper(IServiceProvider systemServiceProvider)
        {
            _systemServiceProvider = systemServiceProvider;
            _maxRetryCount = _systemServiceProvider.GetService<IOptions<MsalAuthConfigurationOptions>>().Value.MSALRetryCount;
        }

        /// <summary>
        /// Handle failure and retry.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = null;
            for (int i = 0; i < _maxRetryCount; i++)
            {
                response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return response;
                }
                else
                {
                    if (response.StatusCode != System.Net.HttpStatusCode.RequestTimeout)
                    {
                        break;
                    }
                    else
                    {
#if DEBUG
                        System.Diagnostics.Trace.WriteLine($">>> MSAL RETRY ON TIMEOUT >>> {Environment.CurrentManagedThreadId} - {i}");
#endif
                    }
                }
            }
            return response;
        }
    }
}
