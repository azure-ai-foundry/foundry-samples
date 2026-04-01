using Azure.Identity;
using Azure.AI.Projects;
using Azure.AI.Extensions.OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001

// Format: "https://resource_name.ai.azure.com/api/projects/project_name"
var ProjectEndpoint = "your_project_endpoint";

// Create project client to call Foundry API
AIProjectClient projectClient = new(
    endpoint: new Uri(ProjectEndpoint),
    tokenProvider: new DefaultAzureCredential());

// Run a responses API call
ProjectResponsesClient responseClient = projectClient.ProjectOpenAIClient.GetProjectResponsesClientForModel("gpt-5-mini"); // supports all Foundry direct models
ResponseResult response = await responseClient.CreateResponseAsync(
    "What is the size of France in square miles?");
Console.WriteLine(response.GetOutputText());
