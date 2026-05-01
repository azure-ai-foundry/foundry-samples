// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Hosting.AspNetCore.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// Provides extension methods for mapping HTTP endpoints related to Agent applications, including activity protocol
    /// endpoints, proactive messaging, and root informational endpoints, to ASP.NET Core applications.
    /// </summary>
    /// <remarks>These extension methods simplify the integration of Agent-based bots and services into
    /// ASP.NET Core applications by registering standardized endpoints for message processing, proactive operations,
    /// and diagnostics. The methods support configuration of authentication requirements, custom request processing
    /// delegates, and route patterns. Use these extensions to quickly expose bot functionality over HTTP in a
    /// consistent and maintainable manner.</remarks>
    public static class AgentEndpointExtensions
    {
        /// <summary>
        /// The delegate signature for custom AgentApplication request processing methods when specified in AgentInterfaceAttribute.ProcessDelegate.
        /// </summary>
        /// <param name="request">The HTTP request object, typically in a POST handler by a Controller.</param>
        /// <param name="response">The HTTP response object.</param>
        /// <param name="adapter">The IAgentHttpAdapter for this request.</param>
        /// <param name="agent">The bot implementation.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        public delegate Task ProcessRequestDelegate(HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken);

        /// <summary>
        /// Represents an asynchronous method that processes an HTTP request using the specified adapter and agent.
        /// </summary>
        /// <typeparam name="TAdapter">The type of the HTTP adapter used to handle the request. Must implement <see cref="Microsoft.Agents.Hosting.AspNetCore.IAgentHttpAdapter"/>.</typeparam>
        /// <typeparam name="TAgent">The type of the agent that processes the request. Must implement <see cref="Microsoft.Agents.Builder.IAgent"/>.</typeparam>
        /// <param name="request">The HTTP request to be processed.</param>
        /// <param name="response">The HTTP response to be sent.</param>
        /// <param name="adapter">The adapter instance used to facilitate communication between the HTTP layer and the agent.</param>
        /// <param name="agent">The agent instance responsible for handling the request logic.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public delegate Task ProcessRequestDelegate<TAdapter, TAgent>(HttpRequest request, HttpResponse response, TAdapter adapter, TAgent agent, CancellationToken cancellationToken)
            where TAgent : IAgent
            where TAdapter : IAgentHttpAdapter;

        /// <summary>
        /// This adds HTTP endpoints for all AgentApplications defined in the calling assembly.  Each AgentApplication must have been added using <see cref="Microsoft.Agents.Hosting.AspNetCore.ServiceCollectionExtensions.AddAgent{TAgent}(Microsoft.Extensions.Hosting.IHostApplicationBuilder)"/>.
        /// </summary>
        /// <param name="endpoints"></param>
        /// <param name="requireAuth"></param>
        /// <param name="defaultPath"></param>
        /// <exception cref="InvalidOperationException"/>
        public static IEndpointConventionBuilder MapAgentApplicationEndpoints(
            this IEndpointRouteBuilder endpoints,
            bool requireAuth = true,
            [StringSyntax("Route")] string defaultPath = "/api/messages")
        {
            if (string.IsNullOrEmpty(defaultPath))
            {
                defaultPath = "/api/messages";
            }

            var agentGroup = endpoints.MapGroup("");
            if (requireAuth)
            {
                agentGroup.RequireAuthorization();
            }
            else
            {
                agentGroup.AllowAnonymous();
            }

            var allAgents = Assembly.GetCallingAssembly().GetTypes().Where(t => t.IsOrDerives(typeof(AgentApplication))).ToList();
            if (allAgents.Count == 0)
            {
                // This is to handle declaring an AgentApplication in an AddTransient lambda.
                var inlineAgent = endpoints.ServiceProvider.GetService<IAgent>()
                    ?? throw new InvalidOperationException("No AgentApplications were found in the calling assembly. Ensure that at least one AgentApplication is defined.");
                allAgents.Add(inlineAgent.GetType());
            }

            foreach (var agent in allAgents)
            {
                var interfaces = agent.GetCustomAttributes<AgentInterfaceAttribute>(true)?.ToList();
                if (interfaces?.Count == 0)
                {
                    if (allAgents.Count == 1)
                    {
                        // If there is only one AgentApplication, we can default
                        interfaces = new List<AgentInterfaceAttribute>()
                        {
                            new(AgentTransportProtocol.ActivityProtocol, defaultPath)
                        };
                    }
                    else
                    {
                        throw new InvalidOperationException($"No AgentInterfaceAttribute was found on Agent '{agent.FullName}'. When multiple AgentApplications are defined, each must have at least one AgentInterfaceAttribute.");
                    }
                }

                foreach (var agentInterface in interfaces)
                {
                    if (agentInterface.Protocol != AgentTransportProtocol.ActivityProtocol)
                    {
                        // Currently only ActivityProtocol is supported here.
                        continue;
                    }

                    agentGroup.MapMethods(agentInterface.Path, ["POST"],
                        async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IServiceProvider services, CancellationToken cancellationToken) =>
                        {
                            IAgent agentInstance = (IAgent)services.GetService(agent);
                            // This is to handle declaring an AgentApplication in an AddTransient lambda.
                            agentInstance ??= (IAgent)services.GetRequiredService(typeof(IAgent));

                            if (!string.IsNullOrEmpty(agentInterface.ProcessDelegate))
                            {
                                var processMethod = agentInstance.GetType().GetMethod(agentInterface.ProcessDelegate, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                                    ?? throw new InvalidOperationException($"The specified ProcessDelegate '{agentInterface.ProcessDelegate}' was not found on AgentApplication '{agentInstance.GetType().FullName}'.");

#if !NETSTANDARD
                                var processDelegate = processMethod.CreateDelegate<ProcessRequestDelegate>(agentInstance);
#else
                                var processDelegate = (ProcessRequestDelegate)processMethod.CreateDelegate(typeof(ProcessRequestDelegate), agentInstance);
#endif

                                await processDelegate(request, response, adapter, agentInstance, cancellationToken).ConfigureAwait(false);
                            }
                            else
                            {
                                await adapter.ProcessAsync(request, response, agentInstance, cancellationToken).ConfigureAwait(false);
                            }
                        });
                }
            }

            return agentGroup;
        }

        /// <summary>
        /// Maps Agent Activity Protocol endpoints.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="requireAuth">Defaults to true.  Use false to allow anonymous requests (recommended for Development only)</param>
        /// <param name="path">Optional: Indicate the route pattern, defaults to "/api/messages"</param>
        /// <param name="process">Optional: Action to handle request processing.  Defaults to IAgentHttpAdapter.ProcessAsync.</param>
        /// <returns>Returns a builder for configuring additional endpoint conventions like authorization policies.</returns>
        public static IEndpointConventionBuilder MapAgentEndpoints(
            this WebApplication app,
            bool requireAuth = true,
            [StringSyntax("Route")] string path = "/api/messages",
            ProcessRequestDelegate<IAgentHttpAdapter, IAgent> process = null)
        {
            return AgentEndpointExtensions.MapAgentEndpoints<IAgentHttpAdapter, IAgent>(app, requireAuth, path, (ProcessRequestDelegate<IAgentHttpAdapter, IAgent>)process);
        }

        /// <summary>
        /// Maps Agent Activity Protocol endpoints.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="requireAuth">Defaults to true.  Use false to allow anonymous requests (recommended for Development only)</param>
        /// <param name="path">Optional: Indicate the route pattern, defaults to "/api/messages"</param>
        /// <param name="process">Optional: Action to handle request processing.  Defaults to IAgentHttpAdapter.ProcessAsync.</param>
        /// <returns>Returns a builder for configuring additional endpoint conventions like authorization policies.</returns>
        public static IEndpointConventionBuilder MapAgentEndpoints<TAgent>(
            this WebApplication app,
            bool requireAuth = true,
            [StringSyntax("Route")] string path = "/api/messages",
            ProcessRequestDelegate<IAgentHttpAdapter, TAgent> process = null)
            where TAgent : IAgent
        {
            return AgentEndpointExtensions.MapAgentEndpoints<IAgentHttpAdapter, TAgent>(app, requireAuth, path, (ProcessRequestDelegate<IAgentHttpAdapter, TAgent>)process);
        }

        /// <summary>
        /// Maps Agent Activity Protocol endpoints.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="requireAuth">Defaults to true.  Use false to allow anonymous requests (recommended for Development only)</param>
        /// <param name="path">Optional: Indicate the route pattern, defaults to "/api/messages"</param>
        /// <param name="process">Optional: Action to handle request processing.  Defaults to IAgentHttpAdapter.ProcessAsync.</param>
        /// <returns>Returns a builder for configuring additional endpoint conventions like authorization policies.</returns>
        public static IEndpointConventionBuilder MapAgentEndpoints<TAdapter, TAgent>(
            this WebApplication app,
            bool requireAuth = true,
            [StringSyntax("Route")] string path = "/api/messages",
            ProcessRequestDelegate<TAdapter, TAgent> process = null)
            where TAgent : IAgent
            where TAdapter : IAgentHttpAdapter
        {
            var agentGroup = app.MapGroup("");
            if (requireAuth)
            {
                agentGroup.RequireAuthorization();
            }
            else
            {
                agentGroup.AllowAnonymous();
            }

            // This receives incoming messages from Azure Bot Service or other SDK Agents
            agentGroup.MapPost(path, async (HttpRequest request, HttpResponse response, TAdapter adapter, TAgent agent, CancellationToken cancellationToken) =>
            {
                if (process != null)
                {
                    await process(request, response, adapter, agent, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await adapter.ProcessAsync(request, response, agent, cancellationToken).ConfigureAwait(false);
                }
            });

            return agentGroup;
        }

        /// <summary>
        /// Maps the root endpoint ('/') of the web application to return the assembly name and version information for
        /// the Microsoft Agents SDK.
        /// </summary>
        /// <remarks>The root endpoint provides a simple informational response containing the name and
        /// version of the calling assembly. This can be useful for diagnostics or verifying deployment
        /// details.</remarks>
        /// <param name="app">The web application instance to which the root endpoint will be mapped.</param>
        public static void MapAgentRootEndpoint(this WebApplication app)
        {
            var assemblyName = System.Reflection.Assembly.GetCallingAssembly().GetName().Name;
            var assemblyVersion = System.Reflection.Assembly.GetCallingAssembly().GetName().Version?.ToString() ?? "unknown";
            app.MapGet("/", () => $"Microsoft Agents SDK: {assemblyName}, version {assemblyVersion}");
        }

        /// <summary>
        /// Maps proactive conversation endpoints using ContinueConversationAttributes on the agent.
        /// </summary>
        /// <typeparam name="TAgent">The agent application type for which proactive endpoints are mapped. Must inherit from AgentApplication.</typeparam>
        /// <param name="endpoints">The web application to which the proactive endpoints will be added.</param>
        /// <param name="requireAuth">A value indicating whether authentication is required for the mapped endpoints. Defaults to <see
        /// langword="true"/>.</param>
        /// <param name="defaultPath">The base route path for the proactive endpoints. Defaults to "/proactive".</param>
        /// <returns>An endpoint convention builder that can be used to further configure the mapped endpoints.</returns>
        public static IEndpointConventionBuilder MapAgentProactiveEndpoints<TAgent>(
            this IEndpointRouteBuilder endpoints,
            bool requireAuth = true,
            [StringSyntax("Route")] string defaultPath = "/proactive") where TAgent : AgentApplication
        {
            var handlers = new Dictionary<string, ContinueConversationRoute<TAgent>>();
            foreach (var method in typeof(TAgent).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var continueHandler = method.GetCustomAttribute<ContinueConversationAttribute>(true);
                if (continueHandler != null)
                {
                    if (handlers.ContainsKey(continueHandler.Key))
                    {
                        throw ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.HttpProactiveDuplicateContinueKey, null, continueHandler.Key);
                    }
                    handlers.Add(continueHandler.Key, new ContinueConversationRoute<TAgent>(method.Name, continueHandler.TokenHandlers));
                }
            }

            return MapAgentProactiveEndpoints<TAgent>(endpoints, handlers, requireAuth, defaultPath);
        }

        /// <summary>
        /// Maps endpoints for handling proactive messaging operations, such as sending activities to conversations, to
        /// the specified route group in the application's endpoint routing pipeline.
        /// </summary>
        /// <remarks>
        /// /proactive/sendactivity/{conversationId} - sends an activity to a specific conversation using the conversation ID.<br/><br/>
        /// /proactive/sendactivity - sends an activity using a conversation reference record, which includes the necessary information 
        /// to identify the target conversation.<br/><br/>
        /// /proactive/createconversation - creates a new conversation and sends an initial activity to it. The request payload should 
        /// include the necessary information to create the conversation and the activity to be sent.<br/><br/>
        /// </remarks>
        /// <remarks>The mapped endpoints include operations for sending activities to a specific
        /// conversation, sending activities using a conversation reference, and creating new conversations. If
        /// requireAuth is set to true, all endpoints require authorization; otherwise, they allow anonymous access. The
        /// endpoints expect JSON payloads and are grouped under the specified route pattern.</remarks>
        /// <param name="endpoints">The WebApplication to which the proactive messaging endpoints are added.</param>
        /// <param name="continueRoutes"></param>
        /// <param name="requireAuth">true to require authentication for the mapped endpoints; otherwise, false. The default is true.</param>
        /// <param name="defaultPath">The route pattern under which the proactive messaging endpoints are grouped. The default is "/proactive".</param>
        /// <returns>An endpoint convention builder that can be used to further customize the mapped proactive messaging
        /// endpoints.</returns>
        public static IEndpointConventionBuilder MapAgentProactiveEndpoints<TAgent>(
            this IEndpointRouteBuilder endpoints, 
            IDictionary<string, ContinueConversationRoute<TAgent>> continueRoutes, 
            bool requireAuth = true, 
            [StringSyntax("Route")] string defaultPath = "/proactive") where TAgent : AgentApplication
        {
            var routeGroup = endpoints.MapGroup(defaultPath);
            if (requireAuth)
            {
                routeGroup.RequireAuthorization();
            }
            else
            {
                routeGroup.AllowAnonymous();
            }

            routeGroup.MapPost("/sendactivity/{conversationId}", HttpProactive.SendActivityWithConversationIdAsync<TAgent>)
                .WithMetadata(new AcceptsMetadata(["application/json"]));

            routeGroup.MapPost("/sendactivity", HttpProactive.SendActivityWithConversationAsync<TAgent>)
                .WithMetadata(new AcceptsMetadata(["application/json"]));

            foreach (var continueRoute in continueRoutes)
            {
                // Continue with ConversationId in the route
                var withId = string.IsNullOrEmpty(continueRoute.Key) ? "/continue/{conversationId}" : $"/continue/{continueRoute.Key}/{{conversationId}}";
                routeGroup.MapPost(withId, (HttpRequest req, HttpResponse resp, IChannelAdapter adapter, TAgent agent, string conversationId, ILogger<HttpProactive> logger, CancellationToken ct) => 
                    HttpProactive.ContinueConversationWithConversationIdAsync<TAgent>(continueRoute.Value, req, resp, adapter, agent, conversationId, logger, ct))
                    .WithMetadata(new AcceptsMetadata(["application/json"]));

                // Continue with Conversation in the body
                var withConversation = string.IsNullOrEmpty(continueRoute.Key) ? "/continue" : $"/continue/{continueRoute.Key}";
                routeGroup.MapPost(withConversation, (HttpRequest req, HttpResponse resp, IChannelAdapter adapter, TAgent agent, ILogger<HttpProactive> logger, CancellationToken ct) =>
                    HttpProactive.ContinueConversationWithConversationAsync<TAgent>(continueRoute.Value, req, resp, adapter, agent, logger, ct))
                    .WithMetadata(new AcceptsMetadata(["application/json"]));

                // Create with CreateConversation in the body
                var createPattern = string.IsNullOrEmpty(continueRoute.Key) ? "/create" : $"/create/{continueRoute.Key}";
                routeGroup.MapPost(createPattern, (HttpRequest req, HttpResponse resp, IChannelAdapter adapter, TAgent agent, ILogger<HttpProactive> logger, CancellationToken ct) =>
                    HttpProactive.CreateConversationAsync<TAgent>(continueRoute.Value, req, resp, adapter, agent, logger, ct))
                    .WithMetadata(new AcceptsMetadata(["application/json"]));
            }

            return routeGroup;
        }

        private static bool IsOrDerives(this Type type, Type baseType)
        {
            if (type.Equals(baseType))
            {
                return true;
            }

            var current = type.BaseType;
            while (current != null)
            {
                if (current.Equals(baseType))
                {
                    return true;
                }
                current = current.BaseType;
            }
            return false;
        }
    }
}