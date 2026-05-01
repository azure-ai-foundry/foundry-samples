// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Builder.Telemetry.Authorization.Scopes
{
    /// <summary>
    /// A <see cref="ScopeAuthorizationRequest"/> that traces the acquisition of an
    /// Azure Bot Framework user token.
    /// </summary>
    internal class ScopeAzureBotToken : ScopeAuthorizationRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeAzureBotToken"/> class.
        /// </summary>
        /// <param name="authHandlerId">The identifier of the authentication handler processing the request.</param>
        /// <param name="exchangeConnection">The OAuth connection name, or <c>null</c>.</param>
        /// <param name="scopes">The OAuth/OIDC scopes requested, or <c>null</c>.</param>
        public ScopeAzureBotToken(string authHandlerId, string? exchangeConnection, IEnumerable<string>? scopes) : base(Constants.ScopeAzureBotToken, authHandlerId, exchangeConnection, scopes)
        {
        }
    }
}