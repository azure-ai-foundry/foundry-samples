// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using System.Collections.Generic;

namespace Microsoft.Agents.Builder.Telemetry.Authorization.Scopes
{
    /// <summary>
    /// A <see cref="ScopeAuthorizationRequest"/> that traces an Azure Bot Framework
    /// OAuth sign-in operation.
    /// </summary>
    internal class ScopeAzureBotSignIn : ScopeAuthorizationRequest
    {
        private readonly ITurnContext _turnContext;
        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeAzureBotSignIn"/> class.
        /// </summary>
        /// <param name="authHandlerId">The identifier of the authentication handler processing the request.</param>
        /// <param name="exchangeConnection">The OAuth connection name, or <c>null</c>.</param>
        /// <param name="scopes">The OAuth/OIDC scopes requested, or <c>null</c>.</param>
        /// <param name="turnContext">The current bot turn context.</param>
        public ScopeAzureBotSignIn(string authHandlerId, string? exchangeConnection, IEnumerable<string>? scopes, ITurnContext turnContext) : base(Constants.ScopeAzureBotSignIn, authHandlerId, exchangeConnection, scopes)
        {
            _turnContext = turnContext;
        }

        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, System.Exception? error)
        {
            base.Callback(telemetryActivity, duration, error);
            telemetryActivity.SetTag(TagNames.ActivityChannelId, _turnContext.Activity.ChannelId?.ToString());
        }
    }
}