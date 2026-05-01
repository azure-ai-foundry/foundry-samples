// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.CopilotStudio.Client;

namespace OrchestratedClientSample
{
    /// <summary>
    /// Connection Settings extension for the sample to include appID and TenantId for creating authentication token.
    /// </summary>
    internal class SampleConnectionSettings : ConnectionSettings
    {
        /// <summary>
        /// Tenant ID for creating the authentication for the connection.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Application ID for creating the authentication for the connection.
        /// </summary>
        public string? AppClientId { get; set; }

        /// <summary>
        /// Create ConnectionSettings from a configuration section.
        /// </summary>
        public SampleConnectionSettings(IConfigurationSection config) : base(config)
        {
            AppClientId = config[nameof(AppClientId)] ?? throw new ArgumentException($"{nameof(AppClientId)} not found in config");
            TenantId = config[nameof(TenantId)] ?? throw new ArgumentException($"{nameof(TenantId)} not found in config");
        }
    }
}
