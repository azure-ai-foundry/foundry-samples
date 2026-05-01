// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.Identity.Client;
using Microsoft.Agents.CopilotStudio.Client;

namespace OrchestratedClientSample
{
    /// <summary>
    /// HttpClientHandler that adds an interactive authentication token to requests.
    /// This is used for the interactive authentication flow.
    /// For more information on how to setup various authentication flows, see the Microsoft Identity documentation at https://aka.ms/msal.
    /// </summary>
    /// <param name="settings">Orchestrated client connection settings.</param>
    internal class AddTokenHandler(SampleConnectionSettings settings) : DelegatingHandler(new HttpClientHandler())
    {
        private static readonly string _keyChainServiceName = "orchestrated_client_app";
        private static readonly string _keyChainAccountName = "orchestrated_client";

        private async Task<AuthenticationResult> AuthenticateAsync(CancellationToken ct = default!)
        {
            ArgumentNullException.ThrowIfNull(settings);

            string[] scopes = [CopilotClient.ScopeFromSettings(settings)];

            IPublicClientApplication app = PublicClientApplicationBuilder.Create(settings.AppClientId)
                 .WithAuthority(AadAuthorityAudience.AzureAdMyOrg)
                 .WithTenantId(settings.TenantId)
                 .WithRedirectUri("http://localhost")
                 .Build();

            string currentDir = Path.Combine(AppContext.BaseDirectory, "orchestrated_client_console");

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
