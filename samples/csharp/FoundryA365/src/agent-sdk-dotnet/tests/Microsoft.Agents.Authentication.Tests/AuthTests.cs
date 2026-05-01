// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Msal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Agents.Auth.Tests
{
    public class AuthTests
    {
        ITestOutputHelper _output = null; 
        public AuthTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [SkippableConnectionTest]
        //[Fact]
        [Trait("Category", "Live Connect Creds Required")]
        public async Task GetClientSecretAccessTokenTest_MSAL()
        {
            // Setup service provider for DI
            IServiceProvider serviceProvider = SetupServiceCollection.GenerateAuthMinServiceProvider("testMSALappsettings.json", _output);
            IConfiguration config = serviceProvider.GetService<IConfiguration>(); 
            
            // Create a new instance of MSAL Auth flow
            MsalAuth authClient = new MsalAuth(serviceProvider, config.GetSection("ServiceConnection"));
            
            // Create empty Scopes List
            List<string> scopes = new List<string>();

            // Decode JWT and check expiration date
            var handler = new JwtSecurityTokenHandler();

            // Request Access token 
            var accessToken = await authClient.GetAccessTokenAsync("api://c3b0ec60-15e3-4974-bebf-0ad0bcec17be", scopes, false);
            Assert.NotNull(accessToken);

            // outbound query
            scopes.Add("01f3068f-2bdb-4fa1-baf9-24892b6795d1/.default");
            accessToken = await authClient.GetAccessTokenAsync("api://c3b0ec60-15e3-4974-bebf-0ad0bcec17be", scopes, false);
            Assert.NotNull(accessToken);

            // no outbound
            scopes.Clear();
            accessToken = await authClient.GetAccessTokenAsync("api://c3b0ec60-15e3-4974-bebf-0ad0bcec17be", scopes, false);
            Assert.NotNull(accessToken);

            // no outbound
            accessToken = await authClient.GetAccessTokenAsync("api://c3b0ec60-15e3-4974-bebf-0ad0bcec17be", scopes, false);
            Assert.NotNull(accessToken);

            accessToken = await authClient.GetAccessTokenAsync("api://c3b0ec60-15e3-4974-bebf-0ad0bcec17be", scopes, true);
            Assert.NotNull(accessToken);


            /*
            var accessToken = await authClient.GetAccessTokenAsync("https://graph.microsoft.com/", scopes, false);
            Assert.NotNull(accessToken);
            
            var jwtToken = handler.ReadJwtToken(accessToken);
            var expirationDate = jwtToken.ValidTo;
            _output.WriteLine($"Access Token: {accessToken}");

            // Re Request Same Access Token. 
            accessToken = await authClient.GetAccessTokenAsync("https://graph.microsoft.com/", scopes, false);
            Assert.NotNull(accessToken);
            jwtToken = handler.ReadJwtToken(accessToken);
            expirationDate = jwtToken.ValidTo;

            // force a refresh of access token: 
            accessToken = await authClient.GetAccessTokenAsync("https://graph.microsoft.com/", scopes, true);
            Assert.NotNull(accessToken);
            jwtToken = handler.ReadJwtToken(accessToken);
            expirationDate = jwtToken.ValidTo;
            */
        }

        [Fact]
        public void ValidateAuthConfig()
        {
            IServiceProvider serviceProvider = SetupServiceCollection.GenerateAuthMinServiceProvider("authvalidation_appsettings.json", _output);
            IConfiguration config = serviceProvider.GetService<IConfiguration>();

            //Check for broken configuration.
            TestAuthConfig(serviceProvider, config, "SystemManagedIdentityAuthFFFFF", "SystemManagedIdentityAuthFFFFF", false);
            TestAuthConfig(serviceProvider, null, "msalConfigurationSection", "Value cannot be null", false);

            // Check for valid config but good and bad values.
            TestAuthConfig(serviceProvider, config, "CertAuth", "ClientId");
            TestAuthConfig(serviceProvider, config, "CertSubjectAuth", "CertificateSubjectName");
            TestAuthConfig(serviceProvider, config, "ClientSecretAuth", "ClientSecret");
            TestAuthConfig(serviceProvider, config, "UserManagedIdentityAuth", "ClientId");
            TestAuthConfig(serviceProvider, config, "SystemManagedIdentityAuth", string.Empty, false);
        }

        /// <summary>
        /// Get and invoke the ValidateConfiguration method.
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <param name="config"></param>
        /// <param name="configName"></param>
        /// <param name="expectedErrorString"></param>
        /// <param name="testFailure"></param>
        private void TestAuthConfig(IServiceProvider serviceProvider, IConfiguration config, string configName , string expectedErrorString, bool testFailure=true)
        {
            // Success Path. 

            MsalAuth authClient = null;
            try
            {
                authClient = new MsalAuth(serviceProvider, config?.GetSection(configName));
            }
            catch (Exception ex)
            {
                Assert.Contains(expectedErrorString, ex.Message);
                return; // stop test. 
            }

            if (!testFailure)
            {
                return;
            }

            // Fail Path. 
            try
            {
                authClient = new MsalAuth(serviceProvider, config.GetSection($"{configName}_fail"));
                throw new Exception("Expected Exception not thrown");
            }
            catch (Exception ex)
            {
                Assert.Contains(expectedErrorString, ex.Message);
            }
        }
    }
}
