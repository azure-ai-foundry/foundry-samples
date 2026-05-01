// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using System.Threading;
using System.Security.Claims;
using System.Collections.Generic;
using Microsoft.Agents.Connector;
using System;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Handles creation of IConnectorClient and IUserTokenClient objects for use when handling incoming Activities.
    /// </summary>
    /// <remarks>This is not something normally used or implemented by an Agent developer.  Rather it is used
    /// internally by the IChannelAdapter to create the clients need to communicate back the sender.</remarks>
    public interface IChannelServiceClientFactory
    {
        /// <summary>
        /// Creates a <see cref="IConnectorClient"/> that can be used to create <see cref="IConnectorClient"/>.
        /// </summary>
        /// <param name="claimsIdentity">The inbound <see cref="Activity"/>'s <see cref="ClaimsIdentity"/>.</param>
        /// <param name="serviceUrl">The service URL.</param>
        /// <param name="audience">An optional audience identifier for which the connector client is created. If null, the default audience is
        /// used.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <param name="scopes">The scopes to request.</param>
        /// <param name="useAnonymous">Whether to use anonymous credentials.</param>
        /// <returns>A <see cref="IConnectorClient"/>.</returns>
        [Obsolete("This method is deprecated. Please use CreateConnectorClientAsync(ITurnContext, string, IList<string>, bool, CancellationToken) instead.")]
        Task<IConnectorClient> CreateConnectorClientAsync(ClaimsIdentity claimsIdentity, string serviceUrl, string audience, CancellationToken cancellationToken, IList<string> scopes = null, bool useAnonymous = false);

        /// <summary>
        /// Creates an instance of a <see cref="IConnectorClient"/> for the turn.
        /// </summary>
        /// <remarks>
        /// This normally wouldn't be called directly by an Agent developer, but rather is used internally by a IChannelAdapter to create the 
        /// client needed to communicate back to the sender.  This is called at the beginning of each turn.  The Agent developer can get a turn
        /// appropriate IConnectorClient by calling <c>turnContext.Services.Get&lt;IConnectorClient&gt;()</c>.
        /// </remarks>
        /// <param name="turnContext">The context for the current turn, providing access to conversation and user information. Cannot be null.</param>
        /// <param name="audience">An optional audience identifier for which the connector client is created. If null, the default audience is
        /// used.</param>
        /// <param name="scopes">An optional list of scopes that specify the permissions granted to the connector client. If null, default
        /// scopes are applied.</param>
        /// <param name="useAnonymous">A value indicating whether to create the connector client using anonymous authentication. If <see
        /// langword="true"/>, the client is created without user credentials.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an instance of <see
        /// cref="IConnectorClient"/> for interacting with the specified audience and scopes.</returns>
        Task<IConnectorClient> CreateConnectorClientAsync(ITurnContext turnContext, string audience = null, IList<string> scopes = null, bool useAnonymous = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates the appropriate <see cref="IUserTokenClient"/> instance.
        /// </summary>
        /// <remarks>
        /// This normally wouldn't be called directly by an Agent developer, but rather is used internally by a IChannelAdapter to create the 
        /// client needed to communicate with the Azure Bot Token Service.  This is called at the beginning of each turn.  The Agent developer 
        /// can get a turn appropriate IUserTokenClient by calling <c>turnContext.Services.Get&lt;IUserTokenClient&gt;()</c>.
        /// </remarks>
        /// <param name="claimsIdentity">The inbound <see cref="Activity"/>'s <see cref="ClaimsIdentity"/>.</param>
        /// <param name="useAnonymous">Whether to use anonymous credentials.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>Asynchronous Task with <see cref="IUserTokenClient" /> instance.</returns>
        Task<IUserTokenClient> CreateUserTokenClientAsync(ClaimsIdentity claimsIdentity, bool? useAnonymous = false, CancellationToken cancellationToken = default);
    }
}
