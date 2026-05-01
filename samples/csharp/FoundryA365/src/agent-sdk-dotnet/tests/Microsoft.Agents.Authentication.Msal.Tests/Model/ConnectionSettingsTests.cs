// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Msal.Model;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.Agents.Authentication.Msal.Tests.Model
{
    public class ConnectionSettingsTests
    {
        private readonly Dictionary<string, string> _configSettings;
        private const string SettingsSection = "Connections:Settings";

        public ConnectionSettingsTests()
        {
            _configSettings = new Dictionary<string, string> {
                { "Connections:Settings:ClientId", "test-client-id" },
                { "Connections:Settings:ClientSecret", "test-client-secret" },
                { "Connections:Settings:CertThumbprint", "test-thumbprint" },
                { "Connections:Settings:CertificateSubjectName", "test-subject-name" },
                { "Connections:Settings:AuthorityEndpoint", "https://botframework/test.com" },
                { "Connections:Settings:TenantId", "test-tenant-id" },
            };            
        }

        [Fact]
        public void ConnectionSettings_ShouldDefaultToClientSecretType()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(_configSettings)
                .Build();

            var settings = new ConnectionSettings(configuration.GetSection(SettingsSection));

            Assert.Equal(AuthTypes.ClientSecret, settings.AuthType);
        }

        [Fact]
        public void ValidateConfiguration_ShouldThrowOnNullClientIdForCertificateType()
        {
            _configSettings.Add("Connections:Settings:AuthType", "Certificate");
            _configSettings.Remove("Connections:Settings:ClientId");

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(_configSettings)
                .Build();

            Assert.Throws<ArgumentNullException>(() => new ConnectionSettings(configuration.GetSection(SettingsSection)));
        }

        [Fact]
        public void ValidateConfiguration_ShouldThrowOnNullCertificateThumbPrintForCertificateType()
        {
            _configSettings.Add("Connections:Settings:AuthType", "Certificate");
            _configSettings.Remove("Connections:Settings:CertThumbprint");

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(_configSettings)
                .Build();

            Assert.Throws<ArgumentNullException>(() => new ConnectionSettings(configuration.GetSection(SettingsSection)));
        }

        [Fact]
        public void ValidateConfiguration_ShouldThrowOnNullAuthorityForCertificateType()
        {
            _configSettings.Add("Connections:Settings:AuthType", "Certificate");
            _configSettings.Remove("Connections:Settings:AuthorityEndpoint");
            _configSettings.Remove("Connections:Settings:TenantId");

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(_configSettings)
                .Build();

            Assert.Throws<ArgumentNullException>(() => new ConnectionSettings(configuration.GetSection(SettingsSection)));
        }

        [Fact]
        public void ValidateConfiguration_ShouldThrowOnNullClientIdForCertificateSubjectNameType()
        {
            _configSettings.Add("Connections:Settings:AuthType", "CertificateSubjectName");
            _configSettings.Remove("Connections:Settings:ClientId");

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(_configSettings)
                .Build();

            Assert.Throws<ArgumentNullException>(() => new ConnectionSettings(configuration.GetSection(SettingsSection)));
        }

        [Fact]
        public void ValidateConfiguration_ShouldThrowOnNullCertificateSubjectNameForCertificateSubjectNameType()
        {
            _configSettings.Add("Connections:Settings:AuthType", "CertificateSubjectName");
            _configSettings.Remove("Connections:Settings:CertificateSubjectName");

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(_configSettings)
                .Build();

            Assert.Throws<ArgumentNullException>(() => new ConnectionSettings(configuration.GetSection(SettingsSection)));
        }

        [Fact]
        public void ValidateConfiguration_ShouldThrowOnNullAuthorityForCertificateSubjectNameType()
        {
            _configSettings.Add("Connections:Settings:AuthType", "CertificateSubjectName");
            _configSettings.Remove("Connections:Settings:AuthorityEndpoint");
            _configSettings.Remove("Connections:Settings:TenantId");

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(_configSettings)
                .Build();

            Assert.Throws<ArgumentNullException>(() => new ConnectionSettings(configuration.GetSection(SettingsSection)));
        }

        [Fact]
        public void ValidateConfiguration_ShouldThrowOnNullClientIdForClientSecretType()
        {
            _configSettings.Add("Connections:Settings:AuthType", "ClientSecret");
            _configSettings.Remove("Connections:Settings:ClientId");

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(_configSettings)
                .Build();

            Assert.Throws<ArgumentNullException>(() => new ConnectionSettings(configuration.GetSection(SettingsSection)));
        }

        [Fact]
        public void ValidateConfiguration_ShouldThrowOnNullClientSecretForClientSecretType()
        {
            _configSettings.Add("Connections:Settings:AuthType", "ClientSecret");
            _configSettings.Remove("Connections:Settings:ClientSecret");

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(_configSettings)
                .Build();

            Assert.Throws<ArgumentNullException>(() => new ConnectionSettings(configuration.GetSection(SettingsSection)));
        }

        [Fact]
        public void ValidateConfiguration_ShouldThrowOnNullAuthorityForClientSecretType()
        {
            _configSettings.Add("Connections:Settings:AuthType", "ClientSecret");
            _configSettings.Remove("Connections:Settings:AuthorityEndpoint");
            _configSettings.Remove("Connections:Settings:TenantId");

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(_configSettings)
                .Build();

            Assert.Throws<ArgumentNullException>(() => new ConnectionSettings(configuration.GetSection(SettingsSection)));
        }

        [Fact]
        public void ValidateConfiguration_ShouldThrowOnNullClientIdForUserManagedIdentityType()
        {
            _configSettings.Add("Connections:Settings:AuthType", "UserManagedIdentity");
            _configSettings.Remove("Connections:Settings:ClientId");

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(_configSettings)
                .Build();

            Assert.Throws<ArgumentNullException>(() => new ConnectionSettings(configuration.GetSection(SettingsSection)));
        }

        [Fact]
        public void ValidateConfiguration_FederatedCredentials()
        {
            // Start with good
            var configSettings = new Dictionary<string, string> {
                { "Connections:Settings:AuthType", "FederatedCredentials" },
                { "Connections:Settings:ClientId", "test-client-id" },
                { "Connections:Settings:AuthorityEndpoint", "https://botframework/test.com" },
                { "Connections:Settings:TenantId", "test-tenant-id" },
                { "Connections:Settings:FederatedClientId", "test-federated-client-id" }
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            var settings = new ConnectionSettings(configuration.GetSection(SettingsSection));

            Assert.Equal(AuthTypes.FederatedCredentials, settings.AuthType);
            Assert.Equal("test-client-id", settings.ClientId);
            Assert.Equal("test-tenant-id", settings.TenantId);
            Assert.Equal("https://botframework/test.com", settings.Authority);
            Assert.Equal("test-federated-client-id", settings.FederatedClientId);
        }

        [Fact]
        public void ValidateConfiguration_ShouldThrowOnNullFederatedClientId()
        {
            // Start with good
            var configSettings = new Dictionary<string, string> {
                { "Connections:Settings:AuthType", "FederatedCredentials" },
                { "Connections:Settings:ClientId", "test-client-id" },
                { "Connections:Settings:AuthorityEndpoint", "https://botframework/test.com" },
                { "Connections:Settings:TenantId", "test-tenant-id" },
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            Assert.Throws<ArgumentNullException>(() => new ConnectionSettings(configuration.GetSection(SettingsSection)));
        }

        [Fact]
        public void ValidateConfiguration_WorkloadIdentity()
        {
            // Start with good
            var configSettings = new Dictionary<string, string> {
                { "Connections:Settings:AuthType", "WorkloadIdentity" },
                { "Connections:Settings:ClientId", "test-client-id" },
                { "Connections:Settings:AuthorityEndpoint", "https://botframework/test.com" },
                { "Connections:Settings:TenantId", "test-tenant-id" },
                { "Connections:Settings:FederatedTokenFile", "test-token-file" }
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            var settings = new ConnectionSettings(configuration.GetSection(SettingsSection));

            Assert.Equal(AuthTypes.WorkloadIdentity, settings.AuthType);
            Assert.Equal("test-client-id", settings.ClientId);
            Assert.Equal("test-tenant-id", settings.TenantId);
            Assert.Equal("https://botframework/test.com", settings.Authority);
            Assert.Equal("test-token-file", settings.FederatedTokenFile);
            Assert.Null(settings.AssertionRequestOptions);
        }

        [Fact]
        public void ValidateConfiguration_ShouldThrowOnNullFederatedTokenFile()
        {
            // Start with good
            var configSettings = new Dictionary<string, string> {
                { "Connections:Settings:AuthType", "WorkloadIdentity" },
                { "Connections:Settings:ClientId", "test-client-id" },
                { "Connections:Settings:AuthorityEndpoint", "https://botframework/test.com" },
                { "Connections:Settings:TenantId", "test-tenant-id" },
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            Assert.Throws<ArgumentNullException>(() => new ConnectionSettings(configuration.GetSection(SettingsSection)));
        }

        [Fact]
        public void ValidateConfiguration_AssertionRequestOptions()
        {
            // Start with good
            var configSettings = new Dictionary<string, string> {
                { "Connections:Settings:AuthType", "WorkloadIdentity" },
                { "Connections:Settings:ClientId", "test-client-id" },
                { "Connections:Settings:AuthorityEndpoint", "https://botframework/test.com" },
                { "Connections:Settings:TenantId", "test-tenant-id" },
                { "Connections:Settings:FederatedTokenFile", "test-token-file" },
                { "Connections:Settings:AssertionRequestOptions:ClientId", "option-client-id" },
                { "Connections:Settings:AssertionRequestOptions:TokenEndpoint", "option-token-endpoint" },
                { "Connections:Settings:AssertionRequestOptions:Claims", "option-claims" },
                { "Connections:Settings:AssertionRequestOptions:ClientCapabilities:0", "option-cap1" },
                { "Connections:Settings:AssertionRequestOptions:ClientCapabilities:1", "option-cap2" },
            };
            
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            var settings = new ConnectionSettings(configuration.GetSection(SettingsSection));

            Assert.Equal(AuthTypes.WorkloadIdentity, settings.AuthType);
            Assert.Equal("test-client-id", settings.ClientId);
            Assert.Equal("test-tenant-id", settings.TenantId);
            Assert.Equal("https://botframework/test.com", settings.Authority);
            Assert.Equal("test-token-file", settings.FederatedTokenFile);
            Assert.NotNull(settings.AssertionRequestOptions);
            Assert.Equal("option-client-id", settings.AssertionRequestOptions.ClientID);
            Assert.Equal("option-token-endpoint", settings.AssertionRequestOptions.TokenEndpoint);
            Assert.Equal("option-claims", settings.AssertionRequestOptions.Claims);
            Assert.Equal(2, settings.AssertionRequestOptions.ClientCapabilities.Count());
        }
    }
}
