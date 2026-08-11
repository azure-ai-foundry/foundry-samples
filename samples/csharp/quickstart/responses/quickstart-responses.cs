using Azure.Identity;
using Azure.AI.Projects;
using Azure.AI.Extensions.OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001

// Format: "https://resource_name.ai.azure.com/api/projects/project_name"
var projectEndpoint = Environment.GetEnvironmentVariable("AZURE_AI_PROJECT_ENDPOINT") ?? "your_project_endpoint";
var modelDeployment = Environment.GetEnvironmentVariable("MODEL_DEPLOYMENT") ?? "gpt-5-mini";

// Create project client to call Foundry API
AIProjectClient projectClient = new(
    endpoint: new Uri(projectEndpoint),
    tokenProvider: new DefaultAzureCredential());

// Run a responses API call
ProjectResponsesClient responseClient = projectClient.ProjectOpenAIClient.GetProjectResponsesClientForModel(modelDeployment);
ResponseResult response = await responseClient.CreateResponseAsync(
    "What is the size of France in square miles?");
string outputText = response.GetOutputText();
if (string.IsNullOrWhiteSpace(outputText))
{
    throw new InvalidOperationException("Response output text was empty.");
}

Console.WriteLine(outputText);
