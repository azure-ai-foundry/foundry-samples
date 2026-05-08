#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

#:package Azure.Identity@1.21.0
#:package Azure.AI.Projects@2.1.0-beta.1
#:package Microsoft.Extensions.Configuration@9.0.5
#:package Microsoft.Extensions.Configuration.Json@9.0.5

using Azure.Identity;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using System.ClientModel.Primitives;
using OpenAI;

var config = new ConfigurationBuilder()
     .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .Build();

string openAiEndpoint = config["AzureOpenAI:Endpoint"]!;
string modelRouterDeploymentName = config["AzureOpenAI:ModelDeploymentName"]!;

BearerTokenPolicy tokenPolicy = new(
    new AzureCliCredential(),
    "https://ai.azure.com/.default");

ChatClient chatClient = new(
    model: modelRouterDeploymentName,
    authenticationPolicy: tokenPolicy,
    options: new OpenAIClientOptions()
    {
        Endpoint = new Uri(openAiEndpoint)
    }
);

var response = await chatClient.CompleteChatAsync([
     new SystemChatMessage("You are a helpful assistant."),
     new UserChatMessage("In one sentence, name the most popular tourist destination in Seattle.")
]);

Console.WriteLine("\n--- Chat Completions Response ---");
Console.WriteLine($"\nRouted to model: {response.Value.Model}");
Console.WriteLine($"\nResponse:\n{response.Value.Content[0].Text}");
Console.WriteLine(
    $"\nUsage: {response.Value.Usage.InputTokenCount} prompt + {response.Value.Usage.OutputTokenCount} completion = {response.Value.Usage.TotalTokenCount} total tokens"
);
