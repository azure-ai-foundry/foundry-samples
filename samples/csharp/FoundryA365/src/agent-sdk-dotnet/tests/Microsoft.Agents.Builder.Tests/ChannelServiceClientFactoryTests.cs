// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Agents.Authentication;
using Moq;
using System.Threading;
using System.Net.Http;
using Microsoft.Agents.Connector;
using Xunit.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Errors;
using System.Text;
using Microsoft.Agents.TestSupport;
using Microsoft.Agents.Builder.Errors;
using System.Security.Claims;

namespace Microsoft.Agents.Builder.Tests
{
    public class ChannelServiceClientFactoryTests(ITestOutputHelper output)
    {
        readonly ITestOutputHelper _outputListener = output;

        [Fact]
        public void ConstructionThrows()
        {
            var serviceProvider = TestSupport.ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                        { "ConnectionsMap", string.Empty },
                    });

            //var serviceProvider = new Mock<IServiceProvider>();
            var connections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());
            var httpFactory = new Mock<IHttpClientFactory>();

            // Null IConfiguration
            Assert.Throws<ArgumentNullException>(() => new RestChannelServiceClientFactory(null, httpFactory.Object, connections));

            // Null IConnections
            Assert.Throws<ArgumentNullException>(() => new RestChannelServiceClientFactory(serviceProvider.GetService<IConfiguration>(), httpFactory.Object, null));

            // Null IHttpClientFactory
            Assert.Throws<ArgumentNullException>(() => new RestChannelServiceClientFactory(serviceProvider.GetService<IConfiguration>(), null, connections));
        }

        [Fact]
        public async Task ConnectionMapNotFoundThrowsAsync()
        {
            var serviceProvider = TestSupport.ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                        { "ConnectionsMap", string.Empty },
                    });

            var connections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());
            var httpFactory = new Mock<IHttpClientFactory>();
            var traceActivity = Activity.CreateTraceActivity("Test");
            traceActivity.Conversation = new ConversationAccount(id: "1234");

            var factory = new RestChannelServiceClientFactory(serviceProvider.GetService<IConfiguration>(), httpFactory.Object, connections);

            try
            {
                IConnectorClient v = await factory.CreateConnectorClientAsync(new System.Security.Claims.ClaimsIdentity(), "http://serviceurl", "audience", CancellationToken.None);
                await v.Conversations.SendToConversationAsync(traceActivity, CancellationToken.None);
            }
            catch (Exception e)
            {
                ExceptionTester.IsException<OperationCanceledException>(e, ErrorHelper.NullIAccessTokenProvider.code, _outputListener);
            }

            try
            {

                IConnectorClient v = await factory.CreateConnectorClientAsync(new System.Security.Claims.ClaimsIdentity(), "http://serviceurl", "audience", CancellationToken.None);
                await v.Conversations.SendToConversationAsync(traceActivity, CancellationToken.None);
            }
            catch (Exception e)
            {
                ExceptionTester.IsException<OperationCanceledException>(e, ErrorHelper.NullIAccessTokenProvider.code, _outputListener);
            }

            try
            {
                IUserTokenClient u = await factory.CreateUserTokenClientAsync(new System.Security.Claims.ClaimsIdentity(), cancellationToken: CancellationToken.None);
                await u.GetUserTokenAsync(userId: "ABC", connectionName: "ConnNAM", channelId: "TEST", magicCode: "Im Magic", CancellationToken.None);
            }
            catch (Exception e)
            {
                ExceptionTester.IsException<OperationCanceledException>(e, ErrorHelper.NullUserTokenProviderIAccessTokenProvider.code, _outputListener);
            }

        }


        [Fact]
        public async Task ConnectionMapNotFoundAnonymousDoesNotThrowAsync()
        {
            var serviceProvider = TestSupport.ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                        { "ConnectionsMap", string.Empty },
                    });

            var connections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());
            var httpFactory = new Mock<IHttpClientFactory>();

            var traceActivity = Activity.CreateTraceActivity("Test");
            traceActivity.Conversation = new ConversationAccount(id: "1234");
            traceActivity.ChannelId = "Emulator";

            var audience = "http://localhost";

            httpFactory.Setup(
                x => x.CreateClient(It.IsAny<string>()))
                .Returns(new Mock<HttpClient>().Object);

            var factory = new RestChannelServiceClientFactory(serviceProvider.GetService<IConfiguration>(), httpFactory.Object, connections);

            var connector = await factory.CreateConnectorClientAsync(new System.Security.Claims.ClaimsIdentity(), "http://serviceurl", audience, CancellationToken.None, useAnonymous: true);
            Assert.IsType<RestConnectorClient>(connector);

            var tokeClient = await factory.CreateUserTokenClientAsync(new System.Security.Claims.ClaimsIdentity(), true, CancellationToken.None);
            Assert.IsType<RestUserTokenClient>(tokeClient);
        }

        [Fact]
        public async Task ConnectionNotFoundThrowsAsync()
        {

            var serviceProvider = TestSupport.ServiceProviderBootStrap.CreateServiceProvider(_outputListener, configurationDictionary: new Dictionary<string, string>
                    {
                         { "ConnectionsMap:0:ServiceUrl", "*" },
                        { "ConnectionsMap:0:Connection", "ServiceConnection" },
                    });

            var connections = new ConfigurationConnections(serviceProvider, serviceProvider.GetService<IConfiguration>());
            var httpFactory = new Mock<IHttpClientFactory>();
            var traceActivity = Activity.CreateTraceActivity("Test");
            traceActivity.Conversation = new ConversationAccount(id: "1234");

            var factory = new RestChannelServiceClientFactory(serviceProvider.GetService<IConfiguration>(), httpFactory.Object, connections);

            try
            {
                IConnectorClient v = await factory.CreateConnectorClientAsync(new System.Security.Claims.ClaimsIdentity(), "http://serviceurl", "audience", CancellationToken.None);
                await v.Conversations.SendToConversationAsync(traceActivity, CancellationToken.None);
            }
            catch (Exception e)
            {
                ExceptionTester.IsException<OperationCanceledException>(e, ErrorHelper.NullIAccessTokenProvider.code, _outputListener);
            }


            try
            {
                IUserTokenClient u = await factory.CreateUserTokenClientAsync(new System.Security.Claims.ClaimsIdentity(), false, CancellationToken.None);
                await u.GetUserTokenAsync(userId: "ABC", connectionName: "ConnNAM", channelId: "TEST", magicCode: "Im Magic", CancellationToken.None);
            }
            catch (Exception e)
            {
                ExceptionTester.IsException<OperationCanceledException>(e, ErrorHelper.NullUserTokenProviderIAccessTokenProvider.code, _outputListener);
            }
            //await Assert.ThrowsAsync<InvalidOperationException>(async () => await factory.CreateConnectorClientAsync(new System.Security.Claims.ClaimsIdentity(), "http://serviceurl", "audience", CancellationToken.None));
            //await Assert.ThrowsAsync<InvalidOperationException>(async () => await factory.CreateUserTokenClientAsync(new System.Security.Claims.ClaimsIdentity(), CancellationToken.None));
        }

        [Fact]
        public async Task ConnectionFoundAsync()
        {
            var config = new ConfigurationRoot(new List<IConfigurationProvider>
            {
                new MemoryConfigurationProvider(new MemoryConfigurationSource
                {
                    InitialData = new Dictionary<string, string>
                    {
                        { "ConnectionsMap:0:ServiceUrl", "*" },
                        { "ConnectionsMap:0:Connection", "ServiceConnection" },
                        { "Connections:ServiceConnection:Type", "MsalAuth" },
                        { "Connections:ServiceConnection:Assembly", "Microsoft.Agents.Authentication.Msal" },
                        { "Connections:ServiceConnection:Settings:ClientId", "ClientId" },
                        { "Connections:ServiceConnection:Settings:ClientSecret", "ClientSecret" },
                        { "Connections:ServiceConnection:Settings:AuthorityEndpoint", "AuthorityEndpoint" },
                    }
                })
            });

            var serviceProvider = new Mock<IServiceProvider>();
            var connections = new ConfigurationConnections(serviceProvider.Object, config);
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory.Setup(
                x => x.CreateClient(It.IsAny<string>()))
                .Returns(new Mock<HttpClient>().Object);

            var factory = new RestChannelServiceClientFactory(config, httpFactory.Object, connections);

            var connector = await factory.CreateConnectorClientAsync(new System.Security.Claims.ClaimsIdentity(), "http://serviceurl", "audience", CancellationToken.None, useAnonymous: true);
            Assert.IsType<RestConnectorClient>(connector);

            var tokeClient = await factory.CreateUserTokenClientAsync(new System.Security.Claims.ClaimsIdentity(), true,  CancellationToken.None);
            Assert.IsType<RestUserTokenClient>(tokeClient);
            Assert.Equal(new Uri(AuthenticationConstants.BotFrameworkOAuthUrl).ToString(), ((RestUserTokenClient)tokeClient).BaseUri.ToString());
        }

        [Fact]
        public void NullBotServiceAudienceThrows()
        {
            var config = new ConfigurationBuilder().Build();
            var connections = new Mock<IConnections>();
            var httpFactory = new Mock<IHttpClientFactory>();

            Assert.Throws<ArgumentNullException>(() => new RestChannelServiceClientFactory(
                config, httpFactory.Object, connections.Object, botServiceAudience: null));
        }

        [Fact]
        public async Task BotServiceAudienceFromConstructorIsUsedAsync()
        {
            var config = new ConfigurationBuilder().Build();
            string capturedAudience = null;

            var tokenProvider = new Mock<IAccessTokenProvider>();
            tokenProvider
                .Setup(x => x.GetAccessTokenAsync(It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<bool>()))
                .Callback<string, IList<string>, bool>((aud, scopes, force) => capturedAudience = aud)
                .ReturnsAsync("fake-token");

            var connections = new Mock<IConnections>();
            connections
                .Setup(x => x.GetTokenProvider(It.IsAny<ClaimsIdentity>(), It.IsAny<string>()))
                .Returns(tokenProvider.Object);

            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory
                .Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient());

            var factory = new RestChannelServiceClientFactory(
                config, httpFactory.Object, connections.Object,
                botServiceAudience: AuthenticationConstants.GovBotFrameworkAudience);

            var connector = await factory.CreateConnectorClientAsync(
                new ClaimsIdentity(), "http://serviceurl", null, CancellationToken.None);

            await ((IRestTransport)connector).GetHttpClientAsync();

            Assert.Equal(AuthenticationConstants.GovBotFrameworkAudience, capturedAudience);
        }

        [Fact]
        public async Task BotServiceAudienceFromConfigIsUsedAsync()
        {
            var config = new ConfigurationRoot(new List<IConfigurationProvider>
            {
                new MemoryConfigurationProvider(new MemoryConfigurationSource
                {
                    InitialData = new Dictionary<string, string>
                    {
                        { "RestChannelServiceClientFactory:BotServiceAudience", AuthenticationConstants.GovBotFrameworkAudience }
                    }
                })
            });
            string capturedAudience = null;

            var tokenProvider = new Mock<IAccessTokenProvider>();
            tokenProvider
                .Setup(x => x.GetAccessTokenAsync(It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<bool>()))
                .Callback<string, IList<string>, bool>((aud, scopes, force) => capturedAudience = aud)
                .ReturnsAsync("fake-token");

            var connections = new Mock<IConnections>();
            connections
                .Setup(x => x.GetTokenProvider(It.IsAny<ClaimsIdentity>(), It.IsAny<string>()))
                .Returns(tokenProvider.Object);

            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory
                .Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient());

            // Default botServiceAudience is BotFrameworkAudience, but config key overrides it
            var factory = new RestChannelServiceClientFactory(config, httpFactory.Object, connections.Object);

            var connector = await factory.CreateConnectorClientAsync(
                new ClaimsIdentity(), "http://serviceurl", null, CancellationToken.None);

            await ((IRestTransport)connector).GetHttpClientAsync();

            Assert.Equal(AuthenticationConstants.GovBotFrameworkAudience, capturedAudience);
        }

        [Fact]
        public async Task AgentIdentityUsesScopesFromClaimsAsync()
        {
            // When the incoming identity is an agent-to-agent call (IsAgent == true),
            // scopes are derived from the caller's appId in the claims: ["{callerAppId}/.default"]
            var config = new ConfigurationBuilder().Build();
            IList<string> capturedScopes = null;

            var callerAppId = "caller-app-id";
            var myAppId = "my-app-id";
            var agentIdentity = AgentClaims.CreateIdentity(audience: myAppId, appId: callerAppId);

            var tokenProvider = new Mock<IAccessTokenProvider>();
            tokenProvider
                .Setup(x => x.GetAccessTokenAsync(It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<bool>()))
                .Callback<string, IList<string>, bool>((aud, scopes, force) => capturedScopes = scopes)
                .ReturnsAsync("fake-token");

            var connections = new Mock<IConnections>();
            connections
                .Setup(x => x.GetTokenProvider(It.IsAny<ClaimsIdentity>(), It.IsAny<string>()))
                .Returns(tokenProvider.Object);

            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory
                .Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient());

            var factory = new RestChannelServiceClientFactory(config, httpFactory.Object, connections.Object);

            var connector = await factory.CreateConnectorClientAsync(
                agentIdentity, "http://serviceurl", null, CancellationToken.None);

            await ((IRestTransport)connector).GetHttpClientAsync();

            Assert.NotNull(capturedScopes);
            Assert.Single(capturedScopes);
            Assert.Equal($"{callerAppId}/.default", capturedScopes[0]);
        }

        [Theory]
        [InlineData(AuthenticationConstants.BotFrameworkTokenIssuer, AuthenticationConstants.BotFrameworkDefaultScope)]
        [InlineData(AuthenticationConstants.GovBotFrameworkTokenIssuer, AuthenticationConstants.GovBotFrameworkDefaultScope)]
        public async Task AbsIdentityUsesConnectionSettingsScopesAsync(string absAudience, string configuredScope)
        {
            // When the incoming identity is from Azure Bot Service (any cloud, IsAgent == false),
            // the factory passes null scopes to GetAccessTokenAsync. The IAccessTokenProvider then
            // uses its ConnectionSettings.Scopes — i.e., the Scopes value from appsettings.
            var config = new ConfigurationBuilder().Build();
            IList<string> effectiveScopes = null;

            var absIdentity = AgentClaims.CreateIdentity(audience: absAudience);

            // Simulate the connection's settings as loaded from appsettings (e.g., Connections:...:Settings:Scopes)
            var connectionSettings = new ImmutableConnectionSettings(
                new TestConnectionSettings { Scopes = [configuredScope] });

            var tokenProvider = new Mock<IAccessTokenProvider>();
            tokenProvider.Setup(x => x.ConnectionSettings).Returns(connectionSettings);
            tokenProvider
                .Setup(x => x.GetAccessTokenAsync(It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<bool>()))
                .Returns<string, IList<string>, bool>((aud, scopes, force) =>
                {
                    // Simulate real IAccessTokenProvider: when the factory passes null scopes for ABS,
                    // fall back to the configured ConnectionSettings.Scopes from appsettings.
                    effectiveScopes = scopes ?? tokenProvider.Object.ConnectionSettings.Scopes;
                    return Task.FromResult("fake-token");
                });

            var connections = new Mock<IConnections>();
            connections
                .Setup(x => x.GetTokenProvider(It.IsAny<ClaimsIdentity>(), It.IsAny<string>()))
                .Returns(tokenProvider.Object);

            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory
                .Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient());

            var factory = new RestChannelServiceClientFactory(config, httpFactory.Object, connections.Object);

            var connector = await factory.CreateConnectorClientAsync(
                absIdentity, "http://serviceurl", null, CancellationToken.None);

            await ((IRestTransport)connector).GetHttpClientAsync();

            Assert.NotNull(effectiveScopes);
            Assert.Single(effectiveScopes);
            Assert.Equal(configuredScope, effectiveScopes[0]);
        }

        private sealed class TestConnectionSettings : ConnectionSettingsBase { }

        [Fact]
        public async Task ConnectionFoundWithConfigTokenEndpointAsync()
        {
            var config = new ConfigurationRoot(new List<IConfigurationProvider>
            {
                new MemoryConfigurationProvider(new MemoryConfigurationSource
                {
                    InitialData = new Dictionary<string, string>
                    {
                        { "ConnectionsMap:0:ServiceUrl", "*" },
                        { "ConnectionsMap:0:Connection", "ServiceConnection" },
                        { "Connections:ServiceConnection:Type", "MsalAuth" },
                        { "Connections:ServiceConnection:Assembly", "Microsoft.Agents.Authentication.Msal" },
                        { "Connections:ServiceConnection:Settings:ClientId", "ClientId" },
                        { "Connections:ServiceConnection:Settings:ClientSecret", "ClientSecret" },
                        { "Connections:ServiceConnection:Settings:AuthorityEndpoint", "AuthorityEndpoint" },
                        { "RestChannelServiceClientFactory:TokenServiceEndpoint", "https://test.token.endpoint" }
                    }
                })
            });

            var serviceProvider = new Mock<IServiceProvider>();
            var connections = new ConfigurationConnections(serviceProvider.Object, config);
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory.Setup(
                x => x.CreateClient(It.IsAny<string>()))
                .Returns(new Mock<HttpClient>().Object);

            var factory = new RestChannelServiceClientFactory(config, httpFactory.Object, connections);

            var connector = await factory.CreateConnectorClientAsync(new System.Security.Claims.ClaimsIdentity(), "http://serviceurl", "audience", CancellationToken.None, useAnonymous: true);
            Assert.IsType<RestConnectorClient>(connector);

            var tokeClient = await factory.CreateUserTokenClientAsync(new System.Security.Claims.ClaimsIdentity(), true, CancellationToken.None);
            Assert.IsType<RestUserTokenClient>(tokeClient);
            Assert.Equal("https://test.token.endpoint/", ((RestUserTokenClient)tokeClient).BaseUri.ToString());
        }
    }
}
