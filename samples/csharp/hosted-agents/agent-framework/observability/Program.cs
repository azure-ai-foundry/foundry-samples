// Copyright (c) Microsoft. All rights reserved.

using Azure.AI.AgentServer.Core;
using Azure.AI.Projects;
using Azure.Identity;
using DotNetEnv;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;

Env.TraversePath().Load();

var projectEndpoint = new Uri(Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT environment variable is not set."));
var deployment = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME") ?? "gpt-4.1-mini";

AIAgent agent = new AIProjectClient(projectEndpoint, new DefaultAzureCredential())
    .AsAIAgent(
        model: deployment,
        instructions: "You are a friendly assistant. Keep your answers brief.",
        name: "observability",
        description: "An instrumented agent demonstrating Foundry hosted-agent observability.",
        tools:
        [
            AIFunctionFactory.Create(GetCurrentLocation, "GetCurrentLocation",
                "Get the current location of the user."),
            AIFunctionFactory.Create(GetWeather, "GetWeather",
                "Get the weather for a given location.")
        ]);

var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();

static string GetCurrentLocation()
{
    string[] locations = ["New York", "London", "Paris", "Tokyo"];
    return locations[Random.Shared.Next(locations.Length)];
}

static string GetWeather(string location)
{
    string[] conditions = ["sunny", "cloudy", "rainy", "stormy"];
    var condition = conditions[Random.Shared.Next(conditions.Length)];
    var temperature = Random.Shared.Next(10, 31);
    return $"The weather in {location} is {condition} with a high of {temperature}°C.";
}
