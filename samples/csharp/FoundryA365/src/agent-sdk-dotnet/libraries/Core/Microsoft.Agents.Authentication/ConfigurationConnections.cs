// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Errors;
using Microsoft.Agents.Authentication.Model;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
#if !NETSTANDARD
using System.Runtime.Loader;
#endif
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.Authentication
{
    /// <summary>
    /// A IConfiguration based IConnections.
    /// </summary>
    /// <code>
    /// "Connections": {
    ///   "ServiceConnection": {
    ///     "Settings": {
    ///     }
    /// },
    /// "ConnectionsMap": [
    ///  { 
    ///    "ServiceUrl": "*",
    ///    "Connection": "ServiceConnection"
    /// }
    /// </code>
    /// <remarks>
    /// The type indicated must have the constructor:<c>(IServiceProvider systemServiceProvider, IConfigurationSection configurationSection)</c>.
    /// The 'configurationSection' argument is the 'Settings' portion of the connection.
    /// 
    /// If 'ConnectionsMap' is not specified, the first Connection is used as the default.
    /// </remarks>
    public partial class ConfigurationConnections : IConnections
    {
        private readonly Dictionary<string, ConnectionDefinition> _connections;
        private readonly IServiceProvider _serviceProvider;
        private readonly List<ConnectionMapItem> _map;
        private readonly ILogger<ConfigurationConnections> _logger;

        public ConfigurationConnections(IServiceProvider systemServiceProvider, IConfiguration configuration, string connectionsKey = "Connections", string mapKey = "ConnectionsMap")
        {
            AssertionHelpers.ThrowIfNullOrEmpty(connectionsKey, nameof(connectionsKey));
            AssertionHelpers.ThrowIfNullOrEmpty(mapKey, nameof(mapKey));

            _serviceProvider = systemServiceProvider ?? throw new ArgumentNullException(nameof(systemServiceProvider));
            _logger = (ILogger<ConfigurationConnections>)systemServiceProvider.GetService(typeof(ILogger<ConfigurationConnections>));

            _connections = configuration
                .GetSection(connectionsKey)
                .Get<Dictionary<string, ConnectionDefinition>>() ?? [];
            if (_connections.Count == 0)
            {
                _logger.LogWarning("No connections found in configuration.");
            }

            _map = configuration
                .GetSection(mapKey)
                .Get<List<ConnectionMapItem>>() ?? [];

            if (_map.Count == 0)
            {
                _logger.LogWarning("No connections map found in configuration.");
                if (_connections.Count == 1)
                {
                    _map.Add(new ConnectionMapItem() { ServiceUrl = "*", Connection = _connections.First().Key });
                }
            }

#if !NETSTANDARD
            var assemblyLoader = new AuthModuleLoader(AssemblyLoadContext.Default, _logger);
#else
            var assemblyLoader = new AuthModuleLoader(AppDomain.CurrentDomain, _logger);
#endif

            foreach (var connection in _connections)
            {
                connection.Value.Constructor = assemblyLoader.GetProviderConstructor(connection.Key, connection.Value.Assembly, connection.Value.Type);
            }
        }

        public ConfigurationConnections(IDictionary<string, IAccessTokenProvider> accessTokenProviders, IList<ConnectionMapItem> connectionMapItems, ILogger<ConfigurationConnections> logger = null)
        {
            _logger = logger ?? NullLogger<ConfigurationConnections>.Instance;

            _connections = [];
            if (accessTokenProviders != null)
            {
                foreach (var provider in accessTokenProviders)
                {
                    _connections[provider.Key] = new ConnectionDefinition() { Instance = provider.Value };
                }
            }

            if (_connections.Count == 0)
            {
                _logger.LogWarning("No connections provided");
            }

            _map = connectionMapItems == null ? [] : [.. connectionMapItems];

            if (_map.Count == 0)
            {
                _logger.LogWarning("No connections map provided");
                if (_connections.Count == 1)
                {
                    _map.Add(new ConnectionMapItem() { ServiceUrl = "*", Connection = _connections.First().Key });
                }
            }
        }

        /// <inheritdoc/>
        public IAccessTokenProvider GetConnection(string name)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(name, nameof(name));

            return GetConnectionInstance(name);
        }

        /// <summary>
        /// Tries to get a connection by name. 
        /// </summary>
        /// <param name="name">Name of the connection.</param>
        /// <param name="connection">Access token provider for the named connection.</param>
        /// <returns>Returns <c>true</c> if successful; otherwise <c>false</c>.</returns>
        public bool TryGetConnection(string name, out IAccessTokenProvider connection)
        {
            if (string.IsNullOrEmpty(name) || !_connections.TryGetValue(name, out ConnectionDefinition definition))
            {
                connection = null;
                return false;
            }

            connection = GetConnectionInstance(definition, doThrow: false);
            return connection != null;
        }

        /// <inheritdoc/>
        public IAccessTokenProvider GetDefaultConnection()
        {
            // if no connections, abort and return null.
            if (_connections.Count == 0)
            {
                _logger.LogError(ErrorHelper.MissingAuthenticationConfiguration.description);
                throw Core.Errors.ExceptionHelper.GenerateException<IndexOutOfRangeException>(ErrorHelper.MissingAuthenticationConfiguration, null);
            }

            // Return the wildcard map item instance.
            foreach (var mapItem in _map)
            {
                if (mapItem.ServiceUrl == "*" && string.IsNullOrEmpty(mapItem.Audience))
                {
                    return GetConnectionInstance(mapItem.Connection);
                }
            }

            // Otherwise, return the first connection.
            return GetConnectionInstance(_connections.FirstOrDefault().Value);
        }

        /// <inheritdoc/>
        public IAccessTokenProvider GetTokenProvider(ClaimsIdentity claimsIdentity, IActivity activity)
        {
            var connection = GetTokenProvider(claimsIdentity, activity.ServiceUrl);

            // This is for if the Agentic BlueprintId is not the same as the AppId
            if (connection != null 
                && (RoleTypes.AgenticIdentity.Equals(activity?.Recipient?.Role, StringComparison.OrdinalIgnoreCase)
                || RoleTypes.AgenticUser.Equals(activity?.Recipient?.Role, StringComparison.OrdinalIgnoreCase)))
            {
                if (!string.IsNullOrEmpty(connection.ConnectionSettings?.AlternateBlueprintConnectionName))
                {
                    connection = GetConnection(connection.ConnectionSettings.AlternateBlueprintConnectionName);
                }
            }

            return connection;
        }

        /// <summary>
        /// Finds a connection based on a map.
        /// </summary>
        /// <code>
        /// "ConnectionsMap":
        /// [
        ///    {
        ///       "ServiceUrl": "http://*..botframework.com/*.",
        ///       "Audience": optional,
        ///       "Connection": "ServiceConnection"
        ///    }
        /// ]
        /// </code>
        /// <remarks>
        /// ServiceUrl is:  A regex to match with, or "*" for any serviceUrl value.
        /// Connection is: A name in the 'Connections'.
        /// </remarks>        
        /// <param name="claimsIdentity"></param>
        /// <param name="serviceUrl"></param>
        /// <returns></returns>
        public IAccessTokenProvider GetTokenProvider(ClaimsIdentity claimsIdentity, string serviceUrl)
        {
            AssertionHelpers.ThrowIfNull(claimsIdentity, nameof(claimsIdentity));
            AssertionHelpers.ThrowIfNullOrEmpty(serviceUrl, nameof(serviceUrl));

            if (_map.Count == 0)
            {
                return GetDefaultConnection();
            }

            var audience = claimsIdentity.GetIncomingAudience();

            // Find a match, in document order.
            foreach (var mapItem in _map)
            {
                var audienceMatch = true;
                if (!string.IsNullOrEmpty(mapItem.Audience))
                {
                    audienceMatch = mapItem.Audience.Equals(audience, StringComparison.OrdinalIgnoreCase);
                }

                if (audienceMatch)
                {
                    if (mapItem.ServiceUrl == "*" || string.IsNullOrEmpty(mapItem.ServiceUrl))
                    {
                        return GetConnectionInstance(mapItem.Connection);
                    }

                    var match = Regex.Match(serviceUrl, mapItem.ServiceUrl, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        return GetConnectionInstance(mapItem.Connection);
                    }
                }
            }

            return null;
        }

        private IAccessTokenProvider GetConnectionInstance(string name)
        {
            if (!_connections.TryGetValue(name, out ConnectionDefinition value))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<IndexOutOfRangeException>(ErrorHelper.ConnectionNotFoundByName, null, name);
            }
            return GetConnectionInstance(value);
        }

        private IAccessTokenProvider GetConnectionInstance(ConnectionDefinition connection, bool doThrow = true)
        {
            if (connection.Instance != null)
            {
                // Return existing instance.
                return connection.Instance;
            }

            try
            {
                // Construct the provider
                connection.Instance = connection.Constructor.Invoke([_serviceProvider, connection.Settings]) as IAccessTokenProvider;
                return connection.Instance;
            }
            catch (Exception ex)
            {
                if (doThrow)
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.FailedToCreateAuthModuleProvider, ex, connection.Type);
                }
            }
            return null;
        }
    }
}
