// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    /// <summary>
    /// A <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeConnectorRequest"/> that traces a get-conversations connector request.
    /// </summary>
    internal class ScopeGetConversations : ScopeConnectorRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeGetConversations"/> class.
        /// </summary>
        public ScopeGetConversations() : base(Constants.ScopeGetConversations) { }
    }
}
