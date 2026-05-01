using Microsoft.Agents.Core.Telemetry;
using System;
using System.Collections.Generic;

#nullable enable

namespace Microsoft.Agents.Authentication.Telemetry.Scopes
{
    /// <summary>
    /// A <see cref="Microsoft.Agents.Authentication.Telemetry.Scopes.ScopeTokenRequest"/> that traces the acquisition of an agentic user
    /// token and records the agentic instance identifier, user identifier, and requested scopes.
    /// </summary>
    internal class ScopeGetAgenticUserToken : ScopeTokenRequest
    {
        private readonly string _agenticInstanceId;
        private readonly string _agenticUserId;
        private readonly IEnumerable<string>? _scopes;

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Authentication.Telemetry.Scopes.ScopeGetAgenticUserToken"/> class.
        /// </summary>
        /// <param name="agenticInstanceId">The identifier of the agentic application instance.</param>
        /// <param name="agenticUserId">The identifier of the agentic user.</param>
        /// <param name="scopes">The OAuth/OIDC scopes requested for the token, or <c>null</c>.</param>
        public ScopeGetAgenticUserToken(string agenticInstanceId, string agenticUserId, IEnumerable<string>? scopes)
            : base(Constants.ScopeGetAgenticUserToken, Constants.AuthMethodAgenticUser)
        {
            _agenticInstanceId = agenticInstanceId;
            _agenticUserId = agenticUserId;
            _scopes = scopes;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Calls <c>base.Callback</c> to record the auth method, then tags the activity with
        /// <see cref="Microsoft.Agents.Core.Telemetry.TagNames.AgenticInstanceId"/>, <see cref="Microsoft.Agents.Core.Telemetry.TagNames.AgenticUserId"/>,
        /// and <see cref="Microsoft.Agents.Core.Telemetry.TagNames.AuthScopes"/>.
        /// </remarks>
        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception? error)
        {
            base.Callback(telemetryActivity, duration, error);
            telemetryActivity.SetTag(TagNames.AgenticInstanceId, _agenticInstanceId);
            telemetryActivity.SetTag(TagNames.AgenticUserId, _agenticUserId);
            telemetryActivity.SetTag(TagNames.AuthScopes, TelemetryUtils.FormatScopes(_scopes));
        }
    }
}