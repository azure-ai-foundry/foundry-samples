// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;
using System;
using System.Net.Http;

namespace Microsoft.Agents.Authentication.Msal
{
    internal class MSALHttpClientFactory : IMsalHttpClientFactory
    {
        readonly IServiceProvider _systemServiceProvider; 
        public MSALHttpClientFactory(IServiceProvider systemServiceProvider)
        {
            _systemServiceProvider = systemServiceProvider; 
        }

        /// <summary>
        /// Return the HTTP client for MSAL.
        /// </summary>
        /// <returns></returns>
        public HttpClient GetHttpClient()
        {
            var httpClientFactory = _systemServiceProvider.GetService<IHttpClientFactory>() ?? throw new InvalidOperationException("IHttpClientFactory for MSAL service is not registered.");
            HttpClient msalClient = httpClientFactory.CreateClient("MSALClientFactory");
            return msalClient;
        }
    }
}
