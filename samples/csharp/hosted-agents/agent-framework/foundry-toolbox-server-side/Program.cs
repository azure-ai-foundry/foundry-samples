// Copyright (c) Microsoft. All rights reserved.

/*
 * Foundry Toolbox (Server-Side Tools) — Agent Framework Responses agent for C#
 *
 * Hosted agent that loads a Foundry Toolbox and passes its tools to the agent as
 * SERVER-SIDE tools. The Foundry platform handles tool discovery and invocation
 * through the Responses API — the agent process does not connect to the toolbox
 * MCP proxy or invoke tools locally.
 *
 * Required environment variables:
 *   FOUNDRY_PROJECT_ENDPOINT       — Foundry project endpoint (auto-injected in hosted containers)
 *   AZURE_AI_MODEL_DEPLOYMENT_NAME — Model deployment name (declared in agent.manifest.yaml)
 *   TOOLBOX_NAME                   — Name of the Foundry Toolbox to load
 */

#pragma warning disable OPENAI001 // GetToolboxToolsAsync is experimental

using Azure.AI.AgentServer.Core;
using Azure.AI.Projects;
using Azure.Identity;
using DotNetEnv;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;

// Load .env file if present (for local development)
Env.TraversePath().Load();

var projectEndpoint = new Uri(Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT environment variable is not set."));

var deployment = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("AZURE_AI_MODEL_DEPLOYMENT_NAME environment variable is not set.");

var toolboxName = Environment.GetEnvironmentVariable("TOOLBOX_NAME")
    ?? throw new InvalidOperationException("TOOLBOX_NAME environment variable is not set.");

// Fetch the toolbox's tools from Foundry. Omitting the version resolves the toolbox's
// current default version. The returned AITools are passed directly to the agent as
// server-side tools — Foundry will execute them on the agent's behalf.
var projectClient = new AIProjectClient(projectEndpoint, new DefaultAzureCredential());
var tools = await projectClient.GetToolboxToolsAsync(toolboxName);

AIAgent agent = projectClient
    .AsAIAgent(
        model: deployment,
        instructions: "You are a helpful assistant with access to Azure AI Foundry toolbox tools. "
                    + "Use the available tools to help answer user questions. Be concise.",
        name: "foundry-toolbox-server-side",
        description: "Agent with Foundry Toolbox integration using server-side tools.",
        tools: [.. tools]);

var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();
