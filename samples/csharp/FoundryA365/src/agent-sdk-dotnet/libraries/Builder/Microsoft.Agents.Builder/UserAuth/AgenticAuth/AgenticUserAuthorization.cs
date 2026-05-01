// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Builder.Telemetry.Authorization.Scopes;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.UserAuth.AgenticAuth
{
    /// <summary>
    /// Handles OAuth using the Azure Bot Token Service.
    /// </summary>
    public class AgenticUserAuthorization : IUserAuthorization
    {
        private readonly IConnections _connections;
        private readonly AgenticAuthSettings _a365AuthSettings;
        private readonly ILogger _logger;

        /// <summary>
        /// Required constructor for type loader construction.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="storage"></param>
        /// <param name="connections"></param>
        /// <param name="configurationSection"></param>
        /// <param name="logger"></param>
        public AgenticUserAuthorization(string name, IStorage storage, IConnections connections, IConfigurationSection configurationSection, ILogger logger = null)
            : this(name, storage, connections, configurationSection.Get<AgenticAuthSettings>(), logger)
        {
        }

        /// <summary>
        /// Initializes the class
        /// </summary>
        /// <param name="name">The authentication name.</param>
        /// <param name="settings">The settings to initialize the class</param>
        /// <param name="logger"></param>
        /// <param name="storage">The storage to use.</param>
        /// <param name="connections"></param>
        public AgenticUserAuthorization(string name, IStorage storage, IConnections connections, AgenticAuthSettings settings, ILogger logger = null) 
        {
            AssertionHelpers.ThrowIfNull(connections, nameof(connections));

            _connections = connections;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _a365AuthSettings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? NullLogger<ILogger>.Instance;
        }

        public string Name { get; private set; }

        /// <inheritdoc/>
        public Task<TokenResponse> SignInUserAsync(ITurnContext turnContext, bool forceSignIn = false, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            return GetRefreshedUserTokenAsync(turnContext, exchangeConnection, exchangeScopes, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<TokenResponse> GetRefreshedUserTokenAsync(ITurnContext turnContext, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            if (!turnContext.Activity.IsAgenticRequest())
            {
                throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.NotAnAgenticRequest, null, "GetAgenticUserToken");
            }

            using var telemetryScope = new ScopeAgenticToken(Name, exchangeConnection, exchangeScopes);

            IAccessTokenProvider connection;
            if (!string.IsNullOrEmpty(_a365AuthSettings.AlternateBlueprintConnectionName))
            {
                connection = _connections.GetConnection(_a365AuthSettings.AlternateBlueprintConnectionName);
            }
            else
            {
                connection = _connections.GetTokenProvider(turnContext.Identity, turnContext.Activity);
            }

            if (connection is not IAgenticTokenProvider agenticTokenProvider)
            {
                throw ExceptionHelper.GenerateException<InvalidOperationException>(
                    ErrorHelper.AgenticTokenProviderNotFound, null, $"{turnContext.Identity.GetIncomingAudience()}:{turnContext.Activity.ServiceUrl}");
            }

            string token;
            if (string.Equals(turnContext.Activity?.Recipient?.Role, RoleTypes.AgenticIdentity, StringComparison.OrdinalIgnoreCase))
            {
                token = await agenticTokenProvider.GetAgenticInstanceTokenAsync(
                    turnContext.Activity.GetAgenticTenantId(),
                    turnContext.Activity.GetAgenticInstanceId(),
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                token = await agenticTokenProvider.GetAgenticUserTokenAsync(
                    turnContext.Activity.GetAgenticTenantId(),
                    turnContext.Activity.GetAgenticInstanceId(),
                    turnContext.Activity.GetAgenticUser(),
                    exchangeScopes ?? _a365AuthSettings.Scopes,
                    cancellationToken).ConfigureAwait(false);
            }

            return new TokenResponse(token: token);
        }

        /// <inheritdoc/>
        public Task ResetStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task SignOutUserAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
