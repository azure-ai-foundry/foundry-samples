// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using DialogRootBot.Bots;
using DialogRootBot.Dialogs;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Client;
using Microsoft.Agents.Client.Compat;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();

// Add the AgentApplication, which contains the logic for responding to user messages.
// In this sample, the RootBot is a ActivityHandler based agent.
builder.AddAgent<RootBot<MainDialog>>();

// Add the Agent-to-Agent handling. This manages communication with other agents and is configured
// in the appsettings.json "Agent" section.  In Bot Framework, this is similar to the setting in
// the "BotFrameworkSkills" section. This is also using the handler to support Bot Framework Skill
// behavior.
builder.AddAgentHost<SkillChannelApiHandler>();

// Register the MainDialog that will be run by the bot.
builder.Services.AddSingleton<MainDialog>();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operates correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Register Conversation state (used by the Dialog system itself).
builder.Services.AddSingleton<ConversationState>();


// Configure the HTTP request pipeline.

WebApplication app = builder.Build();

// For the DialogRootBot to receive responses from DialogSkillBot
app.UseRouting();
app.MapControllers();

// Map GET "/"
app.MapAgentRootEndpoint();

// Map the endpoints for all agents using the [AgentInterface] attribute.
// If there is a single IAgent/AgentApplication, the endpoints will be mapped to (e.g. "/api/message").
app.MapAgentApplicationEndpoints(requireAuth: !app.Environment.IsDevelopment());

if (app.Environment.IsDevelopment())
{
    // Hardcoded for brevity and ease of testing. 
    // In production, this should be set in configuration.
    app.Urls.Add($"http://localhost:3978");
}

app.Run();
