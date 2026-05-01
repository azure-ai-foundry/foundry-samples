// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using HandlingAttachments;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

// Add AgentApplicationOptions from appsettings section "AgentApplication".
builder.AddAgentApplicationOptions();

// Register FileDownloaders
builder.Services.AddSingleton<IList<IInputFileDownloader>>(sp => [
    new AttachmentDownloader(sp.GetService<IHttpClientFactory>()!),
    new M365AttachmentDownloader(sp.GetService<IConnections>()!, sp.GetService<IHttpClientFactory>()!)
]);

// Add the AgentApplication, which contains the logic for responding to
// user messages.
builder.AddAgent<AttachmentsAgent>();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operates correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Configure the HTTP request pipeline.

// Add AspNet token validation for Azure Bot Service and Entra.  Authentication is
// configured in the appsettings.json "TokenValidation" section.
builder.Services.AddControllers();
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

WebApplication app = builder.Build();

// Enable AspNet authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

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
