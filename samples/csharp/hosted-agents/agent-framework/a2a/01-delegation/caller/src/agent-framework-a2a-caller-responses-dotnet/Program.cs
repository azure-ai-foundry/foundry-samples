// Copyright (c) Microsoft. All rights reserved.

/*
 * A2A Caller — concierge agent that delegates over A2A (C#, Agent Framework)
 *
 * Hosted Responses-protocol agent that loads a Foundry Toolbox containing one
 * `a2a_preview` tool. The toolbox proxies tool calls to a remote A2A-compatible
 * executor agent through a Foundry `RemoteA2A` project connection.
 *
 * The toolbox + connection are created out-of-band by
 * `executor/scripts/setup-a2a.{sh,ps1}` — this agent only references the
 * toolbox by name via the TOOLBOX_NAME environment variable and registers it
 * with the hosting layer, which discovers the toolbox's tools at startup and
 * injects them into every request as server-side tools (Foundry handles tool
 * discovery and invocation through the Responses API).
 *
 * Required environment variables:
 *   FOUNDRY_PROJECT_ENDPOINT       — Foundry project endpoint (auto-injected in hosted containers)
 *   AZURE_AI_MODEL_DEPLOYMENT_NAME — Model deployment name (declared in agent.manifest.yaml)
 *   TOOLBOX_NAME                   — Name of the Foundry Toolbox created by setup-a2a
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
// at request time (see AddFoundryToolboxes below). For the `a2a_preview` tool this means
// proxying calls to the executor's A2A endpoint through the `RemoteA2A` connection that
// backs the toolbox.
AIAgent agent = new AIProjectClient(projectEndpoint, new DefaultAzureCredential())
    .AsAIAgent(
        model: deployment,
        instructions: """
            You are a friendly concierge agent. When the user asks a question that is
            best answered by a specialist, delegate the request to the remote agent
            that is exposed through the A2A tool, then summarize the result back to the
            user in a concise, friendly tone. If no remote skill is relevant, answer
            directly.
            """,
        name: "agent-framework-a2a-caller-responses-dotnet",
        description: "Concierge agent that delegates to a Foundry-hosted A2A executor agent.");

var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(agent);

// Register the Foundry Toolbox. At startup the hosting layer connects to the toolbox's
// managed MCP proxy (derived from FOUNDRY_PROJECT_ENDPOINT), discovers its tools, and
// injects them into every request. Omitting a version resolves the toolbox's current
// default version.
builder.Services.AddFoundryToolboxes(toolboxName);

builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();
