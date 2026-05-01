// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Authentication.Msal
{
    internal class MsalTokenCredential(MsalAuth connection) : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
#pragma warning disable CA2012
            return GetTokenAsync(requestContext, cancellationToken).Result;
#pragma warning restore CA2012
        }

        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            var result = await connection.InternalGetAccessTokenAsync(requestContext.Scopes[0], requestContext.Scopes);
            return new AccessToken(result.AccessToken, result.ExpiresOn);
        }
    }
}
