// Copyright (c) Microsoft. All rights reserved.

using Azure.AI.AgentServer.Core;
using Azure.AI.Projects;
using Azure.Identity;
using DotNetEnv;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Agents.AI.Workflows;

Env.TraversePath().Load();

var projectEndpoint = new Uri(Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT environment variable is not set."));
var deployment = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME") ?? "gpt-4o";

var projectClient = new AIProjectClient(projectEndpoint, new DefaultAzureCredential());

// Create individual translation agents
AIAgent englishToFrench = projectClient.AsAIAgent(
    model: deployment,
    instructions: """
        You are a professional translator. Translate the user's input text into French.
        Only output the translated text, nothing else. Do not add explanations or notes.
        """,
    name: "english-to-french",
    description: "Translates English text to French");

AIAgent frenchToSpanish = projectClient.AsAIAgent(
    model: deployment,
    instructions: """
        You are a professional translator. Translate the user's input text into Spanish.
        Only output the translated text, nothing else. Do not add explanations or notes.
        """,
    name: "french-to-spanish",
    description: "Translates French text to Spanish");

AIAgent spanishToEnglish = projectClient.AsAIAgent(
    model: deployment,
    instructions: """
        You are a professional translator. Translate the user's input text back into English.
        Only output the translated text, nothing else. Do not add explanations or notes.
        """,
    name: "spanish-to-english",
    description: "Translates Spanish text to English");

// Build a sequential translation chain: English → French → Spanish → English
AIAgent agent = AgentWorkflowBuilder
    .BuildSequential("translation-chain", englishToFrench, frenchToSpanish, spanishToEnglish)
    .AsAIAgent(
        name: "translation-chain",
        description: "A translation workflow that chains English → French → Spanish → English");

var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();
