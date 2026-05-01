// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Errors;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Authentication
{
    /// <summary>
    /// Describes the connection settings for a given connection.
    /// </summary>
    public abstract class ConnectionSettingsBase : IConnectionSettings
    {
        protected ConnectionSettingsBase() { }

        protected ConnectionSettingsBase(IConfigurationSection msalConfigurationSection)
        {
            if (msalConfigurationSection != null && msalConfigurationSection.Exists())
            {
                ClientId = msalConfigurationSection.GetValue<string>("ClientId", null);
                Authority = msalConfigurationSection.GetValue<string>("AuthorityEndpoint", null);
                TenantId = msalConfigurationSection.GetValue<string>("TenantId", null);
                Scopes = msalConfigurationSection.GetSection("Scopes")?.Get<List<string>>();
                AlternateBlueprintConnectionName = msalConfigurationSection.GetValue<string>("AlternateBlueprintConnectionName", null);
            }
            else
            {
                if (msalConfigurationSection != null)
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ConfigurationSectionNotFound, null, msalConfigurationSection.Key);
                }
                else
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<ArgumentNullException>(ErrorHelper.ConfigurationSectionNotProvided, null);
                }
            }
        }

        /// <summary>
        /// Client ID to use for the connection. 
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Login authority to use for the connection
        /// </summary>
        public string Authority { get; set; } = string.Empty;

        /// <summary>
        /// Tenant Id for creating the authentication for the connection 
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// List of scopes to use for the connection.
        /// </summary>
        public List<string> Scopes { get; set; } = [];

        /// <summary>
        /// Alternate Agentic Blueprint connection
        /// </summary>
        public string AlternateBlueprintConnectionName { get; set; }
    }
}
