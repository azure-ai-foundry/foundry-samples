// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    /// <summary>
    /// A <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeConnectorRequest"/> that traces a reply-to-activity connector request,
    /// recording the conversation ID and activity ID as span tags.
    /// </summary>
    internal class ScopeReplyToActivity : ScopeConnectorRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeReplyToActivity"/> class.
        /// </summary>
        /// <param name="conversationId">The conversation ID associated with the activity.</param>
        /// <param name="activityId">The ID of the activity being replied to.</param>
        public ScopeReplyToActivity(string conversationId, string activityId)
            : base(Constants.ScopeReplyToActivity, conversationId, activityId)
        {}
    }
}
