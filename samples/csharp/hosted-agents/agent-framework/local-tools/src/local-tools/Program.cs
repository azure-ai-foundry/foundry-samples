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
            You are a helpful Seattle hotel concierge assistant.
            Use the available tools to help customers find hotels in Seattle.
            Provide detailed information about available hotels when asked.
            """,
        name: "local-tools",
        description: "A hotel concierge assistant with local function tools",
        tools:
        [
            AIFunctionFactory.Create(GetAvailableHotels, "GetAvailableHotels",
                "Gets a list of available hotels in Seattle with details about amenities and pricing.")
        ]);

var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();

static string GetAvailableHotels(string? checkInDate = null, string? checkOutDate = null, int? guests = null)
{
    var hotels = new[]
    {
        new
        {
            Name = "The Grand Seattle",
            Location = "Downtown Seattle",
            PricePerNight = 289,
            Rating = 4.7,
            Amenities = new[] { "Free WiFi", "Pool", "Spa", "Restaurant", "Fitness Center" },
            AvailableRooms = 12
        },
        new
        {
            Name = "Pike Place Inn",
            Location = "Near Pike Place Market",
            PricePerNight = 199,
            Rating = 4.5,
            Amenities = new[] { "Free WiFi", "Breakfast Included", "Rooftop Bar" },
            AvailableRooms = 8
        },
        new
        {
            Name = "Space Needle View Hotel",
            Location = "Queen Anne",
            PricePerNight = 349,
            Rating = 4.8,
            Amenities = new[] { "Free WiFi", "Pool", "Restaurant", "Valet Parking", "Concierge Service" },
            AvailableRooms = 5
        },
        new
        {
            Name = "Waterfront Lodge",
            Location = "Seattle Waterfront",
            PricePerNight = 159,
            Rating = 4.3,
            Amenities = new[] { "Free WiFi", "Pet Friendly", "Free Parking" },
            AvailableRooms = 15
        },
        new
        {
            Name = "Capitol Hill Boutique",
            Location = "Capitol Hill",
            PricePerNight = 179,
            Rating = 4.6,
            Amenities = new[] { "Free WiFi", "Breakfast Included", "Fitness Center", "Local Art Gallery" },
            AvailableRooms = 6
        }
    };

    var result = "Available Hotels in Seattle:\n\n";
    foreach (var hotel in hotels)
    {
        result += $"🏨 {hotel.Name}\n";
        result += $"   📍 Location: {hotel.Location}\n";
        result += $"   💰 Price: ${hotel.PricePerNight}/night\n";
        result += $"   ⭐ Rating: {hotel.Rating}/5.0\n";
        result += $"   🛏️ Available Rooms: {hotel.AvailableRooms}\n";
        result += $"   ✨ Amenities: {string.Join(", ", hotel.Amenities)}\n\n";
    }

    if (checkInDate != null)
        result += $"Check-in: {checkInDate}\n";
    if (checkOutDate != null)
        result += $"Check-out: {checkOutDate}\n";
    if (guests != null)
        result += $"Guests: {guests}\n";

    return result;
}
