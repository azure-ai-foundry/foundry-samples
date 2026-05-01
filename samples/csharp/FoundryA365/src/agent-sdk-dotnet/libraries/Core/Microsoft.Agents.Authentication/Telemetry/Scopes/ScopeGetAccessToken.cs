using Microsoft.Agents.Core.Telemetry;
using System;
using System.Collections.Generic;

#nullable enable

namespace Microsoft.Agents.Authentication.Telemetry.Scopes
{
    /// <summary>
    /// A <see cref="Microsoft.Agents.Authentication.Telemetry.Scopes.ScopeTokenRequest"/> that traces a generic access-token acquisition
    /// and records the requested scopes.
    /// </summary>
    internal class ScopeGetAccessToken : ScopeTokenRequest
    {
        private readonly IEnumerable<string> _scopes;

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Authentication.Telemetry.Scopes.ScopeGetAccessToken"/> class.
        /// </summary>
        /// <param name="scopes">The OAuth/OIDC scopes requested for the token.</param>
        /// <param name="authMethod">The authentication method label to record.</param>
        public ScopeGetAccessToken(IEnumerable<string> scopes, string authMethod) : base(Constants.ScopeGetAccessToken, authMethod)
        {
            _scopes = scopes;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Calls <c>base.Callback</c> to record the auth method, then tags the activity with
        /// <see cref="Microsoft.Agents.Core.Telemetry.TagNames.AuthScopes"/>.
        /// </remarks>
        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception? exception)
        {
            base.Callback(telemetryActivity, duration, exception);
            telemetryActivity.SetTag(TagNames.AuthScopes, TelemetryUtils.FormatScopes(_scopes));
        }
    }
}