// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Connector.Errors;
using Microsoft.Agents.Connector.Telemetry.Scopes;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Core.Telemetry;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Connector.RestClients
{
    internal class UserTokenRestClient : IUserToken
    {
        private readonly IRestTransport _transport;
        private readonly ILogger _logger;
        private static readonly MemoryCache _cache = new MemoryCache(nameof(UserTokenRestClient));

        public UserTokenRestClient(IRestTransport transport, ILogger logger = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<TokenResponse> ExchangeAsync(string userId, string connectionName, ChannelId channelId, TokenExchangeRequest exchangeRequest, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(userId, nameof(userId));
            AssertionHelpers.ThrowIfNullOrEmpty(connectionName, nameof(connectionName));
            AssertionHelpers.ThrowIfNullOrEmpty(channelId, nameof(channelId));
            AssertionHelpers.ThrowIfNull(exchangeRequest, nameof(exchangeRequest));

            var request = RestRequest.Post(RestApiPaths.UserTokenExchange)
                .WithQuery("userId", userId)
                .WithQuery("connectionName", connectionName)
                .WithQuery("channelId", channelId?.Channel)
                .WithBody(exchangeRequest);

            using var httpResponse = await RestPipeline.SendRawAsync(_transport, request, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    var tokenResponse = await RestPipeline.ReadContentAsync<TokenResponse>(httpResponse, cancellationToken).ConfigureAwait(false);
                    if (tokenResponse?.Token != null)
                    {
                        AddTokenResponseToCache(CacheKey(userId, connectionName, channelId.Channel), tokenResponse);
                    }
                    return tokenResponse;

                // Consent Required
                case 400:
                    ErrorResponse errorBody = await RestPipeline.ReadContentAsync<ErrorResponse>(httpResponse, cancellationToken).ConfigureAwait(false);
                    errorBody.Error.Code = Error.ConsentRequiredCode;
                    throw new ErrorResponseException($"({errorBody.Error.Code}) {errorBody.Error.Message}") { Body = errorBody };

                // Unclear what this means
                case 404:
                    var json404 = await RestPipeline.ReadAsStringAsync(httpResponse, cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrEmpty(json404))
                    {
                        return null;
                    }
                    return ProtocolJsonSerializer.ToObject<TokenResponse>(json404);

                // Normal when OAuth Connection config is wrong
                case 500:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.TokenServiceExchangeFailed, cancellationToken, connectionName);

                // Unknown
                default:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.TokenServiceExchangeUnexpected, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
            }
        }

        private static string CacheKey(string userId, string connectionName, ChannelId channelId)
        {
            return $"{userId}-{connectionName}-{channelId?.Channel}";
        }

        /// <inheritdoc/>
        public async Task<TokenResponse> GetTokenAsync(string userId, string connectionName, ChannelId channelId = null, string code = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(userId, nameof(userId));
            AssertionHelpers.ThrowIfNullOrEmpty(connectionName, nameof(connectionName));

            using var telemetryScope = new ScopeGetToken(connectionName, userId, channelId?.Channel ?? TelemetryUtils.Unknown);

            var cacheKey = CacheKey(userId, connectionName, channelId);
            if (string.IsNullOrEmpty(code))
            {
                var cachedTokenResponse = GetTokenResponseFromCache(cacheKey);
                if (cachedTokenResponse != null)
                {
                    return cachedTokenResponse;
                }
            }

            var request = RestRequest.Get(RestApiPaths.UserToken)
                .WithQuery("userId", userId)
                .WithQuery("connectionName", connectionName)
                .WithQuery("channelId", channelId?.Channel)
                .WithQuery("code", code);

            using var httpResponse = await RestPipeline.SendRawAsync(_transport, request, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    var json = await RestPipeline.ReadAsStringAsync(httpResponse, cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrEmpty(json))
                    {
                        return null;
                    }
                    var response = ProtocolJsonSerializer.ToObject<TokenResponse>(json);
                    AddTokenResponseToCache(cacheKey, response);
                    return response;

                case 404:
                    // there isn't a body provided in this case. This can happen when the code is invalid.
                    return null;

                default:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.TokenServiceGetTokenUnexpected, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
            }
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyDictionary<string, TokenResponse>> GetAadTokensAsync(string userId, string connectionName, AadResourceUrls aadResourceUrls, ChannelId channelId = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(userId, nameof(userId));
            AssertionHelpers.ThrowIfNullOrEmpty(connectionName, nameof(connectionName));

            using var telemetryScope = new ScopeGetAadTokens(connectionName, userId, channelId?.Channel ?? TelemetryUtils.Unknown);

            var request = RestRequest.Post(RestApiPaths.UserTokenAad)
                .WithQuery("userId", userId)
                .WithQuery("connectionName", connectionName)
                .WithQuery("channelId", channelId?.Channel)
                .WithBody(aadResourceUrls);

            using var httpResponse = await RestPipeline.SendRawAsync(_transport, request, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    return await RestPipeline.ReadContentAsync<IReadOnlyDictionary<string, TokenResponse>>(httpResponse, cancellationToken).ConfigureAwait(false);
                default:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.TokenServiceGetAadTokenUnexpected, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
            }
        }

        /// <inheritdoc/>
        public async Task<object> SignOutAsync(string userId, string connectionName, ChannelId channelId, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(userId, nameof(userId));

            using var telemetryScope = new ScopeSignOut(connectionName, userId, channelId?.Channel ?? TelemetryUtils.Unknown);

            _cache.Remove(CacheKey(userId, connectionName, channelId));

            var request = RestRequest.Delete(RestApiPaths.UserTokenSignOut)
                .WithQuery("userId", userId)
                .WithQuery("connectionName", connectionName)
                .WithQuery("channelId", channelId?.Channel);

            using var httpResponse = await RestPipeline.SendRawAsync(_transport, request, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    return await RestPipeline.ReadContentAsync<object>(httpResponse, cancellationToken).ConfigureAwait(false);
                case 204:
                    return null;
                default:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.TokenServiceSignOutUnexpected, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
            }
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<TokenStatus>> GetTokenStatusAsync(string userId, ChannelId channelId = null, string include = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(userId, nameof(userId));

            using var telemetryScope = new ScopeGetTokenStatus(userId, channelId?.Channel ?? TelemetryUtils.Unknown);

            var request = RestRequest.Get(RestApiPaths.UserTokenStatus)
                .WithQuery("userId", userId)
                .WithQuery("channelId", channelId?.Channel)
                .WithQuery("include", include);

            using var httpResponse = await RestPipeline.SendRawAsync(_transport, request, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    return await RestPipeline.ReadContentAsync<IReadOnlyList<TokenStatus>>(httpResponse, cancellationToken).ConfigureAwait(false);
                default:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.TokenServiceGetTokenStatusUnexpected, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
            }
        }

        /// <inheritdoc/>
        public async Task<TokenOrSignInResourceResponse> GetTokenOrSignInResourceAsync(string userId, string connectionName, ChannelId channelId, string state, string code = default, string finalRedirect = default, string fwdUrl = default, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(userId, nameof(userId));
            AssertionHelpers.ThrowIfNullOrEmpty(connectionName, nameof(connectionName));
            AssertionHelpers.ThrowIfNullOrEmpty(channelId, nameof(channelId));
            AssertionHelpers.ThrowIfNullOrEmpty(state, nameof(state));

            using var telemetryScope = new ScopeGetTokenOrSignInResource(connectionName, userId, channelId?.Channel ?? TelemetryUtils.Unknown);

            var cacheKey = CacheKey(userId, connectionName, channelId);
            if (string.IsNullOrEmpty(code))
            {
                var cachedTokenResponse = GetTokenResponseFromCache(cacheKey);
                if (cachedTokenResponse != null)
                {
                    return new TokenOrSignInResourceResponse() { TokenResponse = cachedTokenResponse };
                }
            }

            var request = RestRequest.Get(RestApiPaths.UserTokenOrSignInResource)
                .WithQuery("userId", userId)
                .WithQuery("connectionName", connectionName)
                .WithQuery("channelId", channelId?.Channel)
                .WithQuery("code", code)
                .WithQuery("state", state)
                .WithQuery("finalRedirect", finalRedirect)
                .WithQuery("fwdUrl", fwdUrl);

            using var httpResponse = await RestPipeline.SendRawAsync(_transport, request, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    var json = await RestPipeline.ReadAsStringAsync(httpResponse, cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrEmpty(json))
                    {
                        return null;
                    }
                    var tokenOrSignIn = ProtocolJsonSerializer.ToObject<TokenOrSignInResourceResponse>(json);
                    AddTokenResponseToCache(cacheKey, tokenOrSignIn.TokenResponse);
                    return tokenOrSignIn;

                case 404:
                    return null;

                default:
                    throw RestClientExceptionHelper.CreateErrorResponseException(httpResponse, ErrorHelper.TokenServiceGetTokenOrSignInResourceUnexpected, cancellationToken, ((int)httpResponse.StatusCode).ToString(), httpResponse.StatusCode.ToString());
            }
        }

        private static TokenResponse GetTokenResponseFromCache(string cacheKey)
        {
            var value = _cache.Get(cacheKey);
            if (value != null)
            {
                // Token Service will renew within 5 minutes of expiration. Return the cached token
                // if there is more than that. Otherwise, remove it from the cache and return null;
                // this will result in a call to the Token Service to get a new token.

                var toExpiration = ((TokenResponse)value).Expiration - DateTimeOffset.UtcNow;
                if (toExpiration?.TotalMinutes >= 5)
                {
                    return (TokenResponse)value;
                }

                _cache.Remove(cacheKey);
            }

            return null;
        }

        private void AddTokenResponseToCache(string cacheKey, TokenResponse tokenResponse)
        {
            if (tokenResponse != null && tokenResponse.Token != null)
            {
                try
                {
                    var jwtToken = new JwtSecurityToken(tokenResponse.Token);
                    if (tokenResponse.Expiration == null)
                    {
                        // It's usually the case that the TokenResponse will NOT include an expiration value,
                        // in which case we will use the JWT token expiration value.
                        tokenResponse.Expiration = jwtToken.ValidTo;
                    }
                    tokenResponse.IsExchangeable = AgentClaims.IsExchangeableToken(jwtToken);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to parse JWT token for cache; IsExchangeable set to false.");
                    tokenResponse.IsExchangeable = false;
                }

                // If the TokenResponse doesn't contain an expiration value then expiration calcs
                // won't be available to callers.  But the token can otherwise be used.  However,
                // we'll skip caching for now.
                if (tokenResponse.Expiration != null)
                {
                    _cache.Add(
                        new CacheItem(cacheKey) { Value = tokenResponse },
                        new CacheItemPolicy()
                        {
                            SlidingExpiration = TimeSpan.FromMinutes(5)
                        });
                }
            }
        }
    }
}
