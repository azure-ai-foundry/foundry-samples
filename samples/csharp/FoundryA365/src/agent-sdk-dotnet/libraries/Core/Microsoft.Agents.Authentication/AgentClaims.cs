// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;


namespace Microsoft.Agents.Authentication
{
    /// <summary>
    /// Provides utility methods for working with claims in the context of Agent authentication.
    /// </summary>
    public static class AgentClaims
    {
        /// <summary>
        /// Determines if a token is exchangeable based on its claims. An exchangeable token is one that is not a user token and has 
        /// an audience claim that contains the appId of the token.
        /// </summary>
        /// <remarks>This is intended for use with tokens issued by the Microsoft identity platform including Azure Bot Token Service.</remarks>
        /// <param name="jwtToken">The JWT token to evaluate. Cannot be null.</param>
        /// <returns>True if the token is exchangeable; otherwise, false.</returns>
        public static bool IsExchangeableToken(JwtSecurityToken jwtToken)
        {
            AssertionHelpers.ThrowIfNull(jwtToken, nameof(jwtToken));

            var idtyp = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "idtyp")?.Value;
            if ("user".Equals(idtyp))
            {
                return false;
            }

            var aud = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "aud")?.Value;
            var appId = GetAppId(jwtToken);  // this will use either "appid" or "azp" based on 'ver'
            return (bool)(aud?.Contains(appId));
        }

        /// <summary>
        /// Gets the appid from the given JWT token. This method checks the 'ver' claim to determine whether to look for the appId in the 
        /// 'appid' claim (for v1 tokens) or the 'azp' claim (for v2 tokens). If the 'ver' claim is not present, it will first attempt to 
        /// find the appId in the 'appid' claim and if not found, it will look in the 'azp' claim. Returns null if no suitable claim is found.
        /// </summary>
        /// <remarks>This is intended for use with tokens issued by the Microsoft identity platform including Azure Bot Token Service.</remarks>
        /// <param name="jwtToken">The JWT token to evaluate. Cannot be null.</param>
        /// <returns>The appid or azp value as a string, or null if not found.</returns>
        public static string GetAppId(JwtSecurityToken jwtToken)
        {
            AssertionHelpers.ThrowIfNull(jwtToken, nameof(jwtToken));

            string appIdClaim;
            var ver = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "ver")?.Value ?? "1.0";
            if (ver.Equals("1.0"))
            {
                appIdClaim = jwtToken.Claims?.SingleOrDefault(claim => claim.Type == AuthenticationConstants.AppIdClaim)?.Value;
            }
            else
            {
                appIdClaim = jwtToken.Claims?.SingleOrDefault(claim => claim.Type == AuthenticationConstants.AzpClaim)?.Value;
            }

            return appIdClaim;
        }

        [Obsolete("GetAppId is deprecated, please use GetIncomingAudienceClaim instead.")]
        public static string GetAppId(ClaimsIdentity claimsIdentity)
        {
            return GetIncomingAudienceClaim(claimsIdentity);
        }

        /// <summary>
        /// Retrieves the Audience from the given incoming identity.
        /// </summary>
        /// <param name="claimsIdentity">The incoming identity containing the token information.</param>
        /// <returns>The Audience as a string, or null if not found.</returns>
        /// <remarks>
        /// For requests the Audience is in the 'aud' claim of the JWT token.
        /// </remarks>
        public static string GetIncomingAudienceClaim(ClaimsIdentity claimsIdentity)
        {
            // Verify we have a sensible Claims Identity
            AssertionHelpers.ThrowIfNull(claimsIdentity, nameof(claimsIdentity));

            // For requests from channel App Id is in Audience claim of JWT token. For emulator it is in AppId claim. For
            // unauthenticated requests we have anonymous claimsIdentity provided auth is disabled.
            // For Activities coming from Emulator AppId claim contains the Agent's AAD AppId.
            var appIdClaim = claimsIdentity.Claims?.SingleOrDefault(claim => claim.Type == AuthenticationConstants.AudienceClaim);
            appIdClaim ??= claimsIdentity.Claims?.SingleOrDefault(claim => claim.Type == AuthenticationConstants.AppIdClaim);

            return appIdClaim?.Value;
        }

        /// <summary>
        /// Retrieves the Audience from the given incoming identity.
        /// </summary>
        /// <param name="identity">The claims identity containing the token information.</param>
        /// <returns>The Audience as a string, or null if not found.</returns>
        /// <remarks>
        /// For requests the Audience is in the 'aud' claim of the JWT token.
        /// </remarks>
        public static string GetIncomingAudience(this ClaimsIdentity identity)
        {
            return GetIncomingAudienceClaim(identity);
        }

        /// <summary>
        /// Gets the outgoing AppId from an incoming identity.
        /// </summary>
        /// <remarks>
        /// In v1 tokens the AppId is in the <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.AppIdClaim"/> claim.
        /// In v2 tokens the AppId is in the azp <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.AuthorizedParty"/> claim.
        /// If the <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.VersionClaim"/> is not present, this method will attempt to
        /// obtain the attribute from the <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.AppIdClaim"/> or if present.
        /// </remarks>
        /// <param name="identity">The incoming identity</param>
        /// <returns>The value of the appId claim if found (null if it can't find a suitable claim).</returns>
        public static string GetOutgoingAppIdClaim(ClaimsIdentity identity)
        {
            // Verify we have a sensible Claims Identity
            AssertionHelpers.ThrowIfNull(identity, nameof(identity));

            var claimsList = identity.Claims;
            string appId = null;

            // Depending on Version, the is either in the
            // appid claim (Version 1) or the Authorized Party claim (Version 2).
            var tokenVersion = claimsList.FirstOrDefault(claim => claim.Type == AuthenticationConstants.VersionClaim)?.Value;
            if (string.IsNullOrWhiteSpace(tokenVersion) || tokenVersion == "1.0")
            {
                // either no Version or a version of "1.0" means we should look for
                // the claim in the "appid" claim.
                var appIdClaim = claimsList.FirstOrDefault(c => c.Type == AuthenticationConstants.AppIdClaim);
                appId = appIdClaim?.Value;
            }
            else if (tokenVersion == "2.0")
            {
                // "2.0" puts the AppId in the "azp" claim.
                var appZClaim = claimsList.FirstOrDefault(c => c.Type == AuthenticationConstants.AuthorizedParty);
                appId = appZClaim?.Value;
            }

            return appId;
        }

        /// <summary>
        /// Gets the outgoing AppId from an incoming identity.
        /// </summary>
        /// <remarks>
        /// In v1 tokens the AppId is in the <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.AppIdClaim"/> claim.
        /// In v2 tokens the AppId is in the azp <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.AuthorizedParty"/> claim.
        /// If the <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.VersionClaim"/> is not present, this method will attempt to
        /// obtain the attribute from the <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.AppIdClaim"/> or if present.
        /// </remarks>
        /// <param name="identity">The incoming identity</param>
        /// <returns>The value of the appId claim if found (null if it can't find a suitable claim).</returns>
        public static string GetOutgoingAppId(this ClaimsIdentity identity)
        {
            return GetOutgoingAppIdClaim(identity);
        }

        /// <summary>
        /// Checks if the given incoming identity represents an Agent claim (not coming from ABS/SMBA).
        /// </summary>
        /// <remarks>
        /// An Agent claim should contain:
        ///     An <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.VersionClaim"/> claim.
        ///     An <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.AudienceClaim"/> claim.
        ///     An <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.AppIdClaim"/> claim (v1) or a <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.AuthorizedParty"/> claim (v2).
        /// And the appId claim should be different than the audience claim.
        /// When a channel (webchat, teams, etc.) invokes an Agent, the <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.AudienceClaim"/>
        /// is set to <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.BotFrameworkTokenIssuer"/> but when an Agent calls another Agent,
        /// the audience claim is set to the appId of the Agent being invoked.
        /// The protocol supports v1 and v2 tokens:
        /// For v1 tokens, the  <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.AppIdClaim"/> is present and set to the app Id of the calling Agent.
        /// For v2 tokens, the  <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.AuthorizedParty"/> is present and set to the app Id of the calling Agent.
        /// </remarks>
        /// <param name="identity">The incoming identity to evaluate.</param>
        /// <returns>True if the incoming identity represents an Agent claim, false if it does not.</returns>
        public static bool IsAgentClaim(ClaimsIdentity identity)
        {
            AssertionHelpers.ThrowIfNull(identity, nameof(identity));

            var claimsList = identity.Claims;

            var version = claimsList.FirstOrDefault(claim => claim.Type == AuthenticationConstants.VersionClaim);
            if (string.IsNullOrWhiteSpace(version?.Value))
            {
                // Must have a version claim.
                return false;
            }

            var audience = claimsList.FirstOrDefault(claim => claim.Type == AuthenticationConstants.AudienceClaim)?.Value;
            if (string.IsNullOrWhiteSpace(audience) 
                || AuthenticationConstants.BotFrameworkTokenIssuer.Equals(audience, StringComparison.OrdinalIgnoreCase)
                || AuthenticationConstants.GovBotFrameworkTokenIssuer.Equals(audience, StringComparison.OrdinalIgnoreCase))
            {
                // The audience is https://api.botframework.com or https://api.botframework.us and not an appId.
                return false;
            }

            var appId = GetOutgoingAppIdClaim(identity);
            if (string.IsNullOrWhiteSpace(appId))
            {
                return false;
            }

            // Agent claims must contain and app ID and the AppID must be different than the audience.
            return appId != audience;
        }

        /// <summary>
        /// Checks if the given incoming identity represents an Agent claim (not coming from ABS/SMBA).
        /// </summary>
        /// <remarks>
        /// An Agent claim should contain:
        ///     An <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.VersionClaim"/> claim.
        ///     An <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.AudienceClaim"/> claim.
        ///     An <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.AppIdClaim"/> claim (v1) or a <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.AuthorizedParty"/> claim (v2).
        /// And the appId claim should be different than the audience claim.
        /// When a channel (webchat, teams, etc.) invokes an Agent, the <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.AudienceClaim"/>
        /// is set to <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.BotFrameworkTokenIssuer"/> but when an Agent calls another Agent,
        /// the audience claim is set to the appId of the Agent being invoked.
        /// The protocol supports v1 and v2 tokens:
        /// For v1 tokens, the  <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.AppIdClaim"/> is present and set to the app Id of the calling Agent.
        /// For v2 tokens, the  <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.AuthorizedParty"/> is present and set to the app Id of the calling Agent.
        /// </remarks>
        /// <param name="identity">The incoming identity to evaluate.</param>
        /// <returns>True if the incoming identity represents an Agent claim, false if it does not.</returns>
        public static bool IsAgent(this ClaimsIdentity identity)
        {
            return IsAgentClaim(identity);
        }

        [Obsolete("GetTokenAudience is deprecated, please use GetOutgoingAudienceClaim instead.")]
        public static string GetTokenAudience(ClaimsIdentity identity)
        {
            return GetOutgoingAudienceClaim(identity);
        }

        /// <summary>
        /// Retrieves the audience for an outgoing token from the given incoming identity.
        /// </summary>
        /// <param name="identity">The incoming identity containing the token information.</param>
        /// <returns>The token audience as a string.</returns>
        public static string GetOutgoingAudienceClaim(ClaimsIdentity identity)
        {
            return AgentClaims.IsAgentClaim(identity)
                ? $"api://{AgentClaims.GetOutgoingAppIdClaim(identity)}"
                : AgentClaims.IsGovBotFrameworkClaim(identity)
                    ? AuthenticationConstants.GovBotFrameworkAudience
                    : AuthenticationConstants.BotFrameworkAudience;
        }

        /// <summary>
        /// Retrieves the audience for an outgoing token from the given incoming identity.
        /// </summary>
        /// <param name="identity">The incoming identity containing the token information.</param>
        /// <returns>The token audience as a string.</returns>
        public static string GetOutgoingAudience(this ClaimsIdentity identity)
        {
            return GetOutgoingAudienceClaim(identity);
        }

        /// <summary>
        /// Determines whether the specified incoming identity represents a government Bot Framework claim.
        /// </summary>
        /// <remarks>This method checks the audience claim within the provided incoming identity and
        /// compares it against the expected government Bot Framework token issuer. Use this method to distinguish
        /// government Bot Framework tokens from standard tokens when handling authentication.</remarks>
        /// <param name="identity">The incoming identity to evaluate. Cannot be null.</param>
        /// <returns>true if the claims identity corresponds to a government Bot Framework claim; otherwise, false.</returns>
        public static bool IsGovBotFrameworkClaim(ClaimsIdentity identity)
        {
            AssertionHelpers.ThrowIfNull(identity, nameof(identity));
            var audience = identity.Claims.FirstOrDefault(claim => claim.Type == AuthenticationConstants.AudienceClaim)?.Value;
            return AuthenticationConstants.GovBotFrameworkTokenIssuer.Equals(audience, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether the specified incoming identity represents a Bot Framework claim.
        /// </summary>
        /// <remarks>This method checks the audience claim within the provided incoming identity and
        /// compares it against the expected public Bot Framework token issuer. Use this method to distinguish
        /// public Bot Framework tokens from government tokens when handling authentication.</remarks>
        /// <param name="identity">The incoming identity to evaluate. Cannot be null.</param>
        /// <returns>true if the claims identity corresponds to a public Bot Framework claim; otherwise, false.</returns>
        public static bool IsPublicBotFrameworkClaim(ClaimsIdentity identity)
        {
            AssertionHelpers.ThrowIfNull(identity, nameof(identity));
            var audience = identity.Claims.FirstOrDefault(claim => claim.Type == AuthenticationConstants.AudienceClaim)?.Value;
            return AuthenticationConstants.BotFrameworkTokenIssuer.Equals(audience, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether the specified incoming identity contains a valid Bot Framework claim.
        /// </summary>
        /// <remarks>This method checks for both public and government Bot Framework claims to ascertain
        /// the validity of the incoming identity.</remarks>
        /// <param name="identity">The incoming identity to evaluate for Bot Framework claims. This should represent the user's claims in the
        /// context of the Bot Framework.</param>
        /// <returns>true if the incoming identity contains a valid Bot Framework claim; otherwise, false.</returns>
        public static bool IsBotFrameworkClaim(ClaimsIdentity identity)
        {
            return IsPublicBotFrameworkClaim(identity) || IsGovBotFrameworkClaim(identity);
        }

        /// <summary>
        /// Determines whether the specified incoming identity represents a Bot Framework user.   
        /// </summary>
        /// <remarks>This method checks for specific claims that indicate the identity belongs to a Bot
        /// Framework user. Use this method to distinguish Bot Framework identities from other types of claims
        /// identities.</remarks>
        /// <param name="identity">The incoming identity to evaluate for Bot Framework claims. This parameter cannot be null.</param>
        /// <returns>true if the incoming identity contains Bot Framework claims; otherwise, false.</returns>
        public static bool IsBotFramework(this ClaimsIdentity identity)
        {
            return IsBotFrameworkClaim(identity);
        }

        [Obsolete("GetTokenScopes is deprecated, please use GetOutgoingTokenScopes instead.")]
        public static IList<string> GetTokenScopes(ClaimsIdentity identity)
        {
            return GetOutgoingTokenScopes(identity, false);
        }

        /// <summary>
        /// Retrieves the token scopes from the given incoming identity.
        /// </summary>
        /// <param name="identity">The incoming identity containing the token information.</param>
        /// <param name="defaultABSScopes">Normally the IAccessTokenProvider will use what's in settings.  This is what we want for 
        /// Azure Bot Service since that can change for Public vs Gov, etc...  For agent scenarios though, it's always defaulted
        /// to "{{appid}}/.default".</param>
        /// <returns>A list of token scopes.</returns>
        public static IList<string> GetOutgoingTokenScopes(this ClaimsIdentity identity, bool defaultABSScopes = false)
        {
            return AgentClaims.IsAgentClaim(identity)
                ? [$"{AgentClaims.GetOutgoingAppIdClaim(identity)}/.default"]
                : defaultABSScopes ? DefaultAzureBotServiceScopes(identity) : null;
        }

        /// <summary>
        /// Retrieves the token scopes from the given incoming identity.
        /// </summary>
        /// <param name="identity">The incoming identity containing the token information.</param>
        /// <param name="defaultABSScopes">Normally the IAccessTokenProvider will use what's in settings.  This is what we want for 
        /// Azure Bot Service since that can change for Public vs Gov, etc...  For agent scenarios though, it's always defaulted
        /// to "{{appid}}/.default".</param>
        /// <returns>A list of token scopes.</returns>
        public static IList<string> GetOutgoingScopes(this ClaimsIdentity identity, bool defaultABSScopes = false)
        {
            return GetOutgoingTokenScopes(identity, defaultABSScopes);
        }

        /// <summary>
        /// Returns the default Azure Bot Service scopes based on the incoming identity. If the incoming identity corresponds to a government Bot Framework 
        /// claim, it returns the government-specific default scope; otherwise, it returns the standard default scope.
        /// </summary>
        /// <param name="identity">The incoming identity containing the token information.</param>
        /// <returns>A list of token scopes.</returns>
        public static IList<string> DefaultAzureBotServiceScopes(ClaimsIdentity identity)
        {
            return IsGovBotFrameworkClaim(identity) ? [AuthenticationConstants.GovBotFrameworkDefaultScope] : [AuthenticationConstants.BotFrameworkDefaultScope];
        }

        /// <summary>
        /// Determines whether anonymous access is allowed based on the given incoming identity.
        /// </summary>
        /// <param name="identity">The incoming identity to evaluate.</param>
        /// <returns>True if anonymous access is allowed, otherwise false.</returns>
        public static bool AllowAnonymous(ClaimsIdentity identity)
        {
            return identity != null && !identity.IsAuthenticated && !identity.Claims.Any();
        }

        /// <summary>
        /// Creates an ingress Agent ClaimsIdentity.
        /// </summary>
        /// <param name="audience">The aud of the claim.  Typically the ClientId of the Agent.</param>
        /// <param name="anonymous"></param>
        /// <param name="appId">The appId of the sender. Not supplying the appId will work correctly against Azure Bot Service, but not to another Agent.</param>
        /// <returns></returns>
        public static ClaimsIdentity CreateIdentity(string audience, bool anonymous = false, string appId = null)
        {
            if (anonymous)
            {
                return new ClaimsIdentity();
            }

            IEnumerable<Claim> claims = [
                    new(AuthenticationConstants.AudienceClaim, audience),
                    new(AuthenticationConstants.VersionClaim, "1.0")
                ];

            if (!string.IsNullOrEmpty(appId))
            {
                claims = claims.Append(new(AuthenticationConstants.AppIdClaim, appId));
            }

            return new ClaimsIdentity(claims);
        }
    }
}
