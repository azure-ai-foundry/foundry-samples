// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CopilotStudioClientSample;
using Microsoft.Agents.CopilotStudio.Client;

// Setup the Direct To Engine client example. 

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Get the configuration settings for the DirectToEngine client from the appsettings.json file.
SampleConnectionSettings settings = new SampleConnectionSettings(builder.Configuration.GetSection("CopilotStudioClientSettings"));

// Create an http client for use by the DirectToEngine Client and add the token handler to the client.
builder.Services.AddHttpClient("mcs").ConfigurePrimaryHttpMessageHandler(() =>
{
    if (settings.UseS2SConnection)
    {
        return new AddTokenHandlerS2S(settings);
    }
    else
    {
        return new AddTokenHandler(settings);
    }
});

// add Settings and an instance of the Direct To engine Copilot Client to the Current services.  
builder.Services
    .AddSingleton(settings)
    .AddTransient<CopilotClient>((s) =>
    {
        var logger = s.GetRequiredService<ILoggerFactory>().CreateLogger<CopilotClient>();
        return new CopilotClient(settings, s.GetRequiredService<IHttpClientFactory>(), logger, "mcs");
    })
    .AddHostedService<ChatConsoleService>();
IHost host = builder.Build();
host.Run();

