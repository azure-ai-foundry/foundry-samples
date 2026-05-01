// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Agents.Builder;
using Microsoft.SemanticKernel;
using System.Threading.Tasks;

namespace WeatherAgent.Plugins;

public class WeatherForecastPlugin(ITurnContext turnContext)
{

    /// <summary>
    /// Retrieve the weather forecast for a specific date. This is a placeholder for a real implementation
    /// and currently only returns a random temperature. This would typically call a weather service API.
    /// </summary>
    /// <param name="date">The date as a parsable string</param>
    /// <param name="location">The location to get the weather for</param>
    /// <returns></returns>
    [KernelFunction]
    public async Task<WeatherForecast> GetForecastForDate(string date,  string location)
    {
        string searchingForDate = date;
        if ( DateTime.TryParse(date, out DateTime searchingDate) )
        {
            searchingForDate = searchingDate.ToLongDateString();
        }
        await turnContext.StreamingResponse.QueueInformativeUpdateAsync($"Looking up the Weather in {location} for {searchingForDate}");

        // Simulate a delay for the weather service call
        return new WeatherForecast
        {
            Date = date,
            TemperatureC = Random.Shared.Next(-20, 55)
        };
    }
}
