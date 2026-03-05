using Azure.Identity;
using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;

// Format: "https://resource_name.ai.azure.com/api/projects/project_name"
var projectEndpoint = "your_project_endpoint";
var agentName = "your_agent_name";

// Create project client to call Foundry API
AIProjectClient projectClient = new(
    endpoint: new Uri(projectEndpoint),
    tokenProvider: new DefaultAzureCredential());

// Create an agent with a model and instructions
AgentDefinition agentDefinition = new PromptAgentDefinition("gpt-5-mini")
{
    Instructions = "You are a helpful assistant that answers general questions",
};

AgentVersion agent = projectClient.Agents.CreateAgentVersion(
    agentName,
    options: new(agentDefinition));
Console.WriteLine($"Agent created (id: {agent.Id}, name: {agent.Name}, version: {agent.Version})");
