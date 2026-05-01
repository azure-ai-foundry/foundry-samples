// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using Microsoft.Agents.Builder.App.Proactive;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// Provides extension methods for registering agent-related services, adapters, and middleware with dependency
    /// injection containers and application builders.
    /// </summary>
    /// <remarks>These extension methods simplify the setup of agent applications by enabling the registration
    /// of agents, adapters, options, and supporting middleware. They are intended to be used during application startup
    /// to configure required services for agent-based architectures, such as those using CloudAdapter and
    /// AgentApplication. Methods in this class support both default and custom agent/adapters, and facilitate
    /// integration with ASP.NET Core's dependency injection and middleware pipelines.</remarks>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds an Agent which subclasses <c>AgentApplication</c>
        /// <code>
        /// builder.Services.AddSingleton&lt;IStorage, MemoryStorage&gt;();
        /// builder.AddAgentApplicationOptions();
        /// builder.AddAgent&lt;MyAgent&gt;();
        /// </code>
        /// </summary>
        /// <remarks>
        /// This will also call <see cref="Microsoft.Agents.Hosting.AspNetCore.ServiceCollectionExtensions.AddAgentCore(Microsoft.Extensions.Hosting.IHostApplicationBuilder)"/> and uses <c>CloudAdapter</c>.
        /// The Agent is registered as Transient.
        /// </remarks>
        /// <typeparam name="TAgent"></typeparam>
        /// <param name="builder"></param>
        public static IHostApplicationBuilder AddAgent<TAgent>(this IHostApplicationBuilder builder)
            where TAgent : class, IAgent
        {
            return AddAgent<TAgent, CloudAdapter>(builder);
        }

        /// <summary>
        /// Same as <see cref="Microsoft.Agents.Hosting.AspNetCore.ServiceCollectionExtensions.AddAgent{TAgent}(Microsoft.Extensions.Hosting.IHostApplicationBuilder)"/> but allows for use of
        /// any <c>CloudAdapter</c> subclass.
        /// </summary>
        /// <typeparam name="TAgent"></typeparam>
        /// <typeparam name="TAdapter"></typeparam>
        /// <param name="builder"></param>
        public static IHostApplicationBuilder AddAgent<TAgent, TAdapter>(this IHostApplicationBuilder builder)
            where TAgent : class, IAgent
            where TAdapter : CloudAdapter
        {
            AddAgentCore<TAdapter>(builder);

            // Add the IAgent 
            if (!builder.Services.Any(x => x.ServiceType == typeof(IAgent)))
            {
                // There can only be one IAgent.
                builder.Services.AddTransient<IAgent, TAgent>();
            }

            // Add the TAgent (required for multi agent registrations)
            if (!builder.Services.Any(x => x.ServiceType == typeof(TAgent)))
            {
                // There can only be one TAgent.
                builder.Services.AddTransient<TAgent>();
            }

            return builder;
        }

        /// <summary>
        /// Adds an Agent via lambda construction.
        /// <code>
        /// builder.Services.AddSingleton&lt;IStorage, MemoryStorage&gt;();
        /// builder.AddAgentApplicationOptions();
        /// builder.AddAgent(sp =>
        /// {
        ///    var options = new AgentApplicationOptions()
        ///    {
        ///       TurnStateFactory = () => new TurnState(sp.GetService&lt;IStorage&gt;());
        ///    };
        ///        
        ///    var app = new AgentApplication(options);
        ///
        ///    ...
        ///
        ///    return app;
        /// });
        /// </code>
        /// </summary>
        /// <remarks>
        /// This will also calls <see cref="Microsoft.Agents.Hosting.AspNetCore.ServiceCollectionExtensions.AddAgentCore(Microsoft.Extensions.Hosting.IHostApplicationBuilder)"/> and uses <c>CloudAdapter</c>.
        /// The Agent is registered as Transient.
        /// </remarks>
        /// <param name="builder"></param>
        /// <param name="implementationFactory"></param>
        public static IHostApplicationBuilder AddAgent(this IHostApplicationBuilder builder, Func<IServiceProvider, IAgent> implementationFactory)
        {
            return AddAgent<CloudAdapter>(builder, implementationFactory);
        }

        /// <summary>
        /// This is the same as <see cref="Microsoft.Agents.Hosting.AspNetCore.ServiceCollectionExtensions.AddAgent(Microsoft.Extensions.Hosting.IHostApplicationBuilder, System.Func{System.IServiceProvider, Microsoft.Agents.Builder.IAgent})"/>, except allows the
        /// use of any <c>CloudAdapter</c> subclass.
        /// </summary>
        /// <typeparam name="TAdapter"></typeparam>
        /// <param name="builder"></param>
        /// <param name="implementationFactory"></param>
        public static IHostApplicationBuilder AddAgent<TAdapter>(this IHostApplicationBuilder builder, Func<IServiceProvider, IAgent> implementationFactory)
            where TAdapter : CloudAdapter
        {
            AddAgentCore<TAdapter>(builder);

            builder.Services.AddTransient<IAgent>(implementationFactory);

            return builder;
        }

        /// <summary>
        /// Registers AgentApplicationOptions for AgentApplication-based Agents.
        /// </summary>
        /// <remarks>
        /// This loads options from IConfiguration and DI.  The <c>AgentApplicationOptions</c> is
        /// added as a singleton.
        /// </remarks>
        /// <param name="builder"></param>
        /// <param name="autoSignIn"></param>
        /// <returns></returns>
        public static IHostApplicationBuilder AddAgentApplicationOptions(
            this IHostApplicationBuilder builder,
            AutoSignInSelector autoSignIn = null)
        {
            if (autoSignIn != null)
            {
                builder.Services.AddSingleton<AutoSignInSelector>(sp => autoSignIn);
            }

            builder.Services.AddSingleton<AgentApplicationOptions>();

            return builder;
        }

        /// <summary>
        /// Add the default CloudAdapter.
        /// </summary>
        /// <param name="services"></param>
        public static void AddCloudAdapter(this IServiceCollection services)
        {
            services.AddCloudAdapter<CloudAdapter>();
        }

        /// <summary>
        /// Add a derived CloudAdapter.
        /// </summary>
        /// <param name="services"></param>
        public static void AddCloudAdapter<T>(this IServiceCollection services) where T : CloudAdapter
        {
            AddAsyncAdapterSupport(services);

            if (!services.Any(x => x.ServiceType == typeof(T)))
            {
                services.AddSingleton<CloudAdapter, T>();
                services.AddSingleton<IAgentHttpAdapter>(sp => sp.GetService<CloudAdapter>());
                services.AddSingleton<IChannelAdapter>(sp => sp.GetService<CloudAdapter>());
            }
        }

        /// <summary>
        /// Adds a middleware that collects headers to be propagated.
        /// </summary>
        /// <param name="app">The <see cref="Microsoft.AspNetCore.Builder.IApplicationBuilder"/> to add the middleware to.</param>
        /// <returns>A reference to the <paramref name="app"/> after the operation has completed.</returns>
        public static IApplicationBuilder UseHeaderPropagation(this IApplicationBuilder app)
        {
            ArgumentNullException.ThrowIfNull(app);

            return app.UseMiddleware<HeaderPropagationMiddleware>();
        }

        /// <summary>
        /// Adds the core agent services.
        /// <list type="bullet">
        /// <item><c>IConnections, which uses IConfiguration for settings.</c></item>
        /// <item><c>IChannelServiceClientFactory</c> for ConnectorClient and UserTokenClient creations.  Needed for Azure Bot Service and Agent-to-Agent.</item>
        /// <item><c>CloudAdapter</c>, this is the default adapter that works with Azure Bot Service and Activity Protocol Agents.</item>
        /// </list>
        /// </summary>
        /// <param name="builder"></param>
        public static IHostApplicationBuilder AddAgentCore(this IHostApplicationBuilder builder)
        {
            return builder.AddAgentCore<CloudAdapter>();
        }

        /// <summary>
        /// Adds core services required for Bot Framework Agent functionality, including the specified cloud adapter, to
        /// the application's dependency injection container.
        /// </summary>
        /// <remarks>This method registers essential services such as IConnections and
        /// IChannelServiceClientFactory if they are not already present. It also adds the specified CloudAdapter
        /// implementation, enabling integration with Azure Bot Service and Activity Protocol Agents.</remarks>
        /// <typeparam name="TAdapter">The type of cloud adapter to register. Must inherit from CloudAdapter.</typeparam>
        /// <param name="builder">The host application builder to which the agent core services will be added.</param>
        /// <returns>The same IHostApplicationBuilder instance for chaining further configuration.</returns>
        public static IHostApplicationBuilder AddAgentCore<TAdapter>(this IHostApplicationBuilder builder)
            where TAdapter : CloudAdapter
        {
            if (!builder.Services.Any(x => x.ServiceType == typeof(IConnections)))
            {
                // Add Connections object to access configured token connections.
                builder.Services.AddSingleton<IConnections, ConfigurationConnections>();
            }

            if (!builder.Services.Any(x => x.ServiceType == typeof(IChannelServiceClientFactory)))
            {
                // Add factory for ConnectorClient and UserTokenClient creation
                builder.Services.AddSingleton<IChannelServiceClientFactory, RestChannelServiceClientFactory>();
            }

            // Add the CloudAdapter, this is the default adapter that works with Azure Bot Service and Activity Protocol Agents.
            AddCloudAdapter<TAdapter>(builder.Services);

            return builder;
        }

        /// <summary>
        /// Adds background task and activity processing support to the specified service collection, enabling
        /// asynchronous task execution via hosted services and task queues.
        /// </summary>
        /// <remarks>This method registers hosted services and singleton task queues required for
        /// background and activity processing. It is safe to call multiple times; services are only added if not
        /// already present. Use this method to enable asynchronous task and activity handling in applications that
        /// require background processing.</remarks>
        /// <param name="services">The service collection to which the background task and activity processing services will be added. Cannot
        /// be null.</param>
        public static void AddAsyncAdapterSupport(this IServiceCollection services)
        {
            if (!services.Any(x => x.ServiceType == typeof(IActivityTaskQueue)))
            {
                // Activity specific BackgroundService for processing authenticated activities.
                services.AddHostedService<HostedActivityService>();
                // Generic BackgroundService for processing tasks.
                services.AddHostedService<HostedTaskService>();

                // BackgroundTaskQueue and ActivityTaskQueue are the entry points for
                // the enqueueing activities or tasks to be processed by the BackgroundService.
                services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
                services.AddSingleton<IActivityTaskQueue, ActivityTaskQueue>();
            }
        }
    }
}