using Azure.Identity;
using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001

// Format: "https://resource_name.ai.azure.com/api/projects/project_name"
var projectEndpoint = "your_project_endpoint";

// Create project client to call Foundry API
AIProjectClient projectClient = new(
    endpoint: new Uri(projectEndpoint),
    tokenProvider: new DefaultAzureCredential());

// Run a responses API call
ProjectResponsesClient responseClient = projectClient.OpenAI.GetProjectResponsesClientForModel("gpt-5-mini");
ResponseResult response = await responseClient.CreateResponseAsync(
    "What is the size of France in square miles?");
Console.WriteLine(response.GetOutputText());
