// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.Serialization;

namespace Microsoft.Agents.CopilotStudio.Client.Discovery
{
    /// <summary>
    /// Agent types that can be connected to by the Copilot Studio Client
    /// </summary>
    public enum AgentType
    {
        /// <summary>
        /// Copilot Studio Published Copilot
        /// </summary>
        [EnumMember(Value = "Published")]
        Published = 0,
        /// <summary>
        /// System PreBuilt Copilot
        /// </summary>
        [EnumMember(Value = "Prebuilt")]
        Prebuilt = 1
    }
}
