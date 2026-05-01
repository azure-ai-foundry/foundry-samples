// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Authentication.Msal.Model
{
    public enum AuthTypes
    {
        Certificate,
        CertificateSubjectName,
        ClientSecret,
        UserManagedIdentity,
        SystemManagedIdentity,
        FederatedCredentials,
        WorkloadIdentity
    }
}
