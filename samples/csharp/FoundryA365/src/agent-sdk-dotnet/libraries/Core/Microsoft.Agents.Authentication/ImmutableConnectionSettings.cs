// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Authentication
{
    /// <summary>
    /// Describes the connection settings for a given connection.
    /// </summary>
    public class ImmutableConnectionSettings(ConnectionSettingsBase settings)
    {

        /// <summary>
        /// Client ID to use for the connection. 
        /// </summary>
        public string ClientId => settings.ClientId;

        /// <summary>
        /// Login authority to use for the connection
        /// </summary>
        public string Authority => settings.Authority;

        /// <summary>
        /// Tenant Id for creating the authentication for the connection 
        /// </summary>
        public string TenantId => settings.TenantId;

        /// <summary>
        /// List of scopes to use for the connection.
        /// </summary>
        public IList<string> Scopes => settings.Scopes;

        /// <summary>
        /// Alternate Agentic Blueprint connection
        /// </summary>
        public string AlternateBlueprintConnectionName => settings.AlternateBlueprintConnectionName;
    }
}
