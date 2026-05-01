// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Msal.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Agents.Authentication.Msal.Utils
{
    /// <summary>
    /// Gets a certificate from an X509Store.
    /// </summary>
    /// <param name="_config"></param>
    /// <param name="_logger"></param>
    internal class X509StoreCertificateProvider(IMSALConnectionSettings _config, ILogger _logger) : ICertificateProvider
    {
        private readonly IMSALConnectionSettings config = _config;
        private readonly ILogger logger = _logger;

        public X509Certificate2 GetCertificate()
        {
            X509Certificate2 cert = null;
            if (config.AuthType == Model.AuthTypes.Certificate ||
                config.AuthType == Model.AuthTypes.CertificateSubjectName)
            {
                // Open the Cert store location and then open the store in readonly mode. 
                using X509Store store = new X509Store(GetStoreName(config.CertificateStoreName), StoreLocation.CurrentUser);
                //open store for read only
                store.Open(OpenFlags.ReadOnly);

                if (config.AuthType == Model.AuthTypes.CertificateSubjectName)
                {
                    logger.LogInformation("Reading certificate SubjectName - {config.CertificateSubjectName}", config.CertificateSubjectName);
                    X509Certificate2Collection signingCerts = store.Certificates.Find(X509FindType.FindBySubjectName, config.CertificateSubjectName, config.ValidCertificateOnly);
                    if (signingCerts.Count > 0)
                    {
                        cert = signingCerts[0];
                    }

                    if (cert == null)
                    {
                        logger.LogError("Could not find certificate SubjectName - {config.CertificateSubjectName}", config.CertificateSubjectName);
                    }
                    else
                    {
                        logger.LogInformation("Completed reading certificate SubjectName - {config.CertificateSubjectName}", config.CertificateSubjectName);
                    }
                }
                else
                {
                    logger.LogInformation("Reading certificate Thumbprint - {config.CertificateThumbPrint}", config.CertificateThumbPrint);

                    X509Certificate2Collection signingCerts = store.Certificates.Find(X509FindType.FindByThumbprint, config.CertificateThumbPrint, config.ValidCertificateOnly);
                    if (signingCerts.Count > 0)
                    {
                        cert = signingCerts[0];
                    }

                    if (cert == null)
                    {
                        logger.LogError("Could not find certificate Thumbprint - {config.CertificateThumbPrint}", config.CertificateThumbPrint);
                    }
                    else
                    {
                        logger.LogInformation("Completed reading certificate Thumbprint - {config.CertificateThumbPrint}", config.CertificateThumbPrint);
                    }
                }
                store.Close();
                store.Dispose();
            }
            return cert;
        }

        /// <summary>
        /// Parse Store name. 
        /// </summary>
        /// <param name="storeName"></param>
        /// <returns></returns>
        public StoreName GetStoreName(string storeName)
        {
            if (string.IsNullOrEmpty(storeName))
            {
                logger.LogInformation("StoreName is null or empty. Defaulting to My");
                return StoreName.My;
            }

            if (Enum.TryParse(storeName, true, out StoreName storeNameEnum))
            {
                logger.LogInformation("{StoreName} found", storeNameEnum.ToString());
                return storeNameEnum;
            }
            else
            {
                logger.LogInformation("StoreName not found. Defaulting to My");
                return StoreName.My;
            }
        }
    }
}
