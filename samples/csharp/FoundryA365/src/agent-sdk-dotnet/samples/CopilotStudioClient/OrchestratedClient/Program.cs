// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using OrchestratedClientSample;
using Microsoft.Agents.CopilotStudio.Client;

// Setup the Orchestrated Client example.

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Get the configuration settings from the appsettings.json file.
SampleConnectionSettings settings = new SampleConnectionSettings(builder.Configuration.GetSection("CopilotStudioClientSettings"));

// Create an HTTP client with the token handler for authentication.
builder.Services.AddHttpClient("orchestrated").ConfigurePrimaryHttpMessageHandler(() =>
{
    return new AddTokenHandler(settings);
});

// Register settings and the OrchestratedClient in DI.
builder.Services
    .AddSingleton(settings)
    .AddTransient<OrchestratedClient>((s) =>
    {
        var logger = s.GetRequiredService<ILoggerFactory>().CreateLogger<OrchestratedClient>();
        return new OrchestratedClient(settings, s.GetRequiredService<IHttpClientFactory>(), logger, "orchestrated");
    })
    .AddHostedService<OrchestratedChatConsoleService>();

IHost host = builder.Build();
host.Run();
