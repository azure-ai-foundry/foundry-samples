// Copyright (c) Microsoft. All rights reserved.

/*
 * Foundry Toolbox — Auth Paths — Agent Framework Responses agent for C#
 *
 * Hosted agent that loads a Foundry Toolbox via AddFoundryToolboxes. The hosting
 * layer connects to the Foundry Toolboxes MCP proxy at startup, discovers the
 * toolbox's tools, and makes them available to the agent. The toolbox's MCP tools
 * authenticate to their upstream servers using different paths:
 *
 *   1. Key-based — a CustomKeys project connection injects a Bearer token (GitHub MCP).
 *
 * (Path 2 — Microsoft Entra agent identity — is documented in README.md as an optional
 * additive toolbox entry, because it requires a post-deploy RBAC grant on the agent's
 * managed identity before the toolbox can enumerate it.)
 *
 * The agent process carries NO auth logic. Foundry's toolbox proxy resolves each tool's
 * credential when it proxies the MCP call, so this code is identical regardless of which
 * auth path a given tool uses — the difference lives entirely in agent.manifest.yaml.
 *
 * Required environment variables:
 *   FOUNDRY_PROJECT_ENDPOINT        — Foundry project endpoint (auto-injected in hosted containers)
 *   AZURE_AI_MODEL_DEPLOYMENT_NAME  — Model deployment name (declared in agent.manifest.yaml)
 *   TOOLBOX_NAME                    — Name of the Foundry Toolbox to load
 *   FOUNDRY_AGENT_TOOLSET_ENDPOINT  — Toolbox MCP proxy base URL (auto-injected in hosted containers;
 *                                     set locally to "<project-endpoint>/toolboxes")
 */

#pragma warning disable OPENAI001 // Foundry hosting APIs are experimental

using Azure.AI.AgentServer.Core;
using Azure.AI.Projects;
using Azure.Identity;
using DotNetEnv;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.DependencyInjection;

// Load .env file if present (for local development)
Env.TraversePath().Load();

var projectEndpoint = new Uri(Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT environment variable is not set."));

var deployment = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("AZURE_AI_MODEL_DEPLOYMENT_NAME environment variable is not set.");

var toolboxName = Environment.GetEnvironmentVariable("TOOLBOX_NAME")
    ?? throw new InvalidOperationException("TOOLBOX_NAME environment variable is not set.");

// The agent is backed by the project's Responses API. It carries no tools directly —
// the toolbox's tools are registered below via AddFoundryToolboxes and injected by the
// hosting layer at request time as host-executed MCP tools.
var projectClient = new AIProjectClient(projectEndpoint, new DefaultAzureCredential());

// Hosted containers inject FOUNDRY_AGENT_TOOLSET_ENDPOINT (the toolbox MCP proxy base).
// When it is absent (local dev, or environments that don't inject it), derive it from the
// project endpoint so AddFoundryToolboxes can still reach the toolbox proxy.
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FOUNDRY_AGENT_TOOLSET_ENDPOINT")))
{
    Environment.SetEnvironmentVariable(
        "FOUNDRY_AGENT_TOOLSET_ENDPOINT",
        $"{projectEndpoint.ToString().TrimEnd('/')}/toolboxes");
}

AIAgent agent = projectClient
    .AsAIAgent(
        model: deployment,
        instructions: "You are a developer assistant with access to Foundry toolbox tools that reach "
                    + "external services over authenticated paths. Use the GitHub tools to "
                    + "search issues and pull requests. Be concise and cite the tool you used.",
        name: "toolbox-auth-paths",
        description: "Agent with an authenticated Foundry Toolbox using server-side tools.");

var builder = AgentHost.CreateBuilder(args);

builder.Services.AddFoundryResponses(agent);

// Register the Foundry Toolbox. The hosting layer connects to the toolbox MCP proxy at
// startup (FOUNDRY_AGENT_TOOLSET_ENDPOINT, auto-injected by the platform), discovers the
// toolbox's tools, and injects them into the agent. The platform proxy resolves each
// tool's connection credential, so the agent runs under its own identity.
builder.Services.AddFoundryToolboxes(options => options.ApiVersion = "v1", toolboxName);

builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();
