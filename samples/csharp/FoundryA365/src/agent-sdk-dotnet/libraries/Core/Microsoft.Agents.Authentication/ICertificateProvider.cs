// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Agents.Authentication
{
    /// <summary>
    /// Defines a provider for retrieving X.509 certificates.
    /// </summary>
    public interface ICertificateProvider
    {
        /// <summary>
        /// Gets an <see cref="System.Security.Cryptography.X509Certificates.X509Certificate2"/> instance.
        /// </summary>
        /// <returns>
        /// An X.509 certificate.
        /// </returns>
        X509Certificate2 GetCertificate();
    }
}
