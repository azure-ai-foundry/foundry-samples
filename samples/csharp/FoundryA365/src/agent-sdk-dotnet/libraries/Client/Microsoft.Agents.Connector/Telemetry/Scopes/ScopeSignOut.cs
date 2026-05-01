// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    /// <summary>
    /// A <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeUserTokenRestClientRequest"/> that traces a sign-out request,
    /// recording the connection name, user ID, and optional channel ID as span tags.
    /// </summary>
    internal class ScopeSignOut : ScopeUserTokenRestClientRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeSignOut"/> class.
        /// </summary>
        /// <param name="connectionName">The OAuth connection name used for the sign-out.</param>
        /// <param name="userId">The ID of the user signing out.</param>
        /// <param name="channelId">The channel ID, or <see langword="null"/>.</param>
        public ScopeSignOut(string connectionName, string userId, string? channelId = null)
            : base(Constants.ScopeSignOut, connectionName, userId, channelId)
        { }
    }
}
