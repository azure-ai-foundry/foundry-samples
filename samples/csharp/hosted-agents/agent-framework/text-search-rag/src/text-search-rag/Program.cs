// Copyright (c) Microsoft. All rights reserved.

using Azure.AI.AgentServer.Core;
using Azure.AI.Projects;
using Azure.Identity;
using DotNetEnv;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;

Env.TraversePath().Load();

var projectEndpoint = new Uri(Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT environment variable is not set."));
var deployment = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME") ?? "gpt-4o";

AIAgent agent = new AIProjectClient(projectEndpoint, new DefaultAzureCredential())
    .AsAIAgent(
        model: deployment,
        instructions: """
            You are a helpful customer support assistant for an outdoor equipment company.
            Use the available search tools to find relevant information before answering questions.
            Always base your answers on the search results provided.
            If you cannot find relevant information, let the customer know.
            """,
        name: "text-search-rag",
        description: "A RAG-powered customer support assistant with text search capabilities",
        tools:
        [
            AIFunctionFactory.Create(SearchKnowledgeBase, "SearchKnowledgeBase",
                "Searches the company knowledge base for relevant information about products, policies, and procedures.")
        ]);

var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();

static string SearchKnowledgeBase(string query)
{
    // Mock knowledge base search - in production, this would query a vector store or search index
    var knowledgeBase = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["return policy"] = """
            Return Policy: Items can be returned within 30 days of purchase with a valid receipt.
            Items must be in original condition with tags attached. Sale items are final sale and
            cannot be returned. Refunds are processed within 5-7 business days.
            """,
        ["shipping"] = """
            Shipping Information: Standard shipping takes 5-7 business days.
            Express shipping takes 2-3 business days. Free shipping on orders over $50.
            International shipping is available to select countries with delivery in 10-14 business days.
            """,
        ["tent care"] = """
            Tent Care Guide: Always dry your tent completely before storing to prevent mold.
            Use a footprint or ground cloth to protect the tent floor. Clean with mild soap and water.
            Never machine wash or dry your tent. Store loosely in a cool, dry place.
            Apply seam sealer annually for best waterproofing performance.
            """,
        ["warranty"] = """
            Warranty Information: All products come with a 1-year manufacturer warranty.
            Premium products include a lifetime warranty against defects. Warranty does not cover
            normal wear and tear or damage from misuse. Contact support with your order number
            to file a warranty claim.
            """,
        ["hiking boots"] = """
            Hiking Boot Guide: Break in new boots gradually before long hikes.
            Use waterproofing spray to protect leather boots. Replace insoles every 500 miles.
            Clean boots after each hike and allow them to air dry. Store in a cool, dry place
            away from direct sunlight.
            """
    };

    var results = knowledgeBase
        .Where(kvp => query.Split(' ').Any(word => kvp.Key.Contains(word, StringComparison.OrdinalIgnoreCase)
            || kvp.Value.Contains(word, StringComparison.OrdinalIgnoreCase)))
        .Select(kvp => kvp.Value)
        .ToList();

    return results.Count > 0
        ? string.Join("\n\n---\n\n", results)
        : "No relevant information found in the knowledge base.";
}
