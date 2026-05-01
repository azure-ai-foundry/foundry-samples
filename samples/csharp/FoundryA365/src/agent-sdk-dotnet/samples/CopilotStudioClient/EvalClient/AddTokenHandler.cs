// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace EvalClient
{
    /// <summary>
    /// This sample uses an HttpClientHandler to add an authentication token to the request.
    /// </summary>
    /// <param name="settings">Direct To engine connection settings.</param>
    internal class AddTokenHandler(EvalClientConfig settings) : DelegatingHandler(new HttpClientHandler())
    {
        private static readonly string _keyChainServiceName = "copilot_studio_client_app";
        private static readonly string _keyChainAccountName = "copilot_studio_client";
        
        private async Task<AuthenticationResult> AuthenticateAsync(CancellationToken ct = default!)
        {
            ArgumentNullException.ThrowIfNull(settings);

            string[] scopes = ["https://api.powerplatform.com/.default"];
            //string[] scopes = ["https://api.gov.powerplatform.microsoft.us/CopilotStudio.Copilots.Invoke"];

            IPublicClientApplication app = PublicClientApplicationBuilder.Create(settings.AppClientId)
                 .WithAuthority(AadAuthorityAudience.AzureAdMyOrg)
                 .WithTenantId(settings.TenantId)
                 .WithRedirectUri("http://localhost")
                 .Build();

            string currentDir = Path.Combine(AppContext.BaseDirectory, "mcs_client_console");

            if (!Directory.Exists(currentDir))
            {
                Directory.CreateDirectory(currentDir);
            }

            StorageCreationPropertiesBuilder storageProperties = new("TokenCache", currentDir);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                storageProperties.WithLinuxUnprotectedFile();
            }
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                storageProperties.WithMacKeyChain(_keyChainServiceName, _keyChainAccountName);
            }
            
            MsalCacheHelper tokenCacheHelper = await MsalCacheHelper.CreateAsync(storageProperties.Build());
            tokenCacheHelper.RegisterCache(app.UserTokenCache);

            IAccount? account = (await app.GetAccountsAsync()).FirstOrDefault();

            AuthenticationResult authResponse;
            try
            {
                authResponse = await app.AcquireTokenSilent(scopes, account).ExecuteAsync(ct);
            }
            catch (MsalUiRequiredException)
            {
                authResponse = await app.AcquireTokenInteractive(scopes).ExecuteAsync(ct);
            }
            return authResponse;
        }

        /// <summary>
        /// Handles sending the request and adding the token to the request.
        /// </summary>
        /// <param name="request">Request to be sent</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Headers.Authorization is null)
            {
                AuthenticationResult authResponse = await AuthenticateAsync(cancellationToken);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResponse.AccessToken);
            }
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
