// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Msal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Microsoft.Agents.Auth.Tests
{
    internal class SetupServiceCollection
    {
        internal static ServiceProvider GenerateAuthMinServiceProvider(string settingsFile, ITestOutputHelper output)
        {
            IConfiguration config = new ConfigurationBuilder()
               .AddJsonFile(
                   System.IO.Path.Combine("Resources", settingsFile),
                   optional: false,
                   reloadOnChange: true)
               .Build();

            ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
                    builder.AddSimpleConsole(options =>
                    {
                        options.IncludeScopes = true;
                        options.TimestampFormat = "hh:mm:ss ";
                    })
                    .AddConfiguration(config.GetSection("Logging"))
                    .AddProvider(new TraceConsoleLoggingProvider(output)));

            var services = new ServiceCollection();
            services.AddSingleton(loggerFactory);
            services.AddSingleton(config);

            services.AddDefaultMsalAuth(config); 
            
            return services.BuildServiceProvider();
        }
    }
}