// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.A2A.Protocol;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.A2A;

/// <summary>
/// Implement to handle setting agent specific <see cref="Microsoft.Agents.Hosting.A2A.Protocol.AgentCard"/> properties.
/// </summary>
public interface IAgentCardHandler
{
    /// <summary>
    /// Called when the A2A client needs the <see cref="Microsoft.Agents.Hosting.A2A.Protocol.AgentCard"/>.
    /// </summary>
    /// <param name="hostAgentCard">The A2A Host will create an AgentCard with proper values for most properties.<br/>
    /// AgentApplications will likely want to set: <c>hostAgentCard.Name</c>, <c>hostAgentCard.Description</c>, 
    /// <c>hostAgentCard.Version</c>, and <c>hostAgentCard.Skills</c>.
    /// <para>Changing other properties, specifically around auth and capabilities, should be avoided without guidance
    /// on supported values.</para>
    /// </param>
    Task<AgentCard> GetAgentCard(AgentCard hostAgentCard);
}
