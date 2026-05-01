using Microsoft.Agents.Core.Telemetry;
using System;

#nullable enable

namespace Microsoft.Agents.Authentication.Telemetry.Scopes
{
    /// <summary>
    /// A <see cref="Microsoft.Agents.Authentication.Telemetry.Scopes.ScopeTokenRequest"/> that traces the acquisition of an agentic instance
    /// token and records the agentic instance identifier.
    /// </summary>
    internal class ScopeGetAgenticInstanceToken : ScopeTokenRequest
    {
        private readonly string _agenticInstanceId;

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Authentication.Telemetry.Scopes.ScopeGetAgenticInstanceToken"/> class.
        /// </summary>
        /// <param name="agenticInstanceId">The identifier of the agentic application instance.</param>
        public ScopeGetAgenticInstanceToken(string agenticInstanceId)
            : base(Constants.ScopeGetAgenticInstanceToken, Constants.AuthMethodAgenticInstance)
        {
            _agenticInstanceId = agenticInstanceId;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Calls <c>base.Callback</c> to record the auth method, then tags the activity with
        /// <see cref="Microsoft.Agents.Core.Telemetry.TagNames.AgenticInstanceId"/>.
        /// </remarks>
        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception? error)
        {
            base.Callback(telemetryActivity, duration, error);
            telemetryActivity.SetTag(TagNames.AgenticInstanceId, _agenticInstanceId);
        }
    }
}