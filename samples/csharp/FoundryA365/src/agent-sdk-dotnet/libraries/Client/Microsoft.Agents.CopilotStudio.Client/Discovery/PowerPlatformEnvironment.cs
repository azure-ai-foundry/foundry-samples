// Copyright (c) Microsoft Corporation. All rights reserved.

// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Agents.CopilotStudio.Client.Discovery
{
    internal class PowerPlatformEnvironment
    {
        /// <summary>
        ///  The API version to use when connecting to the Power Platform API.
        /// </summary>
        static readonly string ApiVersion = "2022-03-01-preview";

        /// <summary>
        /// Gets the Power Platform API connection URL for the given settings.
        /// </summary>
        /// <param name="settings">Configuration Settings to use</param>
        /// <param name="conversationId">Optional, Conversation ID to address</param>
        /// <param name="agentType">Type of Agent being addressed. <see cref="Microsoft.Agents.CopilotStudio.Client.Discovery.AgentType"/></param>
        /// <param name="cloud">Power Platform Cloud Hosting Agent <see cref="Microsoft.Agents.CopilotStudio.Client.Discovery.PowerPlatformCloud"/></param>
        /// <param name="createSubscribeLink">Whether to create a subscribe link for the conversation</param>
        /// <param name="cloudBaseAddress">Power Platform API endpoint to use if Cloud is configured as "other". <see cref="Microsoft.Agents.CopilotStudio.Client.Discovery.PowerPlatformCloud.Other"/> </param>
        /// <param name="directConnectUrl">DirectConnection URL to a given Copilot Studio agent, if provided all other settings are ignored</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentException"></exception>
        public static Uri GetCopilotStudioConnectionUrl(
                ConnectionSettings settings,
                string? conversationId,
                AgentType agentType = AgentType.Published,
                PowerPlatformCloud cloud = PowerPlatformCloud.Prod,
                bool createSubscribeLink = false,
                string? cloudBaseAddress = default,
                string? directConnectUrl = default
            )
        {
            if (string.IsNullOrEmpty(directConnectUrl) && string.IsNullOrEmpty(settings.DirectConnectUrl))
            {
                if (cloud == PowerPlatformCloud.Other && string.IsNullOrWhiteSpace(cloudBaseAddress))
                {
                    throw new ArgumentException("cloudBaseAddress must be provided when PowerPlatformCloudCategory is Other", nameof(cloudBaseAddress));
                }
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
                if (string.IsNullOrEmpty(settings.EnvironmentId))
                {
                    throw new ArgumentException("EnvironmentId must be provided", nameof(settings.EnvironmentId));
                }
                if (string.IsNullOrEmpty(settings.SchemaName))
                {
                    throw new ArgumentException("SchemaName must be provided", nameof(settings.SchemaName));
                }
#pragma warning restore CA2208 // Instantiate argument exceptions correctly
                if (settings.Cloud != null && settings.Cloud != PowerPlatformCloud.Unknown)
                {
                    cloud = settings.Cloud.Value;
                }
                if (cloud == PowerPlatformCloud.Other)
                {
                    if (!string.IsNullOrEmpty(cloudBaseAddress) && Uri.IsWellFormedUriString(cloudBaseAddress, UriKind.Absolute))
                    {
                        cloud = PowerPlatformCloud.Other;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(settings.CustomPowerPlatformCloud) && Uri.IsWellFormedUriString(settings.CustomPowerPlatformCloud, UriKind.RelativeOrAbsolute))
                        {
                            cloud = PowerPlatformCloud.Other;
                            cloudBaseAddress = settings.CustomPowerPlatformCloud;
                        }
                        else
                        {
                            throw new ArgumentException("Either CustomPowerPlatformCloud or cloudBaseAddress must be provided when PowerPlatformCloudCategory is Other");
                        }
                    }
                }
                if (settings.CopilotAgentType != null)
                {
                    agentType = settings.CopilotAgentType.Value;
                }

                cloudBaseAddress ??= "api.unknown.powerplatform.com";

                var host = GetEnvironmentEndpoint(cloud, settings.EnvironmentId!, cloudBaseAddress);
                return CreateUri(settings.SchemaName!, host, agentType, conversationId, createSubscribeLink);
            }
            else
            {
                directConnectUrl ??= settings.DirectConnectUrl;
                if (!string.IsNullOrEmpty(directConnectUrl) && Uri.IsWellFormedUriString(directConnectUrl, UriKind.Absolute))
                {
                    return CreateUri(directConnectUrl!, conversationId, createSubscribeLink);
                }
                else
                {
                    throw new ArgumentException("DirectConnectUrl is invalid");

                }
            }
        }

        /// <summary>
        /// Returns the Power Platform API Audience.
        /// </summary>
        /// <param name="settings">Configuration Settings to use</param>
        /// <param name="cloud">Power Platform Cloud Hosting Agent <see cref="Microsoft.Agents.CopilotStudio.Client.Discovery.PowerPlatformCloud"/></param>
        /// <param name="cloudBaseAddress">Power Platform API endpoint to use if Cloud is configured as "other". <see cref="Microsoft.Agents.CopilotStudio.Client.Discovery.PowerPlatformCloud.Other"/> </param>
        /// <param name="directConnectUrl">DirectConnection URL to a given Copilot Studio agent, if provided all other settings are ignored</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentException"></exception>
        public static string GetTokenAudience(
            ConnectionSettings? settings,
            PowerPlatformCloud cloud = PowerPlatformCloud.Unknown,
            string? cloudBaseAddress = default,
            string? directConnectUrl = default)
        {
            if (string.IsNullOrEmpty(directConnectUrl) && string.IsNullOrEmpty(settings?.DirectConnectUrl))
            {
                if (cloud == PowerPlatformCloud.Other && string.IsNullOrWhiteSpace(cloudBaseAddress))
                {
                    throw new ArgumentException("cloudBaseAddress must be provided when PowerPlatformCloudCategory is Other", nameof(cloudBaseAddress));
                }
                if (settings == null && cloud == PowerPlatformCloud.Unknown)
                {
                    throw new ArgumentException("Either settings or cloud must be provided", nameof(settings));
                }
                if (settings != null && settings.Cloud != null && settings.Cloud != PowerPlatformCloud.Unknown)
                {
                    cloud = settings.Cloud.Value;
                }
                if (cloud == PowerPlatformCloud.Other)
                {
                    if (!string.IsNullOrEmpty(cloudBaseAddress) && Uri.IsWellFormedUriString(cloudBaseAddress, UriKind.Absolute))
                    {
                        cloud = PowerPlatformCloud.Other;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(settings?.CustomPowerPlatformCloud) && Uri.IsWellFormedUriString(settings!.CustomPowerPlatformCloud, UriKind.RelativeOrAbsolute))
                        {
                            cloud = PowerPlatformCloud.Other;
                            cloudBaseAddress = settings.CustomPowerPlatformCloud;
                        }
                        else
                        {
                            throw new ArgumentException("Either CustomPowerPlatformCloud or cloudBaseAddress must be provided when PowerPlatformCloudCategory is Other");
                        }
                    }
                }
                cloudBaseAddress ??= "api.unknown.powerplatform.com";
                return $"https://{GetEndpointSuffix(cloud, cloudBaseAddress)}/.default";
            }
            else
            {
                directConnectUrl ??= settings?.DirectConnectUrl;
                if (!string.IsNullOrEmpty(directConnectUrl) && Uri.IsWellFormedUriString(directConnectUrl, UriKind.Absolute))
                {
                    if (DecodeCloudFromURI(new Uri(directConnectUrl)) == PowerPlatformCloud.Unknown)
                    {
                        PowerPlatformCloud cloudToTest = settings?.Cloud ?? cloud;

                        if (cloudToTest == PowerPlatformCloud.Other || cloudToTest == PowerPlatformCloud.Unknown)
                        {
                            throw new ArgumentException("Unable to resolve the PowerPlatform Cloud from DirectConnectUrl. The Token Audiance resolver requires a specific PowerPlatformCloudCategory.");
                        }
                        if (cloudToTest != PowerPlatformCloud.Unknown)
                        {
                            return $"https://{GetEndpointSuffix(cloudToTest, string.Empty)}/.default";
                        }
                        else
                        {
                            throw new ArgumentException("Unable to resolve the PowerPlatform Cloud from DirectConnectUrl. The Token Audiance resolver requires a specific PowerPlatformCloudCategory.");
                        }
                    }
                    return $"https://{GetEndpointSuffix(DecodeCloudFromURI(new Uri(directConnectUrl)), string.Empty)}/.default";
                }
                else
                {
                    throw new ArgumentException("DirectConnectUrl must be provided when DirectConnectUrl is set");
                }
            }
        }


        /// <summary>
        /// Gets the ExternalOrchestration API connection URL for the given settings.
        /// The URL follows the pattern: copilotstudio/orchestrated/{cdsBotId}/conversations/{conversationId}
        /// </summary>
        /// <param name="settings">Configuration Settings to use</param>
        /// <param name="conversationId">Conversation ID to address</param>
        /// <param name="cloud">Power Platform Cloud Hosting Agent <see cref="Microsoft.Agents.CopilotStudio.Client.Discovery.PowerPlatformCloud"/></param>
        /// <param name="cloudBaseAddress">Power Platform API endpoint to use if Cloud is configured as "other"</param>
        /// <param name="directConnectUrl">DirectConnection URL to a given Copilot Studio agent</param>
        /// <returns>The URI for the ExternalOrchestration API endpoint</returns>
        /// <exception cref="System.ArgumentException">Thrown when required settings are missing or invalid</exception>
        internal static Uri GetOrchestratedConnectionUrl(
                ConnectionSettings settings,
                string conversationId,
                PowerPlatformCloud cloud = PowerPlatformCloud.Prod,
                string? cloudBaseAddress = default,
                string? directConnectUrl = default
            )
        {
            if (string.IsNullOrEmpty(conversationId))
            {
                throw new ArgumentException("conversationId must be provided for orchestrated connections", nameof(conversationId));
            }

            if (string.IsNullOrEmpty(directConnectUrl) && string.IsNullOrEmpty(settings.DirectConnectUrl))
            {
                if (cloud == PowerPlatformCloud.Other && string.IsNullOrWhiteSpace(cloudBaseAddress))
                {
                    throw new ArgumentException("cloudBaseAddress must be provided when PowerPlatformCloudCategory is Other", nameof(cloudBaseAddress));
                }

#pragma warning disable CA2208 // Instantiate argument exceptions correctly
                if (string.IsNullOrEmpty(settings.EnvironmentId))
                {
                    throw new ArgumentException("EnvironmentId must be provided", nameof(settings.EnvironmentId));
                }
                if (string.IsNullOrEmpty(settings.CdsBotId))
                {
                    throw new ArgumentException("CdsBotId must be provided for orchestrated connections", nameof(settings.CdsBotId));
                }
#pragma warning restore CA2208 // Instantiate argument exceptions correctly

                if (settings.Cloud != null && settings.Cloud != PowerPlatformCloud.Unknown)
                {
                    cloud = settings.Cloud.Value;
                }
                if (cloud == PowerPlatformCloud.Other)
                {
                    if (!string.IsNullOrEmpty(cloudBaseAddress) && Uri.IsWellFormedUriString(cloudBaseAddress, UriKind.Absolute))
                    {
                        cloud = PowerPlatformCloud.Other;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(settings.CustomPowerPlatformCloud) && Uri.IsWellFormedUriString(settings.CustomPowerPlatformCloud, UriKind.RelativeOrAbsolute))
                        {
                            cloud = PowerPlatformCloud.Other;
                            cloudBaseAddress = settings.CustomPowerPlatformCloud;
                        }
                        else
                        {
                            throw new ArgumentException("Either CustomPowerPlatformCloud or cloudBaseAddress must be provided when PowerPlatformCloudCategory is Other");
                        }
                    }
                }

                cloudBaseAddress ??= "api.unknown.powerplatform.com";

                var host = GetEnvironmentEndpoint(cloud, settings.EnvironmentId!, cloudBaseAddress);
                return CreateOrchestratedUri(settings.CdsBotId!, host, conversationId);
            }
            else
            {
                directConnectUrl ??= settings.DirectConnectUrl;
                if (!string.IsNullOrEmpty(directConnectUrl) && Uri.IsWellFormedUriString(directConnectUrl, UriKind.Absolute))
                {
                    return CreateOrchestratedUri(directConnectUrl!, conversationId);
                }
                else
                {
                    throw new ArgumentException("DirectConnectUrl is invalid", nameof(directConnectUrl));
                }
            }
        }

        #region Private 

        /// <summary>
        /// Creates the PowerPlatform API connection URL for the given settings.
        /// </summary>
        private static Uri CreateUri(string schemaName, string host, AgentType agentType, string? conversationId, bool createSubscribeLink = false)
        {
            string agentPathName;
            if (AgentType.Published == agentType)
            {
                agentPathName = "dataverse-backed";
            }
            else
            {
                agentPathName = "prebuilt";
            }
            var builder = new UriBuilder();
            builder.Scheme = "https";
            builder.Host = host;
            builder.Query = $"api-version={ApiVersion}";
            if (string.IsNullOrEmpty(conversationId))
            {
                builder.Path = $"/copilotstudio/{agentPathName}/authenticated/bots/{schemaName}/conversations";
            }
            else
            {
                var conversationSuffix = createSubscribeLink ? "/subscribe" : string.Empty;
                builder.Path = $"/copilotstudio/{agentPathName}/authenticated/bots/{schemaName}/conversations/{conversationId}{conversationSuffix}";
            }
            return builder.Uri;
        }

        /// <summary>
        /// Used only when DirectConnectUrl is provided.
        /// </summary>
        /// <param name="baseaddress"></param>
        /// <param name="conversationId"></param>
        /// <param name="createSubscribeLink"></param>
        /// <returns></returns>
        private static Uri CreateUri(string baseaddress, string? conversationId, bool createSubscribeLink)
        {
            var builder = new UriBuilder(baseaddress);
            builder.Query = $"api-version={ApiVersion}";

            // if builder.path ends with /, remove it
#if !NETSTANDARD
            if (builder.Path.EndsWith('/') || builder.Path.EndsWith('\\'))
#else
            if (builder.Path.EndsWith("/") || builder.Path.EndsWith("\\"))
#endif
            {
                builder.Path = builder.Path.Substring(0, builder.Path.Length - 1);
            }

            // if builder.path has /conversations, remove it
            if (builder.Path.Contains("/conversations"))
            {
                builder.Path = builder.Path.Substring(0, builder.Path.IndexOf("/conversations"));
            }

            if (string.IsNullOrEmpty(conversationId))
            {
                builder.Path = $"{builder.Path}/conversations";
            }
            else
            {
                builder.Path = createSubscribeLink
                    ? $"{builder.Path}/conversations/{conversationId}/subscribe"
                    : $"{builder.Path}/conversations/{conversationId}";
            }

            return builder.Uri;
        }

        /// <summary>
        /// Gets the environment endpoint for the Power Platform API.
        /// </summary>
        /// <param name="cloud"></param>
        /// <param name="environmentId"></param>
        /// <param name="cloudBaseAddress"></param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentException"></exception>
        private static string GetEnvironmentEndpoint(PowerPlatformCloud cloud, string environmentId, string? cloudBaseAddress = default)
        {
            if (cloud == PowerPlatformCloud.Other && string.IsNullOrWhiteSpace(cloudBaseAddress))
            {
                throw new ArgumentException("cloudBaseAddress must be provided when PowerPlatformCloudCategory is Other", nameof(cloudBaseAddress));
            }
            cloudBaseAddress ??= "api.unknown.powerplatform.com";

            var normalizedResourceId = environmentId.ToLower().Replace("-", "");
            var idSuffixLength = GetIdSuffixLength(cloud);
            var hexPrefix = normalizedResourceId.Substring(0, normalizedResourceId.Length - idSuffixLength);
            var hexSuffix = normalizedResourceId.Substring(normalizedResourceId.Length - idSuffixLength, idSuffixLength);
            return $"{hexPrefix}.{hexSuffix}.environment.{GetEndpointSuffix(cloud, cloudBaseAddress)}";
        }

        /// <summary>
        /// Gets the base Cloud to attach to the Power Platform API end point. 
        /// </summary>
        /// <param name="category"></param>
        /// <param name="cloudBaseAddress"></param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentException"></exception>
        private static string GetEndpointSuffix(PowerPlatformCloud category, string cloudBaseAddress) => category switch
        {
            PowerPlatformCloud.Local => "api.powerplatform.localhost",
            PowerPlatformCloud.Exp => "api.exp.powerplatform.com",
            PowerPlatformCloud.Dev => "api.dev.powerplatform.com",
            PowerPlatformCloud.Prv => "api.prv.powerplatform.com",
            PowerPlatformCloud.Test => "api.test.powerplatform.com",
            PowerPlatformCloud.Preprod => "api.preprod.powerplatform.com",
            PowerPlatformCloud.FirstRelease => "api.powerplatform.com",
            PowerPlatformCloud.Prod => "api.powerplatform.com",
            PowerPlatformCloud.GovFR => "api.gov.powerplatform.microsoft.us",
            PowerPlatformCloud.Gov => "api.gov.powerplatform.microsoft.us",
            PowerPlatformCloud.High => "api.high.powerplatform.microsoft.us",
            PowerPlatformCloud.DoD => "api.appsplatform.us",
            PowerPlatformCloud.Mooncake => "api.powerplatform.partner.microsoftonline.cn",
            PowerPlatformCloud.Ex => "api.powerplatform.eaglex.ic.gov",
            PowerPlatformCloud.Rx => "api.powerplatform.microsoft.scloud",
            PowerPlatformCloud.Other => cloudBaseAddress,
            _ => throw new ArgumentException($"Invalid cluster category value: {category}", nameof(category)),
        };

        /// <summary>
        ///  Decode scope from DirectConnect URL.
        /// </summary>
        /// <param name="hostUri">This is the URL to decode a Cloud from</param>
        /// <returns></returns>
        private static PowerPlatformCloud DecodeCloudFromURI(Uri hostUri)
        {
            string Host = hostUri.Host.ToLower();
            switch (Host)
            {
                case "api.powerplatform.localhost":
                    return PowerPlatformCloud.Local;
                case "api.exp.powerplatform.com":
                    return PowerPlatformCloud.Exp;
                case "api.dev.powerplatform.com":
                    return PowerPlatformCloud.Dev;
                case "api.prv.powerplatform.com":
                    return PowerPlatformCloud.Prv;
                case "api.test.powerplatform.com":
                    return PowerPlatformCloud.Test;
                case "api.preprod.powerplatform.com":
                    return PowerPlatformCloud.Preprod;
                case "api.powerplatform.com":
                    return PowerPlatformCloud.Prod;
                case "api.gov.powerplatform.microsoft.us":
                    return PowerPlatformCloud.GovFR;
                case "api.high.powerplatform.microsoft.us":
                    return PowerPlatformCloud.High;
                case "api.appsplatform.us":
                    return PowerPlatformCloud.DoD;
                case "api.powerplatform.partner.microsoftonline.cn":
                    return PowerPlatformCloud.Mooncake;
                default:
                    return PowerPlatformCloud.Unknown;
            }
        }

        /// <summary>
        /// Creates the ExternalOrchestration API URL using environment-based host resolution.
        /// </summary>
        private static Uri CreateOrchestratedUri(string cdsBotId, string host, string conversationId)
        {
            var builder = new UriBuilder();
            builder.Scheme = "https";
            builder.Host = host;
            builder.Query = $"api-version={ApiVersion}";
            builder.Path = $"/powervirtualagents/orchestrated/{cdsBotId}/conversations/{conversationId}";
            return builder.Uri;
        }

        /// <summary>
        /// Creates the ExternalOrchestration API URL using a DirectConnect URL.
        /// </summary>
        private static Uri CreateOrchestratedUri(string directConnectUrl, string conversationId)
        {
            var builder = new UriBuilder(directConnectUrl);
            builder.Query = $"api-version={ApiVersion}";

            // if builder.path ends with /, remove it
#if !NETSTANDARD
            if (builder.Path.EndsWith('/') || builder.Path.EndsWith('\\'))
#else
            if (builder.Path.EndsWith("/") || builder.Path.EndsWith("\\"))
#endif
            {
                builder.Path = builder.Path.Substring(0, builder.Path.Length - 1);
            }

            // if builder.path has /conversations, remove it
            if (builder.Path.Contains("/conversations"))
            {
                builder.Path = builder.Path.Substring(0, builder.Path.IndexOf("/conversations"));
            }

            builder.Path = $"{builder.Path}/conversations/{conversationId}";
            return builder.Uri;
        }

        /// <summary>
        /// Get Environment ID Suffix Length for the given Power Platform Cloud Category.
        /// </summary>
        /// <param name="cloud"></param>
        /// <returns></returns>
        private static int GetIdSuffixLength(PowerPlatformCloud cloud)
        {
            switch (cloud)
            {
                case PowerPlatformCloud.FirstRelease:
                case PowerPlatformCloud.Prod:
                    return 2;
                default:
                    return 1;
            }
        }

        #endregion
    }
}
