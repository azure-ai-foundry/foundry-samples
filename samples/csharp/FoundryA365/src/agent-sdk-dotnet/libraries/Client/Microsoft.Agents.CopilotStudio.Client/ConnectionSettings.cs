// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Agents.CopilotStudio.Client.Discovery;
using Microsoft.Agents.CopilotStudio.Client.Interfaces;

namespace Microsoft.Agents.CopilotStudio.Client
{
    /// <summary>
    /// Configuration object for the DirectToEngine client.
    /// </summary>
    public class ConnectionSettings : ICopilotStudioClientConnectionSettings
    {
        //<inheritdoc/>
        public string? EnvironmentId { get; set; }
        //<inheritdoc/>
        public PowerPlatformCloud? Cloud { get; set; }
        //<inheritdoc/>
        public string? CustomPowerPlatformCloud { get; set; }
        //<inheritdoc/>
        public string? SchemaName { get; set; }
        //<inheritdoc/>
        public AgentType? CopilotAgentType { get; set; }
        //<inheritdoc/>
        public string? DirectConnectUrl { get; set; } = null;
        //<inheritdoc/>
        public bool UseExperimentalEndpoint { get; set; } = false;
        //<inheritdoc/>
        public bool EnableDiagnostics { get; set; } = false;
        //<inheritdoc/>
        public string? CdsBotId { get; set; }


        /// <summary>
        /// Default constructor for the ConnectionSettings class.
        /// </summary>
        public ConnectionSettings()
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.CopilotStudio.Client.ConnectionSettings"/> class.
        /// </summary>
        /// <param name="config">Configuration Section containing DirectToEngine Connection settings</param>
        /// <exception cref="System.ArgumentException">Thrown when required configuration values are missing</exception>
        public ConnectionSettings(IConfigurationSection config)
        {
            if (config != null && config.Exists())
            {
                DirectConnectUrl = config[nameof(DirectConnectUrl)];
                Cloud = config.GetValue(nameof(Cloud), PowerPlatformCloud.Prod);
                CopilotAgentType = config.GetValue(nameof(CopilotAgentType), AgentType.Published);
                CustomPowerPlatformCloud = config[nameof(CustomPowerPlatformCloud)];
                UseExperimentalEndpoint = config.GetValue<bool>(nameof(UseExperimentalEndpoint), false);
                EnableDiagnostics = config.GetValue<bool>(nameof(EnableDiagnostics), false);
                CdsBotId = config[nameof(CdsBotId)];
                if (string.IsNullOrEmpty(DirectConnectUrl))
                {
                    EnvironmentId = config[nameof(EnvironmentId)] ?? throw new ArgumentException($"{nameof(EnvironmentId)} not found in config");
                    SchemaName = config[nameof(SchemaName)] ?? throw new ArgumentException($"{nameof(SchemaName)} not found in config");
                }
            }
        }
    }
}
