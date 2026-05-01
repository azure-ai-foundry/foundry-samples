// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using DialogSkillBot.Bots;
using DialogSkillBot.Dialogs;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

// The Dialog that will be run by the bot.
builder.Services.AddSingleton<ActivityRouterDialog>();

// Add the AgentApplication, which contains the logic for responding to
// user messages. In this sample, the Agent is a ActivityHandler based
// agent using Dialogs.
builder.AddAgent<SkillBot<ActivityRouterDialog>>();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operate correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Register Conversation state (used by the Dialog system itself).
builder.Services.AddSingleton<ConversationState>();


// Configure the HTTP request pipeline.

WebApplication app = builder.Build();

// Map GET "/"
app.MapAgentRootEndpoint();

// Map the endpoints for all agents using the [AgentInterface] attribute.
// If there is a single IAgent/AgentApplication, the endpoints will be mapped to (e.g. "/api/message").
app.MapAgentApplicationEndpoints(requireAuth: !app.Environment.IsDevelopment());

if (app.Environment.IsDevelopment())
{
    // Hardcoded for brevity and ease of testing. 
    // In production, this should be set in configuration.
    app.Urls.Add($"http://localhost:39783");
}

app.Run();
