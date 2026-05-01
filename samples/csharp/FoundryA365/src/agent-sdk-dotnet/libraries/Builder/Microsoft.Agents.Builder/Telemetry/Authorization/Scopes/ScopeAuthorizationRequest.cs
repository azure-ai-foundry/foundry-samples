// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;

#nullable enable

namespace Microsoft.Agents.Builder.Telemetry.Authorization.Scopes
{
    /// <summary>
    /// A <see cref="TelemetryScope"/> that traces an authorization-token request and records
    /// the auth handler identifier, optional OAuth connection name, and optional scopes on the
    /// <see cref="System.Diagnostics.Activity"/>.
    /// </summary>
    /// <remarks>
    /// Derived classes pass a specific activity name and may add further tags by overriding
    /// <see cref="TelemetryScope.Callback"/> and calling <c>base.Callback</c>.
    /// </remarks>
    internal class ScopeAuthorizationRequest : TelemetryScope
    {
        private readonly string _authHandlerId;
        private readonly string? _exchangeConnection;
        private readonly IEnumerable<string>? _scopes;

        public ScopeAuthorizationRequest(string scopeName, string authHandlerId, string? exchangeConnection = null, IEnumerable<string>? scopes = null) : base(scopeName)
        {
            _authHandlerId = authHandlerId;
            _exchangeConnection = exchangeConnection;
            _scopes = scopes;
        }

        protected override void Callback(Activity telemetryActivity, double duration, Exception? exception)
        {
            telemetryActivity.SetTag(TagNames.AuthHandlerId, _authHandlerId);
            if (_exchangeConnection != null)
            {
                telemetryActivity.SetTag(TagNames.ExchangeConnection, _exchangeConnection);
            }
            if (_scopes != null)
            {
                telemetryActivity.SetTag(TagNames.AuthScopes, TelemetryUtils.FormatScopes(_scopes));
            }
        }
    }
}