// Copyright (c) Microsoft. All rights reserved.

/*
 * Foundry Toolbox (Server-Side Tools) — Agent Framework Responses agent for C#
 *
 * Hosted agent that consumes a Foundry Toolbox as server-side tools. The Agent
 * Framework hosting layer connects to the toolbox's managed MCP proxy at startup,
 * discovers its tools, and injects them into every request. Tool calls are brokered
 * by the Foundry platform's toolbox proxy, so the agent never hard-codes or locally
 * executes the tools.
 *
 * Required environment variables:
 *   FOUNDRY_PROJECT_ENDPOINT       — Foundry project endpoint (auto-injected in hosted containers)
 *   AZURE_AI_MODEL_DEPLOYMENT_NAME — Model deployment name (declared in agent.manifest.yaml)
 *   TOOLBOX_NAME                   — Name of the Foundry Toolbox to load
 */

#pragma warning disable OPENAI001 // Foundry Toolbox hosting APIs are experimental

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

var toolboxName = Environment.GetEnvironmentVariable("TOOLBOX_NAME")
    ?? throw new InvalidOperationException("TOOLBOX_NAME environment variable is not set.");

// Create the agent. No toolbox tools are wired up here: the hosting layer supplies them
// at request time (see AddFoundryToolboxes below).
AIAgent agent = new AIProjectClient(projectEndpoint, new DefaultAzureCredential())
    .AsAIAgent(
        model: deployment,
        instructions: "You are a helpful assistant with access to Azure AI Foundry toolbox tools. "
                    + "Use the available tools to help answer user questions. Be concise.",
        name: "foundry-toolbox-server-side",
        description: "Agent with Foundry Toolbox integration using server-side tools.");

var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(agent);

// Register the Foundry Toolbox. At startup the hosting layer connects to the toolbox's
// managed MCP proxy (derived from FOUNDRY_PROJECT_ENDPOINT), discovers its tools, and
// injects them into every request. Tool calls are brokered by the Foundry platform, so
// the agent process does not hard-code or locally execute the toolbox's tools. Omitting a
// version resolves the toolbox's current default version.
builder.Services.AddFoundryToolboxes(toolboxName);

builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();
