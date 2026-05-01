using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using Xunit.Abstractions;

namespace Microsoft.Agents.TestSupport
{
    public static class ServiceProviderBootStrap
    {
        public static IServiceProvider CreateServiceProvider(ITestOutputHelper output, IConfiguration configuration = null, Dictionary<string, string> configurationDictionary = null)
        {
            var services = new ServiceCollection();

            if (configuration != null)
                services.AddSingleton<IConfiguration>(configuration);

            else if (configurationDictionary != null)
            {
                if (!configurationDictionary.ContainsKey("Logging:LogLevel:Default"))
                {
                    configurationDictionary.Add("Logging:LogLevel:Default", "Trace");
                }

                configuration = new ConfigurationBuilder()
                                   .AddInMemoryCollection(configurationDictionary)
                                   .Build();

                services.AddSingleton<IConfiguration>(configuration);
            }
            else
            {
                configuration = new ConfigurationRoot(new List<IConfigurationProvider>
                {
                    new MemoryConfigurationProvider(new MemoryConfigurationSource
                    {
                        InitialData = new Dictionary<string, string>
                        {
                            { "Logging:LogLevel:Default", "Trace" },
                        }
                    })
                });
                services.AddSingleton<IConfiguration>(configuration);
            }

            services.AddLogging(builder =>
            {
                builder.AddConfiguration(configuration);
                builder.AddSimpleConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.TimestampFormat = "hh:mm:ss ";
                })
                .AddProvider(new TraceConsoleLoggingProvider(output));
                ;
            });

            return services.BuildServiceProvider();
        }
    }
}
