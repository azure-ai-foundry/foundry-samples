// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    /// <summary>
    /// A <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeUserTokenRestClientRequest"/> that traces a get-sign-in-resource request,
    /// recording the connection name, user ID, and optional channel ID as span tags.
    /// </summary>
    internal class ScopeGetSignInResource : ScopeUserTokenRestClientRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeGetSignInResource"/> class.
        /// </summary>
        /// <param name="connectionName">The OAuth connection name.</param>
        /// <param name="userId">The ID of the user requesting the sign-in resource.</param>
        /// <param name="channelId">The channel ID, or <see langword="null"/>.</param>
        public ScopeGetSignInResource(string connectionName, string userId, string? channelId = null)
            : base(Constants.ScopeGetSignInResource, connectionName, userId, channelId)
        { }
    }
}
