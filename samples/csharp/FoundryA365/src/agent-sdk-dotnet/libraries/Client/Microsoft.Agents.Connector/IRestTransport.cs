// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.Agents.Connector
{
    /// <summary>
    /// Defines the contract for REST transport operations.
    /// </summary>
    public interface IRestTransport
    {
        /// <summary>
        /// Gets the base endpoint URI for REST operations.
        /// </summary>
        Uri Endpoint { get; }
        
        /// <summary>
        /// Gets an HTTP client configured for this transport.
        /// </summary>
        Task<HttpClient> GetHttpClientAsync();
    }
}
