// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    /// <summary>
    /// A <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeConnectorRequest"/> that traces an update-activity connector request,
    /// recording the conversation ID and activity ID as span tags.
    /// </summary>
    internal class ScopeUpdateActivity : ScopeConnectorRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeUpdateActivity"/> class.
        /// </summary>
        /// <param name="conversationId">The ID of the conversation containing the activity.</param>
        /// <param name="activityId">The ID of the activity to update.</param>
        public ScopeUpdateActivity(string conversationId, string activityId)
            : base(Constants.ScopeUpdateActivity, conversationId, activityId)
        { }
    }
}
