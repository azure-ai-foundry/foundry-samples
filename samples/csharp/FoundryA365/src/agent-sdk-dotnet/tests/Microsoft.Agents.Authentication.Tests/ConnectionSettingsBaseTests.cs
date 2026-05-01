// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Agents.Auth.Tests
{
    public class ConnectionSettingsBaseTests
    {
        private const string ClientId = "client-id";
        private const string AuthorityEndpoint = "https://botframework/test.com";
        private const string TenantId = "tenant-id";
        private static readonly List<string> _scopes = ["scope1", "scope2"];

        [Fact]
        public void Constructor_ShouldSetPropertiesWithDefaults()
        {

            Dictionary<string, string> configSettings = new()
            {
                { "Connections:Settings:Other", "other" }
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            var settings = new TestConnectionSettings(configuration.GetSection("Connections:Settings"));

            Assert.Null(settings.ClientId);
            Assert.Null(settings.Authority);
            Assert.Null(settings.TenantId);
            Assert.Null(settings.Scopes);
        }

        [Fact]
        public void Constructor_ShouldCorrectlySetProperties()
        {
            Dictionary<string, string> configSettings = new()
            {
                { "Connections:Settings:ClientId", ClientId },
                { "Connections:Settings:AuthorityEndpoint", AuthorityEndpoint },
                { "Connections:Settings:TenantId", TenantId },
                { "Connections:Settings:Scopes:0", _scopes[0] },
                { "Connections:Settings:Scopes:1", _scopes[1] }
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            var settings = new TestConnectionSettings(configuration.GetSection("Connections:Settings"));

            Assert.Equal(ClientId, settings.ClientId);
            Assert.Equal(AuthorityEndpoint, settings.Authority);
            Assert.Equal(TenantId, settings.TenantId);
            Assert.Equal(_scopes[0], settings.Scopes[0]);
            Assert.Equal(_scopes[1], settings.Scopes[1]);
        }

        [Fact]
        public void Constructor_ShouldThrowOnNullConfig()
        {
            Assert.Throws<ArgumentNullException>(() => new TestConnectionSettings(null));
        }

        [Fact]
        public void Constructor_ShouldThrowOnNoConfigSection()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .Build();

           Assert.Throws<ArgumentException>(() => new TestConnectionSettings(configuration.GetSection("EmptySection")));
        }

        private class TestConnectionSettings(IConfigurationSection configurationSection) : ConnectionSettingsBase(configurationSection)
        {
        }
    }
}
