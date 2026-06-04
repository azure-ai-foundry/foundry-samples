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
 * toolbox by name via the TOOLBOX_NAME environment variable, then passes the
 * resolved tools to the agent as server-side tools (Foundry handles tool
 * discovery and invocation through the Responses API).
 *
 * Required environment variables:
 *   FOUNDRY_PROJECT_ENDPOINT       — Foundry project endpoint (auto-injected in hosted containers)
 *   AZURE_AI_MODEL_DEPLOYMENT_NAME — Model deployment name (declared in agent.manifest.yaml)
 *   TOOLBOX_NAME                   — Name of the Foundry Toolbox created by setup-a2a
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
// server-side tools — Foundry will execute them on the agent's behalf, which for the
// `a2a_preview` tool means proxying calls to the executor's A2A endpoint through the
// `RemoteA2A` connection that backs the toolbox.
var projectClient = new AIProjectClient(projectEndpoint, new DefaultAzureCredential());
var tools = await projectClient.GetToolboxToolsAsync(toolboxName);

AIAgent agent = projectClient
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
        description: "Concierge agent that delegates to a Foundry-hosted A2A executor agent.",
        tools: [.. tools]);

var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();
