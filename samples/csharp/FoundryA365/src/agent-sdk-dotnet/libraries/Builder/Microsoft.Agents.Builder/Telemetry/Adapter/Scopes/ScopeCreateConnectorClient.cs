// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Builder.Telemetry.Adapter.Scopes
{
    /// <summary>
    /// A <see cref="TelemetryScope"/> that traces the creation of a connector client
    /// used to communicate with the channel service.
    /// </summary>
    /// <remarks>
    /// Records the service URL, requested authentication scopes, and whether the
    /// request is agentic as span tags.
    /// </remarks>
    internal class ScopeCreateConnectorClient : TelemetryScope
    {
        private readonly string _serviceUrl;
        private readonly IEnumerable<string>? _scopes;
        private readonly bool _isAgenticRequest;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeCreateConnectorClient"/> class.
        /// </summary>
        /// <param name="serviceUrl">The channel service URL the client will connect to.</param>
        /// <param name="scopes">The auth scopes requested for the client, or <c>null</c>.</param>
        /// <param name="isAgenticRequest">Whether the request originates from an agentic scenario.</param>
        public ScopeCreateConnectorClient(string serviceUrl, IEnumerable<string>? scopes, bool isAgenticRequest) : base(Constants.ScopeCreateConnectorClient)
        {
            _serviceUrl = serviceUrl;
            _scopes = scopes;
            _isAgenticRequest = isAgenticRequest;
        }

        /// <inheritdoc />
        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception? error)
        {
            telemetryActivity.SetTag(TagNames.ServiceUrl, _serviceUrl);
            telemetryActivity.SetTag(TagNames.AuthScopes, TelemetryUtils.FormatScopes(_scopes));
            telemetryActivity.SetTag(TagNames.IsAgentic, _isAgenticRequest);
        }
    }
}
