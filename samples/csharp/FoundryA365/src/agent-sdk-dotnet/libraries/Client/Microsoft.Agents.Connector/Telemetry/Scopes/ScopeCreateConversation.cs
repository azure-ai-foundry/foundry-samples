// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    /// <summary>
    /// A <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeConnectorRequest"/> that traces a create-conversation connector request.
    /// </summary>
    internal class ScopeCreateConversation : ScopeConnectorRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeCreateConversation"/> class.
        /// </summary>
        public ScopeCreateConversation() : base(Constants.ScopeCreateConversation)
        { }
    }
}
