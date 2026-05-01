// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// See https://aka.ms/new-console-template for more information
using System.ClientModel;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Extensions.AI;
using Azure.AI.OpenAI;
using EvalClient;

#nullable disable

// Setup the Direct To Engine client example. 

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Get the configuration settings for the DirectToEngine client from the appsettings.json file.
EvalClientConfig settings = new EvalClientConfig(builder.Configuration.GetSection("DirectToEngineSettings"));

// Create an http client for use by the DirectToEngine Client and add the token handler to the client.
builder.Services.AddHttpClient("mcs").ConfigurePrimaryHttpMessageHandler(() => new AddTokenHandler(settings));

// add Settings and an instance of the Direct To engine Copilot Client to the Current services.  
builder.Services
    .AddSingleton(settings)
    .AddTransient<CopilotClient>((s) =>
        {
            var logger = s.GetRequiredService<ILoggerFactory>().CreateLogger<CopilotClient>();
            return new CopilotClient(settings, s.GetRequiredService<IHttpClientFactory>(), logger, "mcs");
        }
    )
    .AddTransient<IChatClient>((s) =>
        {
            return new AzureOpenAIClient(new Uri(settings.AzureOpenAiEndpoint), new ApiKeyCredential(settings.AzureOpenAiKey))
                    .GetChatClient(settings.AzureOpenAiDeployment).AsIChatClient();        
        }    
    )
    .AddHostedService<EvaluationService>();
IHost host = builder.Build();
await host.StartAsync();



