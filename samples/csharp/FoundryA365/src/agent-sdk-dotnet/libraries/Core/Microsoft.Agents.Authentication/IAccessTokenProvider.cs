// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Agents.Authentication
{
    public interface IAccessTokenProvider
    {
        /// <summary>
        /// Used by Agents SDK to acquire access tokens for connection to agent services or clients.
        /// </summary>
        /// <param name="forceRefresh">True to force a refresh of the token; or false to get the token only if it is necessary.</param>
        /// <param name="scopes">The scopes for which to get the token.</param>
        /// <param name="resourceUrl">The resource URL for which to get the token.</param>
        Task<string> GetAccessTokenAsync(string resourceUrl, IList<string> scopes, bool forceRefresh = false);

        /// <summary>
        /// Returns an Azure TokenCredential using this provider.
        /// </summary>
        TokenCredential GetTokenCredential();

        ImmutableConnectionSettings ConnectionSettings { get; }
    }
}
