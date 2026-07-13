// Copyright (c) Microsoft. All rights reserved.

/*
 * A2A Executor — math expert exposed over A2A (C#, Agent Framework)
 *
 * A minimal hosted Responses-protocol agent that answers arithmetic / math
 * questions. Once deployed, the companion `scripts/setup-a2a` script turns on
 * incoming A2A on this agent so the caller (or any other A2A client) can reach
 * it through Foundry's A2A endpoint.
 *
 * Required environment variables:
 *   FOUNDRY_PROJECT_ENDPOINT       — Foundry project endpoint (auto-injected in hosted containers)
 *   AZURE_AI_MODEL_DEPLOYMENT_NAME — Model deployment name (declared in agent.manifest.yaml)
 */

using Azure.AI.AgentServer.Core;
using Azure.AI.Projects;
using Azure.Identity;
using DotNetEnv;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;

// Load .env file if present (for local development)
Env.NoClobber().TraversePath().Load();

var projectEndpoint = new Uri(Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT environment variable is not set."));

var deployment = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("AZURE_AI_MODEL_DEPLOYMENT_NAME environment variable is not set.");

AIAgent agent = new AIProjectClient(projectEndpoint, new DefaultAzureCredential())
    .AsAIAgent(
        model: deployment,
        instructions: """
            You are a math expert. When the user asks an arithmetic or algebra question,
            compute the answer carefully and reply with a concise numeric result followed
            by a one-sentence explanation of the steps. If the question is not math-related,
            politely say that you only answer math questions.
            """,
        name: "agent-framework-a2a-executor-responses-dotnet",
        description: "Math expert agent exposed over A2A.");

var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();
