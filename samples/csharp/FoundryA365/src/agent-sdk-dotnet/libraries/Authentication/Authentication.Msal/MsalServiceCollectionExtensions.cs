// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Msal.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net.Http;

namespace Microsoft.Agents.Authentication.Msal
{
    public static class MsalServiceCollectionExtensions
    {
        /// <summary>
        /// Adds support for MsalAuth using the IConfigurationSection "MSALConfiguration".
        /// <see cref="Microsoft.Agents.Authentication.Msal.Model.MsalAuthConfigurationOptions"/>
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <example>
        /// For example:
        /// <code>
        /// "MSALConfiguration": {
        ///    "MSALEnabledLogPII": {default:false}
        ///    "MSALRequestTimeout": {default:30s}
        ///    "MSALRetryCount": {default:3}
        /// }
        /// </code>
        /// </example>
        public static void AddDefaultMsalAuth(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton(Options.Create(MsalAuthConfigurationOptions.CreateFromConfigurationOptions(configuration.GetSection("MSALConfiguration"))));
            services.AddHttpClient("MSALClientFactory", (sp, client) =>
            {
                client.Timeout = sp.GetService<IOptions<MsalAuthConfigurationOptions>>().Value.MSALRequestTimeout;
            })
                .ConfigurePrimaryHttpMessageHandler(() =>
                {
                    var hander = new HttpClientHandler
                    {
                        AutomaticDecompression = System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.GZip
                    };
                    return hander;
                })
                .AddHttpMessageHandler<MSALHttpRetryHandlerHelper>(); // Adding on board retry hander for MSAL.
            services.AddTransient<MSALHttpRetryHandlerHelper>();
        }
    }
}
