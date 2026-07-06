// Copyright (c) Microsoft. All rights reserved.

/*
 * Hello World — Agent Framework Responses agent for C#
 *
 * Minimal hosted agent that uses the Microsoft Agent Framework (Microsoft.Agents.AI)
 * to create an AIAgent backed by a Foundry model, then hosts it using AgentHost.CreateBuilder()
 * from Azure.AI.AgentServer.Core with AddFoundryResponses from Microsoft.Agents.AI.Foundry.Hosting.
 *
 * This sample demonstrates the simplest possible Agent Framework integration: the agent
 * framework manages the LLM call, conversation history, and response lifecycle automatically —
 * there is no ResponseHandler subclass to implement. AgentHost.CreateBuilder() handles the
 * HTTP contract, port binding, health probes, SSE lifecycle, and OpenTelemetry tracing.
 *
 * Multi-turn conversation works automatically: on each request the framework calls
 * GetHistoryAsync() internally to build the conversation history from prior turns.
 * Pass previous_response_id from one response as the input to the next call to maintain
 * conversation context. Locally, history is stored in-process (lost on restart); when
 * hosted by Foundry (FOUNDRY_HOSTING_ENVIRONMENT set), it uses durable server-side storage.
 *
 * Required environment variables:
 *   FOUNDRY_PROJECT_ENDPOINT  — Foundry project endpoint (auto-injected in hosted containers)
 *   AZURE_AI_MODEL_DEPLOYMENT_NAME     — Model deployment name (declared in agent.manifest.yaml)
 *
 * Usage:
 *   dotnet run
 *
 *   # Turn 1 — invoke the agent:
 *   curl -sS -X POST http://localhost:8088/responses \
 *     -H "Content-Type: application/json" \
 *     -d '{"input": "What is Microsoft Foundry?", "stream": false}' | jq .
 *
 *   # Turn 2 — follow up using the id from the previous response:
 *   curl -sS -X POST http://localhost:8088/responses \
 *     -H "Content-Type: application/json" \
 *     -d '{"input": "Can you summarize that?", "previous_response_id": "<id>", "stream": false}' | jq .
 */

using Azure.AI.AgentServer.Core;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    Console.Error.WriteLine(
        "[WARNING] APPLICATIONINSIGHTS_CONNECTION_STRING not set — traces will not be sent " +
        "to Application Insights. Set it to enable local telemetry. " +
        "(This variable is auto-injected in hosted Foundry containers — do not declare it in agent.manifest.yaml.)");
}

var projectEndpoint = new Uri(Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT environment variable is not set."));

var deployment = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("AZURE_AI_MODEL_DEPLOYMENT_NAME environment variable is not set.");

// Create an AIAgent backed by a Foundry model.
// The agent framework manages the LLM call, conversation sessions, and response lifecycle.
AIAgent agent = new AIProjectClient(projectEndpoint, new DefaultAzureCredential())
    .AsAIAgent(
        model: deployment,
        instructions: "You are a helpful AI assistant. Be concise and informative.",
        name: "hello-world",
        description: "A minimal Hello World agent using the Agent Framework");

// AgentHost.CreateBuilder() auto-configures:
//   - Kestrel on port 8088 (or the PORT environment variable)
//   - GET /readiness health probe
//   - OpenTelemetry traces and metrics
//   - x-platform-server response header
var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();
