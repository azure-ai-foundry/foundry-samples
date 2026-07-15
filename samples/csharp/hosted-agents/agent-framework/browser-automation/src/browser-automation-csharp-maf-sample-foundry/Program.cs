// Copyright (c) Microsoft. All rights reserved.

/*
 * Browser Automation — Agent Framework (MAF) hosted agent for C#
 *
 * Browser automation agent that uses Foundry Toolbox to provision a remote
 * Chromium browser and playwright-cli to drive it. Demonstrates:
 *   - MAF AIAgent with AddFoundryToolboxes for automatic MCP tool discovery
 *   - Function invocation middleware to intercept create_session results
 *   - AgentSkillsProvider for progressive skill disclosure
 *   - LiveViewUrlMiddleware: injects live_view_url post-call
 *
 * Required environment variables:
 *   FOUNDRY_PROJECT_ENDPOINT          — Foundry project endpoint (auto-injected)
 *   AZURE_AI_MODEL_DEPLOYMENT_NAME    — Model deployment name
 *
 * Optional:
 *   TOOLBOX_NAME                      — Override default toolbox name
 *   BROWSER_AGENT_PLAYWRIGHT_CLI_TIMEOUT_SECONDS — CLI timeout (default: 180)
 */

using Azure.AI.Projects;
using Azure.Core;
using Azure.Identity;
using DotNetEnv;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;
using BrowserAutomation;

Env.NoClobber().TraversePath().Load();

// ── Configuration ────────────────────────────────────────────────────────────
var projectEndpoint = new Uri(Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? Environment.GetEnvironmentVariable("AZURE_FOUNDRY_PROJECT_ENDPOINT")
    ?? Environment.GetEnvironmentVariable("AZURE_AI_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT environment variable is not set."));
var deployment = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME")
    ?? Environment.GetEnvironmentVariable("BROWSER_AGENT_MODEL")
    ?? throw new InvalidOperationException("AZURE_AI_MODEL_DEPLOYMENT_NAME environment variable is not set.");

var toolboxName = Environment.GetEnvironmentVariable("TOOLBOX_NAME");
if (string.IsNullOrWhiteSpace(toolboxName))
    toolboxName = "browser-automation-tools";
var playwrightCliTimeout = int.TryParse(Environment.GetEnvironmentVariable("BROWSER_AGENT_PLAYWRIGHT_CLI_TIMEOUT_SECONDS"), out var t) ? t : 180;

// Ensure FOUNDRY_AGENT_TOOLSET_ENDPOINT is set — platform doesn't always inject it
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FOUNDRY_AGENT_TOOLSET_ENDPOINT")))
{
    var toolsetEndpoint = $"{projectEndpoint.ToString().TrimEnd('/')}/toolboxes";
    Environment.SetEnvironmentVariable("FOUNDRY_AGENT_TOOLSET_ENDPOINT", toolsetEndpoint);
}

// ── Skills directory ─────────────────────────────────────────────────────────
var skillsDir = Path.Combine(AppContext.BaseDirectory, "skills");
if (!Directory.Exists(skillsDir))
    skillsDir = Path.Combine(Directory.GetCurrentDirectory(), "skills");

// ── System prompt (loaded from prompts/base.md) ──────────────────────────────
var promptsDir = Path.Combine(AppContext.BaseDirectory, "prompts");
if (!Directory.Exists(promptsDir))
    promptsDir = Path.Combine(Directory.GetCurrentDirectory(), "prompts");
var systemPrompt = File.ReadAllText(Path.Combine(promptsDir, "base.md"));

// ── Local tools (playwright-cli + get_live_view_url) ─────────────────────────
List<AITool> localTools =
[
    Tools.MakeRunPlaywrightCli(playwrightCliTimeout),
    Tools.MakeCloseBrowserSession(playwrightCliTimeout),
    Tools.MakeGetLiveViewUrl(),
];

// ── Skills provider (progressive disclosure, no approval gate) ───────────────
var skillsProvider = new AgentSkillsProvider(skillsDir, options: new AgentSkillsProviderOptions
{
    DisableLoadSkillApproval = true,
    DisableRunSkillScriptApproval = true,
});

// ── Create agent ─────────────────────────────────────────────────────────────
// Toolbox MCP tools (create_session etc.) are injected automatically by
// AddFoundryToolboxes via the hosting layer — no manual McpClient needed.
AIAgent baseAgent = new AIProjectClient(projectEndpoint, new DefaultAzureCredential())
    .AsAIAgent(new ChatClientAgentOptions
    {
        Name = "browser-automation",
        Description = "A browser automation agent using Playwright via Foundry Toolbox",
        ChatOptions = new ChatOptions
        {
            ModelId = deployment,
            Instructions = systemPrompt,
            Tools = localTools,
        },
        AIContextProviders = [skillsProvider],
    });

// ── Middleware pipeline ──────────────────────────────────────────────────────
// 1. Function invocation middleware: intercepts create_session results to store
//    cdp_url + live_view_url server-side (model never sees them).
// 2. Agent-level middleware: injects live_view_url into response post-call.
var agent = baseAgent
    .AsBuilder()
    .Use(Middlewares.FunctionInvocationMiddleware)
    .Use(Middlewares.LiveViewUrlMiddleware, Middlewares.LiveViewUrlStreamingMiddleware)
    .Build();

// ── Host setup ───────────────────────────────────────────────────────────────
var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(agent);

// Pass a credential that forces https://ai.azure.com/.default scope for toolbox auth.
// The framework's FoundryToolboxBearerTokenHandler uses cognitiveservices.azure.com which
// some regions reject. This wrapper overrides the scope while using DefaultAzureCredential.
var toolboxCredential = new ToolboxScopedCredential(new DefaultAzureCredential());

builder.Services.AddFoundryToolboxes(toolboxCredential, opt => opt.ApiVersion = "v1", toolboxName);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();
