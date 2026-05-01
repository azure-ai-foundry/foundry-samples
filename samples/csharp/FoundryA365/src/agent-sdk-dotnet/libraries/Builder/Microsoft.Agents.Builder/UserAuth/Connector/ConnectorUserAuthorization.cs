// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.UserAuth.Connector
{
    /// <summary>
    /// User Authorization handling for Copilot Studio Connector requests.
    /// </summary>
    public class ConnectorUserAuthorization : OBOExchange, IUserAuthorization
    {
        private readonly OBOSettings _settings;
        private readonly ILogger _logger;

        /// <summary>
        /// Required constructor for the UserAuthorizationModuleLoader (when using IConfiguration)
        /// </summary>
        /// <param name="name"></param>
        /// <param name="storage">The storage provider used for user authorization data.</param>
        /// <param name="connections"></param>
        /// <param name="configurationSection"></param>
        /// <param name="logger"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public ConnectorUserAuthorization(string name, IStorage storage, IConnections connections, IConfigurationSection configurationSection, ILogger logger = null)
            : this(name, connections, GetOBOSettings(configurationSection), logger)
        {
        }

        /// <summary>
        /// Code-first constructor.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="connections"></param>
        /// <param name="settings"></param>
        /// <param name="logger"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public ConnectorUserAuthorization(string name, IConnections connections, OBOSettings settings, ILogger logger = null) : base(connections)
        {
            _settings = settings;
            _logger = logger ?? NullLogger<ILogger>.Instance;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        protected override OBOSettings GetOBOSettings()
        {
            return _settings;
        }

        /// <inheritdoc/>
        public string Name { get; private set; }

        /// <inheritdoc/>
        public async Task<TokenResponse> GetRefreshedUserTokenAsync(ITurnContext turnContext, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            var tokenResponse = CreateTokenResponse(turnContext);

            if (tokenResponse.Expiration != null)
            {
                var diff = tokenResponse.Expiration - DateTimeOffset.UtcNow;
                if (diff.HasValue && diff?.TotalMinutes <= 0)
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.UnexpectedConnectorTokenExpiration, null, [Name]);
                }
            }

            try
            {
                return await HandleOBO(turnContext, tokenResponse, exchangeConnection, exchangeScopes, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                await SignOutUserAsync(turnContext, cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<TokenResponse> SignInUserAsync(ITurnContext turnContext, bool forceSignIn = false, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            // There is no "sign in" or external token retrieval in this handler.  A single impl is sufficient.
            return await GetRefreshedUserTokenAsync(turnContext, exchangeConnection, exchangeScopes, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// This is a no-op for this handler.
        /// </summary>
        /// <param name="turnContext"></param>
        /// <param name="cancellationToken"></param>
        public Task ResetStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            // No concept of reset with ConnectorAuth
            return Task.CompletedTask;
        }

        /// <summary>
        /// This is a no-op for this handler.
        /// </summary>
        /// <param name="turnContext"></param>
        /// <param name="cancellationToken"></param>
        public Task SignOutUserAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            // No concept of sign-out with ConnectorAuth
            return Task.CompletedTask;
        }

        private TokenResponse CreateTokenResponse(ITurnContext turnContext)
        {
            if (turnContext.Identity is CaseSensitiveClaimsIdentity identity)
            {
                var tokenResponse = new TokenResponse()
                {
                    Token = identity.SecurityToken.UnsafeToString(),
                };

                try
                {
                    var jwtToken = new JwtSecurityToken(tokenResponse.Token);
                    tokenResponse.Expiration = jwtToken.ValidTo;
                    tokenResponse.IsExchangeable = AgentClaims.IsExchangeableToken(jwtToken);
                }
                catch (Exception)
                {
                    tokenResponse.IsExchangeable = false;
                }

                return tokenResponse;
            }

            throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.UnexpectedConnectorRequestToken, null, [Name]);
        }

        // Get the ConnectorUserAuthorization settings
        private static OBOSettings GetOBOSettings(IConfigurationSection config)
        {
            var settings = config.Get<OBOSettings>();

            if (settings.OBOScopes == null)
            {
                // try reading as a string to compensate for users just setting a non-array string
                var configScope = config.GetSection(nameof(OBOSettings.OBOScopes)).Get<string>();
                if (!string.IsNullOrEmpty(configScope))
                {
                    settings.OBOScopes = [configScope];
                }
            }

            return settings;
        }
    }
}
