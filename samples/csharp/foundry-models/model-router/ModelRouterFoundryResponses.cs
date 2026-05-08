#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

#:package Azure.Identity@1.21.0
#:package Azure.AI.Projects@2.1.0-beta.1
#:package Microsoft.Extensions.Configuration@9.0.5
#:package Microsoft.Extensions.Configuration.Json@9.0.5

using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using OpenAI.Responses;

var config = new ConfigurationBuilder()
     .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .Build();

string foundryEndpoint = config["AzureFoundry:Endpoint"]!;
string modelRouterDeploymentName = config["AzureFoundry:ModelDeploymentName"]!;

AIProjectClient client = new(
    endpoint: new Uri(foundryEndpoint), 
    tokenProvider: new AzureCliCredential());

var responsesClient = client.ProjectOpenAIClient
    .GetProjectResponsesClientForModel(modelRouterDeploymentName);

var inputItems = new[]
{
    ResponseItem.CreateSystemMessageItem("You are a helpful assistant."),
    ResponseItem.CreateUserMessageItem("In one sentence, name the most popular tourist destination in Seattle.")
};

var response = await responsesClient.CreateResponseAsync(new CreateResponseOptions(modelRouterDeploymentName, inputItems));

Console.WriteLine("\n--- Chat Completions Response ---");
Console.WriteLine($"\nRouted to model: {response.Value.Model}");
Console.WriteLine($"\nResponse:\n{response.Value.GetOutputText()}");
Console.WriteLine(
    $"\nUsage: {response.Value.Usage.InputTokenCount} prompt + {response.Value.Usage.OutputTokenCount} completion = {response.Value.Usage.TotalTokenCount} total tokens"
);