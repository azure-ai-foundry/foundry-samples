// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    /// <summary>
    /// A <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeUserTokenRestClientRequest"/> that traces a token-exchange request,
    /// recording the connection name, user ID, and optional channel ID as span tags.
    /// </summary>
    internal class ScopeExchangeToken : ScopeUserTokenRestClientRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeExchangeToken"/> class.
        /// </summary>
        /// <param name="connectionName">The OAuth connection name used for the token exchange.</param>
        /// <param name="userId">The ID of the user performing the token exchange.</param>
        /// <param name="channelId">The channel ID, or <see langword="null"/>.</param>
        public ScopeExchangeToken(string connectionName, string userId, string? channelId = null)
            : base(Constants.ScopeExchangeToken, connectionName, userId, channelId)
        { }
    }
}
