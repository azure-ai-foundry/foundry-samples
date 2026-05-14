// Copyright (c) Microsoft. All rights reserved.

using Azure;
using Azure.AI.AgentServer.Core;
using Azure.AI.Projects;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using DotNetEnv;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;

Env.TraversePath().Load();

var projectEndpoint = new Uri(Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT environment variable is not set."));
var deployment = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME") ?? "gpt-4.1-mini";
var searchEndpoint = new Uri(Environment.GetEnvironmentVariable("AZURE_SEARCH_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_SEARCH_ENDPOINT environment variable is not set."));
var indexName = Environment.GetEnvironmentVariable("AZURE_SEARCH_INDEX_NAME") ?? "contoso-outdoors";

var credential = new DefaultAzureCredential();

// The index is expected to exist and be populated before the agent runs. See README.md for the
// schema and seed content. Provisioning the index is a one-time setup step, not part of the
// agent runtime.
var searchClient = new SearchClient(searchEndpoint, indexName, credential);

var textSearchOptions = new TextSearchProviderOptions
{
    SearchTime = TextSearchProviderOptions.TextSearchBehavior.BeforeAIInvoke,
    RecentMessageMemoryLimit = 6,
};

AIAgent agent = new AIProjectClient(projectEndpoint, credential)
    .AsAIAgent(new ChatClientAgentOptions
    {
        Name = "azure-search-rag",
        ChatOptions = new ChatOptions
        {
            ModelId = deployment,
            Instructions = "You are a helpful customer support assistant for Contoso Outdoors. " +
                           "Answer questions using the provided context and cite the source document when available. " +
                           "If you cannot find relevant information in the provided context, let the customer know.",
        },
        AIContextProviders = [new TextSearchProvider(CreateSearchAdapter(searchClient), textSearchOptions)]
    });

var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();

// Wraps a SearchClient as the delegate TextSearchProvider expects. Keyword/full-text search;
// no embeddings. Returns the top results and projects them into TextSearchResult entries
// the provider will inject into the model context.
static Func<string, CancellationToken, Task<IEnumerable<TextSearchProvider.TextSearchResult>>>
    CreateSearchAdapter(SearchClient client, int top = 3) =>
    async (query, cancellationToken) =>
    {
        var options = new SearchOptions { Size = top };
        Response<SearchResults<SearchDocument>> response =
            await client.SearchAsync<SearchDocument>(query, options, cancellationToken).ConfigureAwait(false);

        var results = new List<TextSearchProvider.TextSearchResult>();
        await foreach (SearchResult<SearchDocument> hit in response.Value.GetResultsAsync().WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            results.Add(new TextSearchProvider.TextSearchResult
            {
                SourceName = hit.Document.TryGetValue("sourceName", out var name) ? name?.ToString() ?? string.Empty : string.Empty,
                SourceLink = hit.Document.TryGetValue("sourceLink", out var link) ? link?.ToString() ?? string.Empty : string.Empty,
                Text = hit.Document.TryGetValue("content", out var content) ? content?.ToString() ?? string.Empty : string.Empty,
                RawRepresentation = hit
            });
        }

        return results;
    };