// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Msal.Model;
using Microsoft.Identity.Client;
using Moq;
using System;
using Xunit;

namespace Microsoft.Agents.Authentication.Msal.Tests.Model
{
    public class ExecuteAuthenticationResultsTests
    {
        [Fact]
        public void GetAuthTokenAndProperties_ShouldReturnMsalAuthResultToken()
        {
            const string token = "test-token";

            var results = new ExecuteAuthenticationResults()
            {
                MsalAuthResult = new AuthenticationResult(token, false, null, DateTimeOffset.UtcNow.AddMinutes(5), DateTimeOffset.UtcNow.AddMinutes(5), null, null, null, null, Guid.NewGuid()),
                TargetServiceUrl = new Uri("http://test.com"),
                MsalAuthClient = "test-client",
                Authority = "authority",
                Resource = "resource",
                UserIdent = new Mock<IAccount>().Object
            };

            var resultToken = results.GetAuthTokenAndProperties(out var outResult, out var outUrl, out var outClient, out var outAuth, out var outResource, out var outUserIdent);

            Assert.Equal(token, resultToken);
            Assert.Equal(results.MsalAuthResult, outResult);
            Assert.Equal(results.TargetServiceUrl, outUrl);
            Assert.Equal(results.MsalAuthClient, outClient);
            Assert.Equal(results.Authority, outAuth);
            Assert.Equal(results.Resource, outResource);
            Assert.Equal(results.UserIdent, outUserIdent);
        }
    }
}
