using Microsoft.Agents.Core.Telemetry;
using System;
using System.Diagnostics;

#nullable enable

namespace Microsoft.Agents.Authentication.Telemetry.Scopes
{
    /// <summary>
    /// A <see cref="Microsoft.Agents.Core.Telemetry.TelemetryScope"/> that traces a token-request operation and tags
    /// the <see cref="System.Diagnostics.Activity"/> with the authentication method used.
    /// </summary>
    /// <remarks>
    /// Derived classes can override <see cref="Microsoft.Agents.Core.Telemetry.TelemetryScope.Callback"/> to add further
    /// tags (e.g., scopes or instance identifiers) after calling <c>base.Callback</c>.
    /// </remarks>
    internal class ScopeTokenRequest : TelemetryScope
    {
        private readonly string _authMethod;

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Authentication.Telemetry.Scopes.ScopeTokenRequest"/> class.
        /// </summary>
        /// <param name="scopeName">The name for the underlying <see cref="System.Diagnostics.Activity"/>.</param>
        /// <param name="authMethod">
        /// The authentication method label to record (e.g., <see cref="Microsoft.Agents.Authentication.Telemetry.Constants.AuthMethodOBO"/>).
        /// </param>
        public ScopeTokenRequest(string scopeName, string authMethod) : base(scopeName)
        {
            _authMethod = authMethod;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Tags the activity with <see cref="Microsoft.Agents.Core.Telemetry.TagNames.AuthMethod"/>.
        /// </remarks>
        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception? exception)
        {
            telemetryActivity.AddTag(TagNames.AuthMethod, _authMethod);

            TagList metricTags = new();
            metricTags.Add(TagNames.AuthMethod, _authMethod);

            Metrics.TokenRequestDuration.Record(duration, metricTags);
            Metrics.TokenRequestCount.Add(1, metricTags);
        }
    }
}