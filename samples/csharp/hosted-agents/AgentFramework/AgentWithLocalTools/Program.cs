// Seattle Hotel Agent - A simple agent with a tool to find hotels in Seattle.
// Uses Microsoft Agent Framework with Azure AI Foundry.
// Ready for deployment to Foundry Hosted Agent service.

using System.ClientModel.Primitives;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using Azure.AI.AgentServer.AgentFramework.Extensions;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Responses;

// Get configuration from environment variables
var endpoint = Environment.GetEnvironmentVariable("AZURE_AI_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_AI_PROJECT_ENDPOINT is not set.");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME") ?? throw new InvalidOperationException("AZURE_AI_MODEL_DEPLOYMENT_NAME is not set.");
Console.WriteLine($"Project Endpoint: {endpoint}");
Console.WriteLine($"Model Deployment: {deploymentName}");
// Simulated hotel data for Seattle
var seattleHotels = new[]
{
    new Hotel("Contoso Suites", 189, 4.5, "Downtown"),
    new Hotel("Fabrikam Residences", 159, 4.2, "Pike Place Market"),
    new Hotel("Alpine Ski House", 249, 4.7, "Seattle Center"),
    new Hotel("Margie's Travel Lodge", 219, 4.4, "Waterfront"),
    new Hotel("Northwind Inn", 139, 4.0, "Capitol Hill"),
    new Hotel("Relecloud Hotel", 99, 3.8, "University District"),
};

[Description("Get available hotels in Seattle for the specified dates. This simulates a call to a hotel availability API.")]
string GetAvailableHotels(
    [Description("Check-in date in YYYY-MM-DD format")] string checkInDate,
    [Description("Check-out date in YYYY-MM-DD format")] string checkOutDate,
    [Description("Maximum price per night in USD (optional, defaults to 500)")] int maxPrice = 500)
{
    try
    {
        // Parse dates
        if (!DateTime.TryParseExact(checkInDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var checkIn))
        {
            return $"Error parsing check-in date. Please use YYYY-MM-DD format.";
        }

        if (!DateTime.TryParseExact(checkOutDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var checkOut))
        {
            return $"Error parsing check-out date. Please use YYYY-MM-DD format.";
        }

        // Validate dates
        if (checkOut <= checkIn)
        {
            return "Error: Check-out date must be after check-in date.";
        }

        var nights = (checkOut - checkIn).Days;

        // Filter hotels by price
        var availableHotels = seattleHotels.Where(h => h.PricePerNight <= maxPrice).ToList();

        if (availableHotels.Count == 0)
        {
            return $"No hotels found in Seattle within your budget of ${maxPrice}/night.";
        }

        // Build response
        var result = new StringBuilder();
        result.AppendLine($"Available hotels in Seattle from {checkInDate} to {checkOutDate} ({nights} nights):");
        result.AppendLine();

        foreach (var hotel in availableHotels)
        {
            var totalCost = hotel.PricePerNight * nights;
            result.AppendLine($"**{hotel.Name}**");
            result.AppendLine($"   Location: {hotel.Location}");
            result.AppendLine($"   Rating: {hotel.Rating}/5");
            result.AppendLine($"   ${hotel.PricePerNight}/night (Total: ${totalCost})");
            result.AppendLine();
        }

        return result.ToString();
    }
    catch (Exception ex)
    {
        return $"Error processing request. Details: {ex.Message}";
    }
}

// Create an agent using OpenAIClient through the Foundry project endpoint.
var agent = new OpenAIClient(
        new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"),
        new OpenAIClientOptions { Endpoint = new Uri($"{endpoint}/openai/v1") })
    .GetResponsesClient(deploymentName)
    .AsAIAgent(
        name: "seattle-hotel-agent",
        instructions: """
            You are a helpful travel assistant specializing in finding hotels in Seattle, Washington.

            When a user asks about hotels in Seattle:
            1. Ask for their check-in and check-out dates if not provided
            2. Ask about their budget preferences if not mentioned
            3. Use the GetAvailableHotels tool to find available options
            4. Present the results in a friendly, informative way
            5. Offer to help with additional questions about the hotels or Seattle

            Be conversational and helpful. If users ask about things outside of Seattle hotels,
            politely let them know you specialize in Seattle hotel recommendations.
            """,
        tools: [AIFunctionFactory.Create(GetAvailableHotels)]);

Console.WriteLine("Seattle Hotel Agent Server running on http://localhost:8088");
await agent.RunAIAgentAsync(telemetrySourceName: "Agents");

// Hotel record for simulated data
record Hotel(string Name, int PricePerNight, double Rating, string Location);
