// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.UserAuth
{
    public abstract class OBOExchange
    {
        public OBOExchange(IConnections connections)
        {
            Connections = connections ?? throw new ArgumentNullException(nameof(connections));
        }

        protected IConnections Connections { get; private set; }

        protected abstract OBOSettings GetOBOSettings();

        protected async Task<TokenResponse> HandleOBO(ITurnContext turnContext, TokenResponse token, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(token?.Token))
            {
                return null;
            }

            var oboSettings = GetOBOSettings();
            var connectionName = exchangeConnection ?? oboSettings.OBOConnectionName;
            IList<string> scopes = exchangeScopes ?? oboSettings.OBOScopes;

            // If OBO is not supplied (by config or passed) return token as-is.
            if (scopes == null || !scopes.Any())
            {
                return token;
            }

            // Can we even exchange this?
            if (!token.IsExchangeable)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.OBONotExchangeableToken, null, [token.ConnectionName]);
            }

            // Can the named Connection even do this?
            if (!TryGetOBOProvider(turnContext, connectionName, out var oboExchangeProvider))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.OBONotSupported, null, [connectionName]);
            }

            // Do exchange.
            try
            {
                token = await oboExchangeProvider.AcquireTokenOnBehalfOf(scopes, token.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.OBOExchangeFailed, ex, [connectionName, string.Join(",", scopes)]);
            }

            if (token == null)
            {
                // AcquireTokenOnBehalfOf returned null
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.OBOExchangeFailed, null, [connectionName, string.Join(",", scopes)]);
            }

            return token ?? null;
        }

        protected bool TryGetOBOProvider(ITurnContext turnContext, string connectionName, out IOBOExchange oboExchangeProvider)
        {
            IAccessTokenProvider tokenProvider = null;

            if (string.IsNullOrEmpty(connectionName))
            {
                // Use default connection from turn context
                tokenProvider = Connections.GetTokenProvider(turnContext.Identity, turnContext.Activity);
            }
            else
            {
                Connections.TryGetConnection(connectionName, out tokenProvider);
            }

            if (tokenProvider is not IOBOExchange)
            {
                oboExchangeProvider = null;
                return false;
            }

            oboExchangeProvider = (IOBOExchange)tokenProvider;
            return true;
        }
    }
}