// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Authentication
{
    public interface IConnectionSettings
    {
        /// <summary>
        /// Entra Authentication Endpoint to use.  If not populated, the Entra Public Cloud endpoint is assumed. 
        /// This example of Public Cloud Endpoint is https://login.microsoftonline.com
        /// </summary>
        /// <seealso cref="System.Uri" href="https://learn.microsoft.com/entra/identity-platform/authentication-national-cloud"/>
        string Authority { get; set; }
        /// <summary>
        /// Client/Application ID to use.
        /// </summary>
        string ClientId { get; set; }
        /// <summary>
        /// Default scopes list to supply.
        /// </summary>
        List<string> Scopes { get; set; }
        /// <summary>
        /// Tenant ID of endpoint to use when acquiring an access token.
        /// If not supplied, "common" is used. 
        /// </summary>
        string TenantId { get; set; }
    }
}