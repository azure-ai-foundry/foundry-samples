// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Msal.Interfaces;
using Microsoft.Agents.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using System;
using System.IO;

namespace Microsoft.Agents.Authentication.Msal.Model
{
    /// <summary>
    /// Gets and manages connection settings for MSAL Auth
    /// </summary>
    public class ConnectionSettings : ConnectionSettingsBase, IMSALConnectionSettings
    {
        public ConnectionSettings() : base() { }

        public ConnectionSettings(IConfigurationSection msalConfigurationSection) : base(msalConfigurationSection)
        {
            AssertionHelpers.ThrowIfNull(msalConfigurationSection, nameof(msalConfigurationSection));

            if (msalConfigurationSection != null)
            {
                CertificateThumbPrint = msalConfigurationSection.GetValue<string>("CertThumbprint", string.Empty);
                CertificateSubjectName = msalConfigurationSection.GetValue<string>("CertSubjectName", string.Empty);
                CertificateStoreName = msalConfigurationSection.GetValue<string>("CertStoreName", "My");
                ValidCertificateOnly = msalConfigurationSection.GetValue<bool>("ValidCertificateOnly", true);
                SendX5C = msalConfigurationSection.GetValue<bool>("SendX5C", false);
                ClientSecret = msalConfigurationSection.GetValue<string>("ClientSecret", string.Empty);
                AuthType = msalConfigurationSection.GetValue<AuthTypes>("AuthType", AuthTypes.ClientSecret);
                FederatedClientId = msalConfigurationSection.GetValue<string>("FederatedClientId", string.Empty);
                FederatedTokenFile = msalConfigurationSection.GetValue<string>("FederatedTokenFile", string.Empty);
                EnableContainerIMDS = msalConfigurationSection.GetValue<bool>("EnabledContainerIMDS", false);
                AssertionRequestOptions = msalConfigurationSection.GetSection("AssertionRequestOptions").Get<AssertionRequestOptions>();
            }

            ValidateConfiguration();
        }

        /// <inheritdoc/>
        public AuthTypes AuthType { get; set; } = AuthTypes.ClientSecret;

        /// <inheritdoc/>
        public string CertificateThumbPrint { get; set; }

        /// <inheritdoc/>
        public string ClientSecret { get; set; }

        /// <inheritdoc/>
        public string CertificateSubjectName { get; set; }

        /// <inheritdoc/>
        public string CertificateStoreName { get; set; }

        /// <inheritdoc/>
        public bool ValidCertificateOnly { get; set; } = true;

        /// <inheritdoc/>
        public bool SendX5C { get; set; } = false;

        /// <inheritdoc/>
        public string FederatedClientId { get; set; }

        /// <inheritdoc/>
        public string FederatedTokenFile { get; set; }

        /// <inheritdoc/>
        public bool EnableContainerIMDS { get; set; }

        public AssertionRequestOptions AssertionRequestOptions { get; set; }

        /// <summary>
        /// Validates required properties are present in the configuration for the requested authentication type. 
        /// </summary>
        /// <exception cref="System.ArgumentNullException"></exception>
        public void ValidateConfiguration()
        {
            switch (AuthType)
            {
                case AuthTypes.Certificate:
                    if (string.IsNullOrEmpty(ClientId))
                    {
                        throw new ArgumentNullException(nameof(ClientId), "ClientId is required");
                    }
                    if (string.IsNullOrEmpty(CertificateThumbPrint))
                    {
                        throw new ArgumentNullException(nameof(CertificateThumbPrint), "CertificateThumbPrint is required");
                    }
                    if (string.IsNullOrEmpty(Authority) && string.IsNullOrEmpty(TenantId))
                    {
                        throw new ArgumentNullException(nameof(Authority), "TenantId or Authority is required");
                    }
                    break;
                case AuthTypes.CertificateSubjectName:
                    if (string.IsNullOrEmpty(ClientId))
                    {
                        throw new ArgumentNullException(nameof(ClientId), "ClientId is required");
                    }
                    if (string.IsNullOrEmpty(CertificateSubjectName))
                    {
                        throw new ArgumentNullException(nameof(CertificateSubjectName), "CertificateSubjectName is required");
                    }
                    if (string.IsNullOrEmpty(Authority) && string.IsNullOrEmpty(TenantId))
                    {
                        throw new ArgumentNullException(nameof(Authority), "TenantId or Authority is required");
                    }
                    break;
                case AuthTypes.ClientSecret:
                    if (string.IsNullOrEmpty(ClientId))
                    {
                        throw new ArgumentNullException(nameof(ClientId), "ClientId is required");
                    }
                    if (string.IsNullOrEmpty(ClientSecret))
                    {
                        throw new ArgumentNullException(nameof(ClientSecret), "ClientSecret is required");
                    }
                    if (string.IsNullOrEmpty(Authority) && string.IsNullOrEmpty(TenantId))
                    {
                        throw new ArgumentNullException(nameof(Authority), "TenantId or Authority is required");
                    }
                    break;
                case AuthTypes.UserManagedIdentity:
                    if (string.IsNullOrEmpty(ClientId))
                    {
                        throw new ArgumentNullException(nameof(ClientId), "ClientId is required");
                    }
                    break;
                case AuthTypes.SystemManagedIdentity:
                    // No additional validation needed
                    break;
                case AuthTypes.FederatedCredentials:
                    if (string.IsNullOrEmpty(ClientId))
                    {
                        throw new ArgumentNullException(nameof(ClientId), "ClientId is required");
                    }
                    if (string.IsNullOrEmpty(FederatedClientId))
                    {
                        throw new ArgumentNullException(nameof(FederatedClientId), "FederatedClientId is required");
                    }
                    if (string.IsNullOrEmpty(Authority) && string.IsNullOrEmpty(TenantId))
                    {
                        throw new ArgumentNullException(nameof(Authority), "TenantId or Authority is required");
                    }
                    break;
                case AuthTypes.WorkloadIdentity:
                    if (string.IsNullOrEmpty(ClientId))
                    {
                        throw new ArgumentNullException(nameof(ClientId), "ClientId is required");
                    }
                    if (string.IsNullOrEmpty(Authority) && string.IsNullOrEmpty(TenantId))
                    {
                        throw new ArgumentNullException(nameof(Authority), "TenantId or Authority is required");
                    }
                    if (string.IsNullOrEmpty(FederatedTokenFile))
                    {
                        throw new ArgumentNullException(nameof(FederatedTokenFile), "FederatedTokenFile is required");
                    }
                    break;
                default:
                    break;
            }
        }

    }
}
