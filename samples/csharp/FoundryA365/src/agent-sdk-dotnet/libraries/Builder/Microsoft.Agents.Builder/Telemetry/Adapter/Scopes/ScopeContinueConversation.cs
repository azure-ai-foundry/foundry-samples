// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Telemetry;
using System;

namespace Microsoft.Agents.Builder.Telemetry.Adapter.Scopes
{
    /// <summary>
    /// A <see cref="TelemetryScope"/> that traces a proactive "continue conversation"
    /// operation through the channel adapter.
    /// </summary>
    /// <remarks>
    /// Records the agent application identifier, conversation identifier, and whether
    /// the request is agentic as span tags.
    /// </remarks>
    internal class ScopeContinueConversation : TelemetryScope
    {
        private readonly IActivity _activity;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeContinueConversation"/> class.
        /// </summary>
        /// <param name="activity">The activity representing the conversation reference to continue.</param>
        public ScopeContinueConversation(IActivity activity) : base(Constants.ScopeContinueConversation)
        {
            _activity = activity;
        }

        /// <inheritdoc />
        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception? error)
        {
            telemetryActivity.SetTag(TagNames.AppId, _activity.Recipient?.Id);
            telemetryActivity.SetTag(TagNames.ConversationId, _activity.Conversation?.Id);
            telemetryActivity.SetTag(TagNames.IsAgentic, _activity.IsAgenticRequest());
        }
    }
}
