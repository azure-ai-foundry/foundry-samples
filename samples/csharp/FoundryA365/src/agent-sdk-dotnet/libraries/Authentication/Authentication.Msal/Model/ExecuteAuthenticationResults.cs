// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Identity.Client;
using System;

namespace Microsoft.Agents.Authentication.Msal.Model
{
    /// <summary>
    /// Class used to describe the outcome of the execute authentication process.
    /// </summary>
    internal class ExecuteAuthenticationResults
    {
        public AuthenticationResult MsalAuthResult { get; set; }
        public Uri TargetServiceUrl { get; set; }
        public object MsalAuthClient { get; set; }
        public string Authority { get; set; }
        public string Resource { get; set; }
        public IAccount UserIdent { get; set; }
        //public MemoryBackedTokenCache MemTokenCache { get; set; }


        internal string GetAuthTokenAndProperties(out AuthenticationResult msalAuthResult, out Uri targetServiceUrl, out object msalAuthClient, out string authority, out string resource, out IAccount userIdent) //, out MemoryBackedTokenCache memoryBackedTokenCache)
        {
            msalAuthResult = MsalAuthResult;
            targetServiceUrl = TargetServiceUrl;
            msalAuthClient = MsalAuthClient;
            authority = Authority;
            resource = Resource;
            userIdent = UserIdent;
            //memoryBackedTokenCache = MemTokenCache;

            return MsalAuthResult.AccessToken;
        }
    }
}
