// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using Xunit;

namespace Microsoft.Agents.Auth.Tests
{
    public class AgentClaimsTests
    {
        [Fact]
        public void GetAppId_ThrowIfNull()
        {
            Assert.Throws<ArgumentNullException>(() => ((ClaimsIdentity)null).GetIncomingAudience());
        }

        [Fact]
        public void GetAppId_NullIfNoClaims()
        {
            ClaimsIdentity identity = new();
            Assert.Null(identity.GetIncomingAudience());
        }

        [Fact]
        public void GetAppId_NullIfNoAudienceOrAppIDClaims()
        {
            // Setup a claim set with a claim, but not with a valid Audience or AppID claim. 
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, "Name1")
            };
            ClaimsIdentity identity = new(claims);

            // Verify asking for an AppId on a claimset without the right claims throws an exception
            Assert.Null(identity.GetIncomingAudience());
        }
        [Fact]
        public void GetAppId_ThrowIf2AudienceClaims()
        {
            // Setup a claim set with a 2 claims, both of which as Audience claims.
            var claims = new List<Claim>
            {
                new(AuthenticationConstants.AudienceClaim, "claim1"),
                new(AuthenticationConstants.AudienceClaim, "claim2")
            };

            ClaimsIdentity identity = new(claims);

            // Verify asking for an AppId on a claimset without the right claims throws an exception
            Assert.Throws<InvalidOperationException>(() => identity.GetIncomingAudience());
        }

        [Fact]
        public void GetAppId_ThrowIf2AppIdClaims()
        {
            // Setup a claim set with a 2 claims, both of which are AppId claims.
            var claims = new List<Claim>
            {
                new(AuthenticationConstants.AppIdClaim, "claim1"),
                new(AuthenticationConstants.AppIdClaim, "claim2")
            };

            ClaimsIdentity identity = new(claims);

            // Verify asking for an AppId on a claimset without the right claims throws an exception
            Assert.Throws<InvalidOperationException>(() => identity.GetIncomingAudience());
        }

        [Fact]
        public void GetAppId_AudienceClaim()
        {
            // Setup a claim set with a 2 claims, both of which are AppId claims.
            var claims = new List<Claim>
            {
                new(AuthenticationConstants.AudienceClaim, "claim1"),
            };

            ClaimsIdentity identity = new(claims);

            // Verify asking for an AppId on a claimset without the right claims throws an exception
            Assert.Equal("claim1", identity.GetIncomingAudience());
        }

        [Fact]
        public void GetAppId_AppIdClaim()
        {
            // Setup a claim set with a 2 claims, both of which are AppId claims.
            var claims = new List<Claim>
            {
                new(AuthenticationConstants.AppIdClaim, "claim1"),
            };

            ClaimsIdentity identity = new(claims);

            // Verify asking for an AppId on a claimset without the right claims throws an exception
            Assert.Equal("claim1", identity.GetIncomingAudience());
        }

        [Fact]
        public void GetAppId_AudienceAndAppIdClaim()
        {
            // Setup a claim set with a 2 claims. Verify Audiance is chosen over AppId. 
            var claims = new List<Claim>
            {
                new(AuthenticationConstants.AppIdClaim, "claim1"),
                new(AuthenticationConstants.AudienceClaim, "claim2"),
            };

            ClaimsIdentity identity = new(claims);

            // Verify asking for an AppId on a claimset with both claims returns the Audiance claim.
            Assert.Equal("claim2", identity.GetIncomingAudience());
        }

        [Fact]
        public void GetOutgoingAppId_ThrowIfNull()
        {
            Assert.Throws<ArgumentNullException>(() => AgentClaims.GetOutgoingAppIdClaim(null));
        }

        [Fact]
        public void GetOutgoingAppId_NoVersionClaim()
        {
            var claims = new ClaimsIdentity(
            [
                new(ClaimTypes.Name, "Name1")
            ]);

            Assert.Null(AgentClaims.GetOutgoingAppIdClaim(claims));
        }

        [Fact]
        public void GetOutgoingAppId_MultipleVersionClaims()
        {
            var claims = new ClaimsIdentity(
            [
                new(AuthenticationConstants.VersionClaim, "claim1"),
                new(AuthenticationConstants.VersionClaim, "claim2")
            ]);

            Assert.Null(AgentClaims.GetOutgoingAppIdClaim(claims));
        }

        [Fact]
        public void GetOutgoingAppId_InvalidVersionNumber()
        {
            var claims = new ClaimsIdentity(
            [
                new(AuthenticationConstants.VersionClaim, "3.0"),                
            ]);

            Assert.Null(AgentClaims.GetOutgoingAppIdClaim(claims));
        }

        [Fact]
        public void GetOutgoingAppId_ValidVersionClaimNullVersionNoAppId()
        {
            // this tests sets up a claimset with a version claim with an empty string. There is no AppIdClaim, so this should fail. 
            var claims = new ClaimsIdentity(
            [
                new(AuthenticationConstants.VersionClaim, string.Empty),
            ]);

            Assert.Null(AgentClaims.GetOutgoingAppIdClaim(claims));
        }
        [Fact]
        public void GetOutgoingAppId_ValidVersionClaimNullVersionAppId()
        {
            // this tests sets up a claimset with a empty version and an AppId Claim. This is a valid combination. 
            var claims = new ClaimsIdentity(
            [
                new(AuthenticationConstants.VersionClaim, string.Empty),
                new(AuthenticationConstants.AppIdClaim, "appId")                
            ]);

            Assert.Equal("appId", AgentClaims.GetOutgoingAppIdClaim(claims));
        }
        
        [Fact]
        public void GetOutgoingAppId_ValidV1VersionAppId()
        {
            // this tests sets up a claimset with a version of v1 and an AppId Claim. This is a valid combination. 
            var claims = new ClaimsIdentity(
            [
                new(AuthenticationConstants.VersionClaim, "1.0"),
                new(AuthenticationConstants.AppIdClaim, "appId")
            ]);

            Assert.Equal("appId", AgentClaims.GetOutgoingAppIdClaim(claims));
        }

        [Fact]
        public void GetOutgoingAppId_ValidV2VersionWrongClaim()
        {
            // this tests sets up a claimset with a version claim with an empty string. There is no AppIdClaim, so this should fail. 
            var claims = new ClaimsIdentity(
            [
                new(AuthenticationConstants.VersionClaim, "2.0"),
                new(AuthenticationConstants.AppIdClaim, "appId") // not a valid claim on a v2 token
            ]);

            Assert.Null(AgentClaims.GetOutgoingAppIdClaim(claims));            
        }


        [Fact]
        public void GetOutgoingAppId_ValidV2VersionClaimWithAuthorizedParty()
        {
            var claims = new ClaimsIdentity(
            [
                // this tests sets up a claimset with a version claim "2.0" and an AuthorizedParty Claim.
                new(AuthenticationConstants.VersionClaim, "2.0"),
                new(AuthenticationConstants.AuthorizedParty, "appId")
            ]);

            Assert.Equal("appId", AgentClaims.GetOutgoingAppIdClaim(claims));
        }

        [Fact]
        public void IsAgentClaim_ThrowOnNullClaimset()
        {
            Assert.Throws<ArgumentNullException>(() => AgentClaims.IsAgentClaim(null));
        }

        [Fact]
        public void IsAgentClaim_ValidClaimset()
        {
            // Setup a valid claim set
            var claims = new ClaimsIdentity(
            [
                new(AuthenticationConstants.VersionClaim, "2.0"),
                new(AuthenticationConstants.AuthorizedParty, "party"),
                new(AuthenticationConstants.AudienceClaim, "audience")
            ]);

            Assert.True(AgentClaims.IsAgentClaim(claims));
        }

        [Fact]
        public void IsAgentClaim_InvalidClaimset()
        {
            // Setup a valid claim set
            var claims = new ClaimsIdentity(
            [
                new(AuthenticationConstants.VersionClaim, "2.0"),
                new(AuthenticationConstants.AuthorizedParty, "party"), // if these 2 match, the claim set doesn't qualify
                new(AuthenticationConstants.AudienceClaim, "party") // if these 2 match, the claim set doesn't qualify
            ]);

            Assert.False(AgentClaims.IsAgentClaim(claims));
        }

        [Fact]
        public void IsAgentClaim_InvalidClaimsetByAudiance()
        {
            // Setup a valid claim set
            var claims = new ClaimsIdentity(
            [
                new(AuthenticationConstants.VersionClaim, "2.0"),
                new(AuthenticationConstants.AuthorizedParty, "party"), 

                // For some reason, this is invalid. Coding it here to make sure behavior doesn't change. 
                new(AuthenticationConstants.AudienceClaim, AuthenticationConstants.BotFrameworkTokenIssuer) 
            ]);

            Assert.False(AgentClaims.IsAgentClaim(claims));
        }

        [Fact]
        public void IsAgentClaim_ShouldReturnFalseOnNoVersion()
        {
            var claims = new ClaimsIdentity(
            [
                new(AuthenticationConstants.AudienceClaim, "audience"),
                new(AuthenticationConstants.AppIdClaim, "appId")
            ]);

            Assert.False(AgentClaims.IsAgentClaim(claims));
        }

        [Fact]
        public void IsAgentClaim_ShouldReturnFalseOnNoOutgoingAppId()
        {
            // Setup an invalid claim set
            var claims = new ClaimsIdentity(
            [
                new(AuthenticationConstants.VersionClaim, "2.0"),
                new(AuthenticationConstants.AudienceClaim, "audience"),
                new(AuthenticationConstants.AppIdClaim, "appId") // not a valid claim on a v2 token
            ]);

            Assert.False(AgentClaims.IsAgentClaim(claims));
        }

        [Fact]
        public void GetTokenAudience_ForBot()
        {
            var claims = new ClaimsIdentity(
            [
                new(AuthenticationConstants.VersionClaim, "2.0"),
                new(AuthenticationConstants.AuthorizedParty, "appId"),
                new(AuthenticationConstants.AudienceClaim, "aud")
            ]);

            var audience = claims.GetOutgoingAudience();

            Assert.Equal("api://appId", audience);
        }

        [Fact]
        public void GetTokenAudience_ForABS()
        {
            var claims = new ClaimsIdentity(
            [
                new(AuthenticationConstants.VersionClaim, "2.0"),
                new(AuthenticationConstants.AuthorizedParty, "appId"),
            ]);

            var audience = claims.GetOutgoingAudience();

            Assert.Equal(AuthenticationConstants.BotFrameworkAudience, audience);
        }

        [Fact]
        public void GetOutgoingTokenScopes_AgentClaim_ReturnsAppIdDefaultScope()
        {
            // Agent claim: version + azp + audience (different from azp)
            var claims = new ClaimsIdentity(
            [
                new(AuthenticationConstants.VersionClaim, "2.0"),
                new(AuthenticationConstants.AuthorizedParty, "myAppId"),
                new(AuthenticationConstants.AudienceClaim, "audience")
            ]);

            var scopes = claims.GetOutgoingTokenScopes();

            Assert.NotNull(scopes);
            Assert.Single(scopes);
            Assert.Equal("myAppId/.default", scopes[0]);
        }

        [Fact]
        public void GetOutgoingTokenScopes_NonAgentClaim_DefaultABSScopesFalse_ReturnsNull()
        {
            // ABS claim: no audience that qualifies as agent claim
            var claims = new ClaimsIdentity(
            [
                new(AuthenticationConstants.VersionClaim, "2.0"),
                new(AuthenticationConstants.AuthorizedParty, "appId"),
            ]);

            var scopes = claims.GetOutgoingTokenScopes(defaultABSScopes: false);

            Assert.Null(scopes);
        }

        [Fact]
        public void GetOutgoingTokenScopes_NonAgentClaim_DefaultABSScopesTrue_ReturnsPublicABSScope()
        {
            // Public ABS claim: audience is BotFrameworkTokenIssuer
            var claims = new ClaimsIdentity(
            [
                new(AuthenticationConstants.VersionClaim, "2.0"),
                new(AuthenticationConstants.AuthorizedParty, "appId"),
                new(AuthenticationConstants.AudienceClaim, AuthenticationConstants.BotFrameworkTokenIssuer)
            ]);

            var scopes = claims.GetOutgoingTokenScopes(defaultABSScopes: true);

            Assert.NotNull(scopes);
            Assert.Single(scopes);
            Assert.Equal(AuthenticationConstants.BotFrameworkDefaultScope, scopes[0]);
        }

        [Fact]
        public void GetOutgoingTokenScopes_NonAgentClaim_DefaultABSScopesTrue_ReturnsGovABSScope()
        {
            // Gov ABS claim: audience is GovBotFrameworkTokenIssuer
            var claims = new ClaimsIdentity(
            [
                new(AuthenticationConstants.VersionClaim, "2.0"),
                new(AuthenticationConstants.AuthorizedParty, "appId"),
                new(AuthenticationConstants.AudienceClaim, AuthenticationConstants.GovBotFrameworkTokenIssuer)
            ]);

            var scopes = claims.GetOutgoingTokenScopes(defaultABSScopes: true);

            Assert.NotNull(scopes);
            Assert.Single(scopes);
            Assert.Equal(AuthenticationConstants.GovBotFrameworkDefaultScope, scopes[0]);
        }
    }
}
