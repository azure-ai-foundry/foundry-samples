// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Connector.RestClients
{
    /// <summary>
    /// Central HTTP send/receive helper used by all inner REST clients.
    /// This is the single location for platform-specific (#if !NETSTANDARD) deserialization logic.
    /// </summary>
    internal static class RestPipeline
    {
        /// <summary>
        /// Sends <paramref name="request"/> and returns the raw <see cref="System.Net.Http.HttpResponseMessage"/>.
        /// The caller is responsible for disposing the response.
        /// Use this for methods that need to branch on specific status codes.
        /// </summary>
        public static async Task<HttpResponseMessage> SendRawAsync(
            IRestTransport transport,
            RestRequest request,
            CancellationToken cancellationToken)
        {
            using var httpClient = await transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpRequest = request.Build(transport.Endpoint);
            return await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        }

#if !NETSTANDARD
        /// <summary>
        /// Deserializes JSON from the response content.
        /// This is the SINGLE location for the #if !NETSTANDARD deserialization pattern.
        /// </summary>
        internal static Task<T> ReadContentAsync<T>(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(ProtocolJsonSerializer.ToObject<T>(response.Content.ReadAsStream(cancellationToken)));
        }
#else
        /// <summary>
        /// Deserializes JSON from the response content.
        /// This is the SINGLE location for the #if !NETSTANDARD deserialization pattern.
        /// </summary>
        internal static async Task<T> ReadContentAsync<T>(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
        {
            return ProtocolJsonSerializer.ToObject<T>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        }
#endif

        /// <summary>
        /// Reads response content as a string.
        /// This is the SINGLE location for the #if !NETSTANDARD string-reading pattern.
        /// </summary>
        internal static async Task<string> ReadAsStringAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
        {
#if !NETSTANDARD
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        }
    }
}
