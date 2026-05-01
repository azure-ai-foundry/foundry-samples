// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Connector.Errors;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Connector.RestClients
{
    internal class AgentSignInRestClient(IRestTransport transport) : IAgentSignIn
    {
        private readonly IRestTransport _transport = transport ?? throw new ArgumentNullException(nameof(_transport));

        /// <inheritdoc/>
        public async Task<string> GetSignInUrlAsync(string state, string codeChallenge = null, string emulatorUrl = null, string finalRedirect = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(state, nameof(state));

            // Special case: requires Accept: text/plain and returns a plain string, not JSON.
            var requestUri = new Uri(_transport.Endpoint.EnsureTrailingSlash(), RestApiPaths.AgentSignIn)
                .AppendQuery("state", state)
                .AppendQuery("code_challenge", codeChallenge)
                .AppendQuery("emulatorUrl", emulatorUrl)
                .AppendQuery("finalRedirect", finalRedirect);

            using var message = new HttpRequestMessage(HttpMethod.Get, requestUri);
            message.Headers.Add("Accept", "text/plain");

            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    return await RestPipeline.ReadAsStringAsync(httpResponse, cancellationToken).ConfigureAwait(false);
                default:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.TokenServiceGetSignInUrlUnexpected, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
            }
        }

        /// <inheritdoc/>
        public async Task<SignInResource> GetSignInResourceAsync(string state, string codeChallenge = null, string emulatorUrl = null, string finalRedirect = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(state, nameof(state));

            var request = RestRequest.Get(RestApiPaths.AgentSignInResource)
                .WithQuery("state", state)
                .WithQuery("code_challenge", codeChallenge)
                .WithQuery("emulatorUrl", emulatorUrl)
                .WithQuery("finalRedirect", finalRedirect);

            using var httpResponse = await RestPipeline.SendRawAsync(_transport, request, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    return await RestPipeline.ReadContentAsync<SignInResource>(httpResponse, cancellationToken).ConfigureAwait(false);
                case 400:
                    throw ErrorResponseException.CreateErrorResponseException(httpResponse, ErrorHelper.GetSignInResourceAsync_BadRequestError, cancellationToken: cancellationToken);
                default:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.TokenServiceGetSignInResourceUnexpected, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
            }
        }
    }
}
