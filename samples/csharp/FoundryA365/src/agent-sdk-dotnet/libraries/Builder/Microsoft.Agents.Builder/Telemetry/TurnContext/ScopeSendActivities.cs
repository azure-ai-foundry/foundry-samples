// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;

namespace Microsoft.Agents.Builder.Telemetry.TurnContext
{
    /// <summary>
    /// A <see cref="TelemetryScope"/> that traces the sending of activities from within
    /// a turn context.
    /// </summary>
    /// <remarks>
    /// Tags the <see cref="System.Diagnostics.Activity"/> with the conversation identifier
    /// from the current turn's activity.
    /// </remarks>
    internal class ScopeSendActivities : TelemetryScope
    {
        private readonly ITurnContext _turnContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeSendActivities"/> class.
        /// </summary>
        /// <param name="turnContext">The current turn context whose activity supplies the conversation identifier.</param>
        public ScopeSendActivities(ITurnContext turnContext) : base(Constants.ScopeSendActivities)
        {
            _turnContext = turnContext;
        }

        /// <inheritdoc />
        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, System.Exception? error)
        {
            telemetryActivity.SetTag(TagNames.ConversationId, _turnContext.Activity.Conversation?.Id);
        }
    }
}