// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.CopilotStudio.Client.Discovery;

namespace Microsoft.Agents.CopilotStudio.Client.Interfaces
{

    public interface ICopilotStudioClientConnectionSettings
    {
        /// <summary>
        /// Schema name for the Copilot Studio Hosted Copilot. 
        /// </summary>
        string? SchemaName { get; set; }
        
        /// <summary>
        /// if PowerPlatformCloud is set to Other, this is the url for the power platform API endpoint.
        /// </summary>
        string? CustomPowerPlatformCloud { get; set; }
        
        /// <summary>
        /// Environment ID for the environment that hosts the Agent
        /// </summary>
        string? EnvironmentId { get; set; }
        
        /// <summary>
        /// Power Platform Cloud where the environment is hosted
        /// </summary>
        PowerPlatformCloud? Cloud { get; set; }

        /// <summary>
        /// Type of Agent hosted in Copilot Studio
        /// </summary>
        AgentType? CopilotAgentType { get; set; }

        /// <summary>
        /// URL provided to connect directly to Copilot Studio endpoint,  When provided all other settings are ignored.
        /// </summary>
        string? DirectConnectUrl { get; set; }
        
        /// <summary>
        /// Directs Copilot Studio Client to use the experimental endpoint if available.
        /// </summary>
        bool UseExperimentalEndpoint { get; set; }

        /// <summary>
        /// When enabled, writes out diagnostic information to the logsink.
        /// </summary>
        bool EnableDiagnostics { get; set; }
    }
}