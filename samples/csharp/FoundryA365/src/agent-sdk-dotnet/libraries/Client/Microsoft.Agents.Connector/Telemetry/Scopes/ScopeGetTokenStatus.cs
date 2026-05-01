// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    /// <summary>
    /// A <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeUserTokenRestClientRequest"/> that traces a get-token-status request,
    /// recording the user ID and optional channel ID as span tags.
    /// </summary>
    internal class ScopeGetTokenStatus : ScopeUserTokenRestClientRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeGetTokenStatus"/> class.
        /// </summary>
        /// <param name="userId">The ID of the user whose token status is being checked.</param>
        /// <param name="channelId">The channel ID, or <see langword="null"/>.</param>
        public ScopeGetTokenStatus(string userId, string? channelId = null)
            : base(Constants.ScopeGetTokenStatus, null, userId, channelId)
        { }
    }
}
