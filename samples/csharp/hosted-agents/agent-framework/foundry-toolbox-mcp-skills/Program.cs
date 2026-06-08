// Copyright (c) Microsoft. All rights reserved.

/*
 * Foundry Toolbox MCP Skills - Agent Framework Responses agent for C#
 *
 * Hosted agent that discovers MCP-based skills from a Foundry Toolbox and exposes
 * them to the agent via AgentSkillsProviderBuilder.UseMcpSkills(mcpClient).
 *
 * The AgentSkillsProvider implements the progressive-disclosure pattern from the
 * Agent Skills specification (https://agentskills.io/):
 *   1. Advertise - skill names and descriptions are injected into the system prompt.
 *   2. Load      - the model retrieves the full skill body on demand.
 *   3. Read      - supplementary skill resources (reference documents, assets) are
 *                  fetched on demand.
 *
 * The full skill body and resources are only fetched from the toolbox when the model
 * actually needs them, reducing token usage.
 *
 * Required environment variables (values set by `azd ai agent init`):
 *   FOUNDRY_PROJECT_ENDPOINT       - Foundry project endpoint.
 *   AZURE_AI_MODEL_DEPLOYMENT_NAME - Model deployment name.
 *   TOOLBOX_NAME                   - Name of the Foundry Toolbox to connect to. The
 *                                    toolbox must already be provisioned with
 *                                    MCP-based skills before the agent starts.
 */

using System.Net.Http.Headers;
using Azure.AI.Projects;
using Azure.Core;
using Azure.Identity;
using DotNetEnv;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

// Load .env file if present (for local development).
Env.TraversePath().Load();

string projectEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT environment variable is not set.");

string deployment = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("AZURE_AI_MODEL_DEPLOYMENT_NAME environment variable is not set.");

string toolboxName = Environment.GetEnvironmentVariable("TOOLBOX_NAME")
    ?? throw new InvalidOperationException("TOOLBOX_NAME environment variable is not set.");

// Build the Foundry Toolbox MCP URL from the project endpoint and toolbox name.
string toolboxMcpServerUrl = $"{projectEndpoint.TrimEnd('/')}/toolboxes/{toolboxName}/mcp?api-version=v1";

TokenCredential credential = new DefaultAzureCredential();

// HttpClient that attaches a fresh Foundry bearer token to every request.
// CheckCertificateRevocationList = true satisfies CA5399.
using var httpClient = new HttpClient(
    new BearerTokenHandler(credential, "https://ai.azure.com/.default")
    {
        CheckCertificateRevocationList = true,
    });

Console.WriteLine($"Connecting to Foundry Toolbox '{toolboxName}' MCP server...");

// Connect to the Foundry Toolbox MCP endpoint.
// The Foundry-Features: Toolboxes=V1Preview opt-in header is required while the
// toolbox MCP surface is in preview.
await using var mcpClient = await McpClient.CreateAsync(
    new HttpClientTransport(
        new HttpClientTransportOptions
        {
            Endpoint = new Uri(toolboxMcpServerUrl),
            Name = toolboxName,
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string>
            {
                ["Foundry-Features"] = "Toolboxes=V1Preview",
            },
        },
        httpClient));

// AgentSkillsProvider implements progressive disclosure over the MCP-discovered skills:
// names and descriptions are advertised in the system prompt, and the full skill body
// (and any supplementary resources) is loaded on demand when the model decides it is
// relevant.
var skillsProvider = new AgentSkillsProviderBuilder()
    .UseMcpSkills(mcpClient)
    .Build();

AIAgent agent = new AIProjectClient(new Uri(projectEndpoint), credential)
    .AsAIAgent(new ChatClientAgentOptions
    {
        Name = "foundry-toolbox-mcp-skills",
        Description = "Agent that discovers MCP-based skills from a Foundry Toolbox and exposes them via AgentSkillsProvider.",
        ChatOptions = new ChatOptions
        {
            ModelId = deployment,
            Instructions = "You are a helpful assistant.",
        },
        AIContextProviders = [skillsProvider],
    });

var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();

// HttpClientHandler that attaches a fresh Foundry bearer token to every outgoing request.
internal sealed class BearerTokenHandler(TokenCredential credential, string scope) : HttpClientHandler
{
    private readonly TokenRequestContext _tokenContext = new([scope]);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        AccessToken token = await credential.GetTokenAsync(this._tokenContext, cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
