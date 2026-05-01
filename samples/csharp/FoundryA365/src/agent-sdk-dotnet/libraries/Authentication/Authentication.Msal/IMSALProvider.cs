// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Identity.Client;

namespace Microsoft.Agents.Authentication.Msal
{
    public interface IMSALProvider
    {
        IApplicationBase CreateClientApplication();
    }
}
