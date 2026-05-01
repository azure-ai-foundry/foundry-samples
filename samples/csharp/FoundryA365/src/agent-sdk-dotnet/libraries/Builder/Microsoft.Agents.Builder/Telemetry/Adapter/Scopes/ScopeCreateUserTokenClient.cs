// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using System;

namespace Microsoft.Agents.Builder.Telemetry.Adapter.Scopes
{
    /// <summary>
    /// A <see cref="TelemetryScope"/> that traces the creation of a user-token client
    /// used to manage OAuth user tokens.
    /// </summary>
    /// <remarks>
    /// Records the token service endpoint as a span tag.
    /// </remarks>
    internal class ScopeCreateUserTokenClient : TelemetryScope
    {
        private readonly string _tokenServiceEndpoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeCreateUserTokenClient"/> class.
        /// </summary>
        /// <param name="tokenServiceEndpoint">The endpoint of the token service.</param>
        public ScopeCreateUserTokenClient(string tokenServiceEndpoint) : base(Constants.ScopeCreateUserTokenClient)
        {
            _tokenServiceEndpoint = tokenServiceEndpoint;
        }

        /// <inheritdoc />
        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception? error)
        {
            telemetryActivity.SetTag(TagNames.TokenServiceEndpoint, _tokenServiceEndpoint);
        }
    }
}
