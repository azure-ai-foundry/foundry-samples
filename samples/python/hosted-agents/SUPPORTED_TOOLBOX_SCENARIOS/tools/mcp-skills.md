# MCP Skills

Foundry Toolbox can expose MCP-based skills that an agent loads through the Agent Skills progressive-disclosure pattern. The agent first sees skill names and descriptions, then loads the full skill and its resources only when needed.

[Back to the tool-type table](../README.md#tool-types)

## Prerequisites

1. Create a Foundry Toolbox in the same project as the agent.
2. Add and publish at least one MCP-based skill.
3. Set the agent's `TOOLBOX_NAME` to the published Toolbox name.

## Agent Framework integration

The .NET sample builds an `AgentSkillsProvider` with `AgentSkillsProviderBuilder.UseMcpSkills`. The provider authenticates to the Toolbox MCP endpoint, advertises available skills, and retrieves skill content on demand.

See the [C# Foundry Toolbox MCP Skills sample](../../../../csharp/hosted-agents/agent-framework/foundry-toolbox-mcp-skills/) for the complete implementation and local/deployed setup.

See [Agent Skills in Foundry Toolbox](https://learn.microsoft.com/azure/foundry/agents/how-to/tools/skills?pivots=dotnet) for authoring and publishing skills.