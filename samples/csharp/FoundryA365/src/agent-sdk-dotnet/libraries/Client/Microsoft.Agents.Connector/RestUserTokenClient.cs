// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Connector.RestClients;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Agents.Core;

namespace Microsoft.Agents.Connector
{
    /// <summary>
    /// Client for OAuth flow using the TokenAPI endpoints.  This uses the Azure Bot Service Token Service to facilitate the token exchange.
    /// </summary>
    public class RestUserTokenClient : RestClientBase, IUserTokenClient, IDisposable
    {
        private readonly string _appId;
        private readonly UserTokenRestClient _userTokenClient;
        private readonly AgentSignInRestClient _agentSignInClient;
        private readonly ILogger _logger;
        private bool _disposed;

        public Uri BaseUri => Endpoint;

        public RestUserTokenClient(string appId, Uri endpoint, IHttpClientFactory httpClientFactory, Func<Task<string>> tokenProviderFunction, string namedClient = nameof(RestUserTokenClient), ILogger logger = null)
            : base(endpoint, httpClientFactory, namedClient, tokenProviderFunction)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(appId, nameof(appId));

            _appId = appId;

            _userTokenClient = new UserTokenRestClient(this);
            _agentSignInClient = new AgentSignInRestClient(this);
            _logger = logger ?? NullLogger.Instance;
        }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);

            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        /// <inheritdoc />
        public async Task<TokenResponse> GetUserTokenAsync(string userId, string connectionName, ChannelId channelId, string magicCode, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfObjectDisposed(_disposed, nameof(GetUserTokenAsync));
            AssertionHelpers.ThrowIfNullOrEmpty(userId, nameof(userId));
            AssertionHelpers.ThrowIfNullOrEmpty(connectionName, nameof(connectionName));

            _logger.LogInformation("GetTokenAsync ConnectionName: {connectionName}", connectionName);
            return await _userTokenClient.GetTokenAsync(userId, connectionName, channelId, magicCode, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<SignInResource> GetSignInResourceAsync(string connectionName, IActivity activity, string finalRedirect, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfObjectDisposed(_disposed, nameof(GetSignInResourceAsync));
            AssertionHelpers.ThrowIfNullOrEmpty(connectionName, nameof(connectionName));
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));

            _logger.LogInformation("GetSignInResourceAsync ConnectionName: {connectionName}", connectionName);
            var state = CreateTokenExchangeState(_appId, connectionName, activity);
            return await _agentSignInClient.GetSignInResourceAsync(state, null, null, finalRedirect, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task SignOutUserAsync(string userId, string connectionName, ChannelId channelId, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfObjectDisposed(_disposed, nameof(SignOutUserAsync));
            AssertionHelpers.ThrowIfNullOrEmpty(userId, nameof(userId));
            AssertionHelpers.ThrowIfNullOrEmpty(connectionName, nameof(connectionName));

            _logger.LogInformation("SignOutAsync ConnectionName: {connectionName}", connectionName);
            await _userTokenClient.SignOutAsync(userId, connectionName, channelId, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<TokenStatus[]> GetTokenStatusAsync(string userId, ChannelId channelId, string includeFilter, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfObjectDisposed(_disposed, nameof(GetTokenStatusAsync));
            AssertionHelpers.ThrowIfNullOrEmpty(userId, nameof(userId));
            AssertionHelpers.ThrowIfNullOrEmpty(channelId, nameof(channelId));

            _logger.LogInformation("GetTokenStatusAsync");
            var result = await _userTokenClient.GetTokenStatusAsync(userId, channelId, includeFilter, cancellationToken).ConfigureAwait(false);
            return result?.ToArray();
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, TokenResponse>> GetAadTokensAsync(string userId, string connectionName, string[] resourceUrls, ChannelId channelId, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfObjectDisposed(_disposed, nameof(GetAadTokensAsync));
            AssertionHelpers.ThrowIfNullOrEmpty(userId, nameof(userId));
            AssertionHelpers.ThrowIfNullOrEmpty(connectionName, nameof(connectionName));

            _logger.LogInformation("GetAadTokensAsync ConnectionName: {connectionName}", connectionName);
            return (Dictionary<string, TokenResponse>)await _userTokenClient.GetAadTokensAsync(userId, connectionName, new AadResourceUrls() { ResourceUrls = resourceUrls?.ToList() }, channelId, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<TokenResponse> ExchangeTokenAsync(string userId, string connectionName, ChannelId channelId, TokenExchangeRequest exchangeRequest, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfObjectDisposed(_disposed, nameof(ExchangeTokenAsync));
            AssertionHelpers.ThrowIfNullOrEmpty(userId, nameof(userId));
            AssertionHelpers.ThrowIfNullOrEmpty(connectionName, nameof(connectionName));

            _logger.LogInformation("ExchangeAsync ConnectionName: {connectionName}", connectionName);
            return await _userTokenClient.ExchangeAsync(userId, connectionName, channelId, exchangeRequest, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<TokenOrSignInResourceResponse> GetTokenOrSignInResourceAsync(string connectionName, IActivity activity, string code, string finalRedirect = null, string fwdUrl = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfObjectDisposed(_disposed, nameof(GetTokenOrSignInResourceAsync));
            AssertionHelpers.ThrowIfNullOrEmpty(connectionName, nameof(connectionName));
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));

            _logger.LogInformation("GetTokenOrSignInResourceAsync ConnectionName: {connectionName}", connectionName);
            var state = CreateTokenExchangeState(_appId, connectionName, activity);
            return await _userTokenClient.GetTokenOrSignInResourceAsync(activity.From.Id, connectionName, activity.ChannelId, state, code, finalRedirect, fwdUrl, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Helper function to create the base64 encoded token exchange state used in GetSignInResourceAsync calls.
        /// </summary>
        /// <param name="appId">The appId to include in the token exchange state.</param>
        /// <param name="connectionName">The connectionName to include in the token exchange state.</param>
        /// <param name="activity">The <see cref="Microsoft.Agents.Core.Models.IActivity"/> from which to derive the token exchange state.</param>
        /// <returns>base64 encoded token exchange state.</returns>
        public static string CreateTokenExchangeState(string appId, string connectionName, IActivity activity)
        {
            var tokenExchangeState = new TokenExchangeState
            {
                ConnectionName = connectionName,
                Conversation = activity.GetConversationReference(),
                RelatesTo = activity.RelatesTo,
                MsAppId = appId,
            };
            var json = ProtocolJsonSerializer.ToJson(tokenExchangeState);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }
    }
}
