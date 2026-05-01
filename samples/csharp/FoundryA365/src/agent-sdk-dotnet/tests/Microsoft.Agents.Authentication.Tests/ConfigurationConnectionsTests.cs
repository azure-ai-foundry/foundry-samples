// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Authentication.Errors;
using Microsoft.Agents.Authentication.Model;
using Microsoft.Agents.Authentication.Msal.Model;
using Microsoft.Agents.Authentication.Msal;
using Microsoft.Agents.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
#if !NETFRAMEWORK
using System.Runtime.Loader;
#endif
using System.Security.Claims;
using Xunit;
using Xunit.Abstractions;
using Moq;

namespace Microsoft.Agents.Auth.Tests
{
    public class ConfigurationConnectionsTests(ITestOutputHelper output)
    {
        private readonly ClaimsIdentity _identity = new([]);
        readonly ITestOutputHelper _outputListener = output;

        [Fact]
        public void GetConnection_ShouldReturnAccessTokenProviderWithConnectionName()
        {

            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                        { "ConnectionsMap:0:ServiceUrl", "*" },
                        { "ConnectionsMap:0:Connection", "ServiceConnection" },
                        { "Connections:ServiceConnection:Type", "MsalAuth" },
                        { "Connections:ServiceConnection:Assembly", "Microsoft.Agents.Authentication.Msal" },
                        { "Connections:ServiceConnection:Settings:ClientId", "ClientId" },
                        { "Connections:ServiceConnection:Settings:ClientSecret", "ClientSecret" },
                        { "Connections:ServiceConnection:Settings:AuthorityEndpoint", "AuthorityEndpoint" },
                    });

            var configurationConnections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());

            var response = configurationConnections.GetConnection("ServiceConnection");

            Assert.NotNull(response);
        }

        [Fact]
        public void GetConnection_ShouldThrowOnNullConnectionName()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                        { "ConnectionsMap:0:ServiceUrl", "*" },
                        { "ConnectionsMap:0:Connection", "ServiceConnection" },
                        { "Connections:ServiceConnection:Type", "MsalAuth" },
                        { "Connections:ServiceConnection:Assembly", "Microsoft.Agents.Authentication.Msal" },
                        { "Connections:ServiceConnection:Settings:ClientId", "ClientId" },
                        { "Connections:ServiceConnection:Settings:ClientSecret", "ClientSecret" },
                        { "Connections:ServiceConnection:Settings:AuthorityEndpoint", "AuthorityEndpoint" },
                    });

            var configurationConnections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());

            Assert.Throws<ArgumentNullException>(() => configurationConnections.GetConnection(null));
        }

        [Fact]
        public void GetDefaultConnection_ShouldReturnAccessTokenProviderFromMap()
        {

            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                        { "ConnectionsMap:0:ServiceUrl", "*" },
                        { "ConnectionsMap:0:Connection", "ServiceConnection" },
                        { "Connections:ServiceConnection:Type", "MsalAuth" },
                        { "Connections:ServiceConnection:Assembly", "Microsoft.Agents.Authentication.Msal" },
                        { "Connections:ServiceConnection:Settings:ClientId", "ClientId" },
                        { "Connections:ServiceConnection:Settings:ClientSecret", "ClientSecret" },
                        { "Connections:ServiceConnection:Settings:AuthorityEndpoint", "AuthorityEndpoint" },
                    });

            var configurationConnections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());

            var response = configurationConnections.GetDefaultConnection();

            Assert.NotNull(response);
        }

        [Fact]
        public void GetDefaultConnection_ShouldReturnAccessTokenProviderFromConnections()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                        { "ConnectionsMap:0:ServiceUrl", "serviceUrl" },
                        { "ConnectionsMap:0:Connection", "ServiceConnection" },
                        { "Connections:ServiceConnection:Type", "MsalAuth" },
                        { "Connections:ServiceConnection:Assembly", "Microsoft.Agents.Authentication.Msal" },
                        { "Connections:ServiceConnection:Settings:ClientId", "ClientId" },
                        { "Connections:ServiceConnection:Settings:ClientSecret", "ClientSecret" },
                        { "Connections:ServiceConnection:Settings:AuthorityEndpoint", "AuthorityEndpoint" },
                    });

            var configurationConnections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());

            var response = configurationConnections.GetDefaultConnection();

            Assert.NotNull(response);
        }

        [Fact]
        public void GetTokenProvider_ShouldReturnAccessTokenProviderOnMatchingServiceUrl()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                        { "ConnectionsMap:0:ServiceUrl", "serviceUrl" },
                        { "ConnectionsMap:0:Connection", "ServiceConnection" },
                        { "Connections:ServiceConnection:Type", "MsalAuth" },
                        { "Connections:ServiceConnection:Assembly", "Microsoft.Agents.Authentication.Msal" },
                        { "Connections:ServiceConnection:Settings:ClientId", "ClientId" },
                        { "Connections:ServiceConnection:Settings:ClientSecret", "ClientSecret" },
                        { "Connections:ServiceConnection:Settings:AuthorityEndpoint", "AuthorityEndpoint" },
                    });

            var configurationConnections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());

            var response = configurationConnections.GetTokenProvider(_identity, "serviceUrl");

            Assert.NotNull(response);
        }

        [Fact]
        public void GetTokenProvider_ShouldReturnAccessTokenProviderOnEmptyServiceUrl()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                        { "Connections:ServiceConnection:Type", "MsalAuth" },
                        { "Connections:ServiceConnection:Assembly", "Microsoft.Agents.Authentication.Msal" },
                        { "Connections:ServiceConnection:Settings:ClientId", "ClientId" },
                        { "Connections:ServiceConnection:Settings:ClientSecret", "ClientSecret" },
                        { "Connections:ServiceConnection:Settings:AuthorityEndpoint", "AuthorityEndpoint" },
                    });

            var configurationConnections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());

            var response = configurationConnections.GetTokenProvider(_identity, "serviceUrl");

            Assert.NotNull(response);
        }

        [Fact]
        public void GetTokenProvider_ShouldReturnAccessTokenProviderOnGenericServiceUrl()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                        { "ConnectionsMap:0:ServiceUrl", "*" },
                        { "ConnectionsMap:0:Connection", "ServiceConnection" },
                        { "Connections:ServiceConnection:Type", "MsalAuth" },
                        { "Connections:ServiceConnection:Assembly", "Microsoft.Agents.Authentication.Msal" },
                        { "Connections:ServiceConnection:Settings:ClientId", "ClientId" },
                        { "Connections:ServiceConnection:Settings:ClientSecret", "ClientSecret" },
                        { "Connections:ServiceConnection:Settings:AuthorityEndpoint", "AuthorityEndpoint" },
                    });

            var configurationConnections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());

            var response = configurationConnections.GetTokenProvider(_identity, "generic");

            Assert.NotNull(response);
        }

        [Fact]
        public void GetTokenProvider_ShouldReturnAccessTokenProviderFromConnectionInstance()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                        { "ConnectionsMap:0:ServiceUrl", "serviceUrl" },
                        { "ConnectionsMap:0:Connection", "ServiceConnection" },
                        { "Connections:ServiceConnection:Type", "MsalAuth" },
                        { "Connections:ServiceConnection:Assembly", "Microsoft.Agents.Authentication.Msal" },
                        { "Connections:ServiceConnection:Settings:ClientId", "ClientId" },
                        { "Connections:ServiceConnection:Settings:ClientSecret", "ClientSecret" },
                        { "Connections:ServiceConnection:Settings:AuthorityEndpoint", "AuthorityEndpoint" },
                    });

            var configurationConnections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());

            var response = configurationConnections.GetTokenProvider(_identity, "serviceUrl");

            //Call a second time to obtain AccessTokenProvider from the Connection instance
            response = configurationConnections.GetTokenProvider(_identity, "serviceUrl");

            Assert.NotNull(response);
        }

        [Fact]
        public void GetTokenProvider_ShouldReturnNullOnNotMatchingServiceUrl()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                        { "ConnectionsMap:0:ServiceUrl", "serviceUrl" },
                        { "ConnectionsMap:0:Connection", "ServiceConnection" },
                        { "Connections:ServiceConnection:Type", "MsalAuth" },
                        { "Connections:ServiceConnection:Assembly", "Microsoft.Agents.Authentication.Msal" },
                        { "Connections:ServiceConnection:Settings:ClientId", "ClientId" },
                        { "Connections:ServiceConnection:Settings:ClientSecret", "ClientSecret" },
                        { "Connections:ServiceConnection:Settings:AuthorityEndpoint", "AuthorityEndpoint" },
                    });

            var configurationConnections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());

            var response = configurationConnections.GetTokenProvider(_identity, "noUrl");

            Assert.Null(response);
        }

        [Fact]
        public void GetTokenProvider_ShouldReturnIndexOutOfRangeExceptionOnEmptyConnections()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                        { "ConnectionsMap:0:ServiceUrl", "*" },
                        { "ConnectionsMap:0:Connection", "ServiceConnection" },
                        { "ConnectionsMap:0:Audience", "audience" }
                    });

            var configurationConnections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());

            var claims = new List<Claim>
            {
                new(AuthenticationConstants.AudienceClaim, "audience"),
            };
            ClaimsIdentity identity = new(claims);

            try
            {
                var response = configurationConnections.GetTokenProvider(identity, "serviceUrl");
            }
            catch (IndexOutOfRangeException e)
            {
                ExceptionTester.IsException<IndexOutOfRangeException>(e, ErrorHelper.ConnectionNotFoundByName.code, _outputListener);
                return; 
            }
            throw new Exception("Should not reach this point");
        }

        [Fact]
        public void GetProviderConstructor_ShouldReturnConstructorInfoOnValidProviderType()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: null);
#if !NETFRAMEWORK
            var assemblyLoader = new AuthModuleLoader(AssemblyLoadContext.Default, serviceProvider.GetService<ILogger<ConfigurationConnections>>());
#else
            var assemblyLoader = new AuthModuleLoader(AppDomain.CurrentDomain, serviceProvider.GetService<ILogger<ConfigurationConnections>>());
#endif

            var response = assemblyLoader.GetProviderConstructor("name", "Microsoft.Agents.Authentication.Msal", "MsalAuth");

            Assert.NotNull(response);
        }

        [Fact]
        public void GetProviderConstructor_ShouldReturnConstructorInfoOnNullType()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: null);
#if !NETFRAMEWORK
            var assemblyLoader = new AuthModuleLoader(AssemblyLoadContext.Default, serviceProvider.GetService<ILogger<ConfigurationConnections>>());
#else
            var assemblyLoader = new AuthModuleLoader(AppDomain.CurrentDomain, serviceProvider.GetService<ILogger<ConfigurationConnections>>());
#endif

            var response = assemblyLoader.GetProviderConstructor("name", "Microsoft.Agents.Authentication.Msal", null);

            Assert.NotNull(response);
        }

        [Fact]
        public void GetProviderConstructor_ShouldThrowOnNullLoadContext()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: null);
            Assert.Throws<ArgumentNullException>(() => new AuthModuleLoader(null, serviceProvider.GetService<ILogger<ConfigurationConnections>>()));
        }

        [Fact]
        public void GetProviderConstructor_ShouldThrowOnNullAssemblyName()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: null);
#if !NETFRAMEWORK
            var assemblyLoader = new AuthModuleLoader(AssemblyLoadContext.Default, serviceProvider.GetService<ILogger<ConfigurationConnections>>());
#else
            var assemblyLoader = new AuthModuleLoader(AppDomain.CurrentDomain, serviceProvider.GetService<ILogger<ConfigurationConnections>>());
#endif

            try
            {
                assemblyLoader.GetProviderConstructor("name", null, "type-name");
            }
            catch(InvalidOperationException ex)
            {
                ExceptionTester.IsException<InvalidOperationException>(ex, ErrorHelper.AuthProviderTypeNotFound.code, _outputListener);
                return;
            }
            throw new Exception("Should not reach this point");
        }

        [Fact]
        public void GetProviderConstructor_ShouldThrowOnInvalidProviderType()
        {
            var serviceProvider = ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: null);
#if !NETFRAMEWORK
            var assemblyLoader = new AuthModuleLoader(AssemblyLoadContext.Default, serviceProvider.GetService<ILogger<ConfigurationConnections>>());
#else
            var assemblyLoader = new AuthModuleLoader(AppDomain.CurrentDomain, serviceProvider.GetService<ILogger<ConfigurationConnections>>());
#endif
            try
            {
                assemblyLoader.GetProviderConstructor("name", "Microsoft.Agents.Authentication.Msal", "type");
            }
            catch (InvalidOperationException ex)
            {
                ExceptionTester.IsException<InvalidOperationException>(ex, ErrorHelper.AuthProviderTypeNotFound.code, _outputListener);
                return;
            }
            throw new Exception("Should not reach this point");
        }

        [Fact]
        public void ManualConstruction()
        {
            var sp = new Mock<IServiceProvider>();
            var providers = new Dictionary<string, IAccessTokenProvider>
            {
                {
                    "ServiceConnection", new MsalAuth(sp.Object, new ConnectionSettings()
                    {
                        ClientId = "12345",
                        AuthType = AuthTypes.ClientSecret,
                    }
                )}
            };

            List<ConnectionMapItem> mapItems = new List<ConnectionMapItem> { new ConnectionMapItem { ServiceUrl = "*", Connection = "ServiceConnection" } };
            var connection = new ConfigurationConnections(providers, mapItems);
            Assert.True(connection.TryGetConnection("ServiceConnection", out var provider));
            Assert.NotNull(provider);
        }
    }
}
