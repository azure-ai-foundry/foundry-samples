// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Authentication
{
    /// <summary>
    /// Values and constants used for authentication and authorization by the activity protocol.
    /// </summary>
    public static class AuthenticationConstants
    {
        /// <summary>
        /// Bot Framework OAuth scope to request.
        /// </summary>
        [Obsolete("This constant is deprecated. Please use BotFrameworkAudience or BotFrameworkDefaultScope instead.")]
        public const string BotFrameworkScope = "https://api.botframework.com";

        /// <summary>
        /// Bot Framework audience to request.
        /// </summary>
        public const string BotFrameworkAudience = "https://api.botframework.com";

        /// <summary>
        /// Bot Framework Gov audience to request.
        /// </summary>
        public const string GovBotFrameworkAudience = "https://api.botframework.us";

        /// <summary>
        /// Bot Framework China audience to request.
        /// </summary>
        public const string ChinaBotFrameworkAudience = "https://api.botframework.azure.cn";

        /// <summary>
        /// Bot Framework OAuth scope to request.
        /// </summary>
        public const string BotFrameworkDefaultScope = "https://api.botframework.com/.default";

        /// <summary>
        /// Bot Framework Gov OAuth scope to request.
        /// </summary>
        public const string GovBotFrameworkDefaultScope = "https://api.botframework.us/.default";

        /// <summary>
        /// Bot Framework China (Gallatin/Mooncake) OAuth scope to request.
        /// </summary>
        public const string ChinaBotFrameworkDefaultScope = "https://api.botframework.azure.cn/.default";

        /// <summary>
        /// Token issuer for ABS tokens.
        /// </summary>
        public const string BotFrameworkTokenIssuer = "https://api.botframework.com";

        /// <summary>
        /// Token issuer for Gov ABS tokens.
        /// </summary>
        public const string GovBotFrameworkTokenIssuer = "https://api.botframework.us";

        /// <summary>
        /// Token issuer for China (Gallatin/Mooncake) ABS tokens.
        /// </summary>
        public const string ChinaBotFrameworkTokenIssuer = "https://api.botframework.azure.cn";

        /// <summary>
        /// Gets the URL template for the Bot Framework login authority, which includes a placeholder for the tenant ID.
        /// </summary>
        public const string BotFrameworkLoginUrlTemplate = "https://login.microsoftonline.com/{0}";

        /// <summary>
        /// Gets the URL template for the Gov Bot Framework login authority, which includes a placeholder for the tenant ID.
        /// </summary>
        public const string GovBotFrameworkLoginUrlTemplate = "https://login.microsoftonline.us/{0}";

        /// <summary>
        /// Gets the URL template for the China (Gallatin/Mooncake) Bot Framework login authority, which includes a placeholder for the tenant ID.
        /// </summary>
        public const string ChinaBotFrameworkLoginUrlTemplate = "https://login.partner.microsoftonline.cn/0b4a31a2-c1a0-475d-b363-5f26668660a3/{0}";

        /// <summary>
        /// Default Url for the Azure Bot Token Service.
        /// </summary>
        public const string BotFrameworkOAuthUrl = "https://api.botframework.com";

        /// <summary>
        /// Default Url for the Gov Azure Bot Token Service.
        /// </summary>
        public const string GovBotFrameworkOAuthUrl = "https://api.botframework.azure.us";

        /// <summary>
        /// Default Url for the China (Gallatin/Mooncake) Azure Bot Token Service.
        /// </summary>
        public const string ChinaBotFrameworkOAuthUrl = "https://token.botframework.azure.cn/";

        /// <summary>
        /// The OpenID metadata URL for the public Azure Bot Service.
        /// </summary>
        public const string PublicAzureBotServiceOpenIdMetadataUrl = "https://login.botframework.com/v1/.well-known/openidconfiguration";

        /// <summary>
        /// The OpenID metadata URL for public Azure Active Directory.
        /// </summary>
        public const string PublicOpenIdMetadataUrl = "https://login.microsoftonline.com/common/v2.0/.well-known/openid-configuration";

        /// <summary>
        /// The OpenID metadata URL format for enterprise channels.
        /// </summary>
        public const string EnterpriseChannelOpenIdMetadataUrlFormat = "https://{0}.enterprisechannel.botframework.com/v1/.well-known/openidconfiguration";

        /// <summary>
        /// The OpenID metadata URL for the government Azure Bot Service.
        /// </summary>
        public const string GovAzureBotServiceOpenIdMetadataUrl = "https://login.botframework.azure.us/v1/.well-known/openidconfiguration";

        /// <summary>
        /// The OpenID metadata URL for government Azure Active Directory.
        /// </summary>
        public const string GovOpenIdMetadataUrl = "https://login.microsoftonline.us/cab8a31a-1906-4287-a0d8-4eef66b95f6e/v2.0/.well-known/openid-configuration";

        /// <summary>
        /// The OpenID metadata URL for the China (Gallatin/Mooncake) Azure Bot Service.
        /// </summary>
        public const string ChinaAzureBotServiceOpenIdMetadataUrl = "https://login.botframework.azure.cn/v1/.well-known/openidconfiguration";

        /// <summary>
        /// The OpenID metadata URL for China (Gallatin/Mooncake)
        /// </summary>
        public const string ChinaOpenIdMetadataUrl = "https://login.partner.microsoftonline.cn/a55a4d5b-9241-49b1-b4ff-befa8db00269/.well-known/openid-configuration";

        /// <summary>
        /// The V1 Azure AD token issuer URL template that will contain the tenant id where the token was issued from.
        /// </summary>
        public const string ValidTokenIssuerUrlTemplateV1 = "https://sts.windows.net/{0}/";

        /// <summary>
        /// The V2 Azure AD token issuer URL template that will contain the tenant id where the token was issued from.
        /// </summary>
        public const string ValidTokenIssuerUrlTemplateV2 = "https://login.microsoftonline.com/{0}/v2.0";

        /// <summary>
        /// The Government V2 Azure AD token issuer URL template that will contain the tenant id where the token was issued from.
        /// </summary>
        public const string ValidGovernmentTokenIssuerUrlTemplateV2 = "https://login.microsoftonline.us/{0}/v2.0";

        /// <summary>
        /// The China (Gallatin/Mooncake) V2 Azure AD token issuer URL template that will contain the tenant id where the token was issued from.
        /// </summary>
        public const string ValidChinaTokenIssuerUrlTemplateV2 = "https://login.partner.microsoftonline.cn/0b4a31a2-c1a0-475d-b363-5f26668660a3/{0}/v2.0";

        /// <summary>
        /// "azp" Claim.
        /// </summary>
        /// <remarks>
        /// Authorized party - the party to which the ID Token was issued.
        /// This claim follows the general format set forth in the OpenID Spec. http://openid.net/specs/openid-connect-core-1_0.html#IDToken.
        /// </remarks>
        public const string AuthorizedParty = "azp";

        /// <summary>
        /// Audience Claim. 
        /// </summary>
        /// <remarks>
        /// The "aud" (audience) claim identifies the recipients that the JWT is
        /// intended for. Each principal intended to process the JWT MUST
        /// identify itself with a value in the audience claim. If the principal
        /// processing the claim does not identify itself with a value in the
        /// "aud" claim when this claim is present, then the JWT MUST be
        /// rejected. In the general case, the "aud" value is an array of case-
        /// sensitive strings, each containing a StringOrURI value. In the
        /// special case when the JWT has one audience, the "aud" value MAY be a
        /// single case-sensitive string containing a StringOrURI value. The
        /// interpretation of audience values is generally application specific.
        /// Use of this claim is OPTIONAL.
        /// 
        /// For more details, see <a href="https://tools.ietf.org/html/rfc7519#section-4.1.3">RFC 7519 Section 4.1.3</a>.
        /// </remarks>
        public const string AudienceClaim = "aud";

        /// <summary>
        /// Token iss claim name. As used in Microsoft AAD tokens.
        /// </summary>
        public const string IssuerClaim = "iss";

        /// <summary>
        /// Token version claim name. As used in Microsoft AAD tokens.
        /// </summary>
        public const string VersionClaim = "ver";

        /// <summary>
        /// App ID claim name. As used in Microsoft AAD 1.0 tokens.
        /// </summary>
        public const string AppIdClaim = "appid";

        /// <summary>
        /// App ID claim name. As used in Microsoft AAD 2.0 tokens.
        /// </summary>
        public const string AzpClaim = "azp";

        /// <summary>
        /// Tenant Id claim name. As used in Microsoft AAD tokens.
        /// </summary>
        public const string TenantIdClaim = "tid";

        public const string ApxLocalScope = "c16e153d-5d2b-4c21-b7f4-b05ee5d516f1/.default";
        public const string ApxDevScope = "0d94caae-b412-4943-8a68-83135ad6d35f/.default";
        public const string ApxProductionScope = "5a807f24-c9de-44ee-a3a7-329e88a00ffc/.default";
        public const string ApxGCCScope = "c9475445-9789-4fef-9ec5-cde4a9bcd446/.default";
        public const string ApxGCCHScope = "6f669b9e-7701-4e2b-b624-82c9207fde26/.default";
        public const string ApxDoDScope = "0a069c81-8c7c-4712-886b-9c542d673ffb/.default";
        public const string ApxGallatinScope = "bd004c8e-5acf-4c48-8570-4e7d46b2f63b/.default";
    }
}
