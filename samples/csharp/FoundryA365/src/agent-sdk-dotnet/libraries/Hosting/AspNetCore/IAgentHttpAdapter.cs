// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Agents.Builder;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// An Adapter that handles Http request/response.
    /// </summary>
    public interface IAgentHttpAdapter
    {
        /// <summary>
        /// This method can be called from inside a POST method on any endpoint implementation.
        /// </summary>
        /// <param name="httpRequest">The HTTP request object, typically in a POST handler.</param>
        /// <param name="httpResponse">The HTTP response object.</param>
        /// <param name="agent">The Agent implementation.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        Task ProcessAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, CancellationToken cancellationToken = default);
    }
}
