// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.UserAuth
{
    /// <summary>
    /// Handles user OAuth flows.
    /// </summary>
    public interface IUserAuthorization
    {
        string Name { get; }

        /// <summary>
        /// Signs in a user.
        /// This is called by AgentApplication each turn when OAuth is active."/>
        /// </summary>
        /// <param name="context">Current turn context.</param>
        /// <param name="forceSignIn"></param>
        /// <param name="exchangeConnection">if null, OAuthSettings are used.</param>
        /// <param name="exchangeScopes">if null, OAuthSettings are used.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The token if the exchange was successful. Otherwise returns null.</returns>
        Task<TokenResponse> SignInUserAsync(ITurnContext context, bool forceSignIn = false, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Signs out a user.
        /// </summary>
        /// <param name="turnContext">Current turn context.</param>
        /// <param name="state">AgentApplication state.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        Task SignOutUserAsync(ITurnContext turnContext, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resets the sign in flow state.
        /// </summary>
        /// <param name="turnContext"></param>
        /// <param name="turnState"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task ResetStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a refreshed user token.
        /// </summary>
        /// <param name="turnContext">Current turn context.</param>
        /// <param name="exchangeConnection">if null, OAuthSettings are used.</param>
        /// <param name="exchangeScopes">if null, OAuthSettings are used.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The token if the exchange was successful. Otherwise returns null.</returns>
        Task<TokenResponse> GetRefreshedUserTokenAsync(ITurnContext turnContext, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default);
    }
}
