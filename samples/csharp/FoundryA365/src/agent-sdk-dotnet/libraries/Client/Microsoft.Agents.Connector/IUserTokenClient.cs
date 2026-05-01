// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Connector
{
    /// <summary>
    /// Client for OAuth token exchange.
    /// </summary>
    public interface IUserTokenClient : IDisposable
    {
        /// <summary>
        /// Attempts to retrieve the token for a user that's in a login flow.
        /// </summary>
        /// <param name="userId">The user id that will be associated with the token.</param>
        /// <param name="connectionName">Name of the auth connection to use.</param>
        /// <param name="channelId">The channel Id that will be associated with the token.</param>
        /// <param name="magicCode">(Optional) Optional user entered code to validate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Token Response.</returns>
        Task<TokenResponse> GetUserTokenAsync(string userId, string connectionName, ChannelId channelId, string magicCode, CancellationToken cancellationToken);

        /// <summary>
        /// Get the raw signin link to be sent to the user for signin for a connection name.
        /// </summary>
        /// <param name="connectionName">Name of the auth connection to use.</param>
        /// <param name="activity">The <see cref="Microsoft.Agents.Core.Models.Activity"/> from which to derive the token exchange state.</param>
        /// <param name="finalRedirect">The final URL that the OAuth flow will redirect to.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A <see cref="System.Threading.Tasks.Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        Task<SignInResource> GetSignInResourceAsync(string connectionName, IActivity activity, string finalRedirect, CancellationToken cancellationToken);

        /// <summary>
        /// Signs the user out with the token server.
        /// </summary>
        /// <param name="userId">The user id that will be associated with the token.</param>
        /// <param name="connectionName">Name of the auth connection to use.</param>
        /// <param name="channelId">The channel Id that will be associated with the token.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> representing the result of the asynchronous operation.</returns>
        Task SignOutUserAsync(string userId, string connectionName, ChannelId channelId, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieves the token status for each configured connection for the given user.
        /// </summary>
        /// <param name="userId">The user id that will be associated with the token.</param>
        /// <param name="channelId">The channel Id that will be associated with the token.</param>
        /// <param name="includeFilter">The includeFilter.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task of Array of TokenStatus.</returns>
        Task<TokenStatus[]> GetTokenStatusAsync(string userId, ChannelId channelId, string includeFilter, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieves Azure Active Directory tokens for particular resources on a configured connection.
        /// </summary>
        /// <param name="userId">The user id that will be associated with the token.</param>
        /// <param name="connectionName">Name of the auth connection to use.</param>
        /// <param name="resourceUrls">The list of resource URLs to retrieve tokens for.</param>
        /// <param name="channelId">The channel Id that will be associated with the token.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task of Dictionary of resourceUrl to the corresponding TokenResponse.</returns>
        Task<Dictionary<string, TokenResponse>> GetAadTokensAsync(string userId, string connectionName, string[] resourceUrls, ChannelId channelId, CancellationToken cancellationToken);

        /// <summary>
        /// Performs a token exchange operation such as for single sign-on.
        /// </summary>
        /// <param name="userId">The user id that will be associated with the token.</param>
        /// <param name="connectionName">Name of the auth connection to use.</param>
        /// <param name="channelId">The channel Id that will be associated with the token.</param>
        /// <param name="exchangeRequest">The exchange request details, either a token to exchange or a uri to exchange.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception>ErrorResponseException</exception>
        Task<TokenResponse> ExchangeTokenAsync(string userId, string connectionName, ChannelId channelId, TokenExchangeRequest exchangeRequest, CancellationToken cancellationToken);

        /// <summary>
        /// Get the token or sign in resource for a user.
        /// </summary>
        /// <param name="connectionName">Name of the auth connection to use.</param>
        /// <param name="activity"></param>
        /// <param name="code">(Optional) Optional user entered code to validate.</param>
        /// <param name="finalRedirect">The final URL that the OAuth flow will redirect to.</param>
        /// <param name="fwdUrl"></param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> representing the result of the asynchronous operation.</returns>
        Task<TokenOrSignInResourceResponse> GetTokenOrSignInResourceAsync(string connectionName, IActivity activity, string code, string finalRedirect = null, string fwdUrl = null, CancellationToken cancellationToken = default);
    }
}
